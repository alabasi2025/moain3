=== FILE: src/Hrms.Api/Features/Employees/EmployeesController.cs ===
using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Hrms.Api.Features.Employees;

[ApiController]
[Route("api/v1/employees")]
public sealed class EmployeesController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser,
    FieldEncryptor encryptor) : ControllerBase
{
    [HttpGet]
    [HasPermission("employees.employees.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> List(
        string? search = null,
        long? orgNodeId = null,
        long? statusId = null,
        int page = 1,
        int pageSize = 25)
    {
        if (page < 1 || pageSize is < 1 or > 200)
            throw new ArgumentException("بيانات تقسيم الصفحات غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        var args = FeatureDb.Parameters(currentUser, new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            OrgNodeId = orgNodeId,
            StatusId = statusId,
            Offset = ((long)page - 1) * pageSize,
            PageSize = pageSize
        });

        const string filter = """
            FROM hr.Employees
            WHERE CompanyId=@CompanyId AND IsDeleted=0
              AND (@Search IS NULL OR FullNameAr LIKE N'%' + @Search + N'%'
                   OR CONVERT(nvarchar(20), EmployeeNo)=@Search
                   OR CONVERT(nvarchar(20), FingerprintNo)=@Search)
              AND (@OrgNodeId IS NULL OR OrgNodeId=@OrgNodeId)
              AND (@StatusId IS NULL OR StatusId=@StatusId)
            """;

        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var total = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) " + filter, args, tx);
        var rows = await FeatureDb.Query(db, """
            SELECT Id,EmployeeNo,FingerprintNo,FullNameAr,OrgNodeId,JobId,StatusId,HireDate,WorkStartDate
            """ + filter + " ORDER BY Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", args, tx);
        tx.Commit();
        return new(rows.Select(FeatureDb.JsonRow).ToArray(), new(page, pageSize, total));
    }

    [HttpGet("{id:long}")]
    [HasPermission("employees.employees.read")]
    public async Task<ApiResponse<Dictionary<string, object?>>> Get(long id)
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var row = await FeatureDb.Require(db, tx, currentUser, "Employees", id);
        var accessed = new List<string>();

        foreach (var field in new[] { "IdentityNo", "PassportNo", "BankAccount" })
        {
            var permission = field == "BankAccount"
                ? "employees.financial.read"
                : "employees.government.read";

            if (currentUser.Has(permission) && row[field] is string ciphertext)
            {
                row[field] = encryptor.Decrypt(ciphertext);
                accessed.Add(field);
            }
            else
            {
                row[field] = null;
            }
        }

        if (accessed.Count != 0)
            await FeatureDb.Audit(db, tx, currentUser, "Employees", id, "read_sensitive",
                new { fields = accessed });

        tx.Commit();
        return new(FeatureDb.JsonRow(row));
    }

    [HttpPost]
    [HasPermission("employees.employees.create")]
    public async Task<ApiResponse<object>> Create([FromBody] EmployeeInput input)
    {
        Validate(input);
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var employeeNo = input.EmployeeNo ?? await NextEmployeeNo(db, tx);
            await CheckDuplicates(db, tx, null, employeeNo, input.FingerprintNo);
            var values = Values(input, employeeNo);
            var id = await FeatureDb.Insert(db, tx, currentUser, "Employees", values);
            var row = await FeatureDb.Require(db, tx, currentUser, "Employees", id);
            tx.Commit();
            return new(new { id, rowVersion = row["RowVersion"] });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("رقم الموظف أو رقم البصمة مستخدم مسبقاً", ex);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("employees.employees.update")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] EmployeeInput input)
    {
        Validate(input);
        var rowVersion = ParseIfMatch();
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var existing = await FeatureDb.Require(db, tx, currentUser, "Employees", id);
            if (FeatureDb.Date(existing, "WorkStartDate") is { } workStart && input.HireDate > workStart)
                throw new ArgumentException("تاريخ التعيين لا يجوز أن يلي تاريخ مباشرة العمل");

            var employeeNo = input.EmployeeNo ?? FeatureDb.Int(existing, "EmployeeNo");
            await CheckDuplicates(db, tx, id, employeeNo, input.FingerprintNo);
            await FeatureDb.Update(db, tx, currentUser, "Employees", id,
                Values(input, employeeNo), rowVersion);
            var row = await FeatureDb.Require(db, tx, currentUser, "Employees", id);
            tx.Commit();
            return new(new { id, rowVersion = row["RowVersion"] });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("رقم الموظف أو رقم البصمة مستخدم مسبقاً", ex);
        }
    }

    [HttpDelete("{id:long}")]
    [HasPermission("employees.employees.delete")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await FeatureDb.Require(db, tx, currentUser, "Employees", id);
        await FeatureDb.Delete(db, tx, currentUser, "Employees", id);
        tx.Commit();
        return new(new { id });
    }

    [HttpPost("{id:long}/start-work")]
    [HasPermission("employees.employees.process")]
    public async Task<ApiResponse<object>> StartWork(long id, [FromBody] StartWorkInput input)
    {
        if (input.StartDate == default || input.Notes?.Length > 500)
            throw new ArgumentException("بيانات مباشرة العمل غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var employee = await FeatureDb.Require(db, tx, currentUser, "Employees", id);
        if (input.StartDate < FeatureDb.Date(employee, "HireDate")!.Value)
            throw new ArgumentException("لا يمكن مباشرة العمل قبل تاريخ التعيين");

        var workStartId = await FeatureDb.Insert(db, tx, currentUser, "EmployeeWorkStarts",
            FeatureDb.Values(new
            {
                EmployeeId = id,
                input.StartDate,
                ApprovedBy = currentUser.UserId,
                ApprovedAt = DateTimeOffset.UtcNow,
                input.Notes
            }));

        await FeatureDb.Update(db, tx, currentUser, "Employees", id,
            FeatureDb.Values(new
            {
                WorkStartDate = input.StartDate,
                LastWorkStartDate = input.StartDate
            }));
        tx.Commit();
        return new(new { id = workStartId, employeeId = id, input.StartDate });
    }

    [HttpGet("{id:long}/history")]
    [HasPermission("employees.history.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> History(long id)
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await FeatureDb.Require(db, tx, currentUser, "Employees", id);
        var rows = await FeatureDb.Query(db, """
            SELECT * FROM hr.vw_EmployeeHistory
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND EmployeeId=@EmployeeId
            """, FeatureDb.Parameters(currentUser, new { EmployeeId = id }), tx);
        tx.Commit();
        return new(rows.Select(FeatureDb.JsonRow).ToArray());
    }

    private byte[]? ParseIfMatch()
    {
        if (!Request.Headers.TryGetValue("If-Match", out var header))
            return null;
        if (header.Count != 1)
            throw new ArgumentException("قيمة If-Match غير صالحة");

        var text = header.ToString().Trim();
        if (text.StartsWith('"') && text.EndsWith('"') && text.Length >= 2)
            text = text[1..^1];

        try
        {
            var bytes = Convert.FromBase64String(text);
            if (bytes.Length != 8)
                throw new ArgumentException("قيمة If-Match غير صالحة");
            return bytes;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("قيمة If-Match غير صالحة", ex);
        }
    }

    private async Task<int> NextEmployeeNo(IDbConnection db, IDbTransaction tx)
    {
        var maximum = await db.ExecuteScalarAsync<long>("""
            SELECT COALESCE(MAX(CONVERT(bigint,EmployeeNo)),0)
            FROM hr.Employees WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@CompanyId AND IsDeleted=0
            """, FeatureDb.Parameters(currentUser), tx);
        if (maximum >= int.MaxValue)
            throw new InvalidOperationException("نفدت أرقام الموظفين");
        return checked((int)maximum + 1);
    }

    private async Task CheckDuplicates(
        IDbConnection db, IDbTransaction tx, long? id, int employeeNo, int? fingerprintNo)
    {
        var duplicates = await FeatureDb.Query(db, """
            SELECT EmployeeNo,FingerprintNo
            FROM hr.Employees WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@CompanyId AND IsDeleted=0
              AND (@Id IS NULL OR Id<>@Id)
              AND (EmployeeNo=@EmployeeNo
                   OR (@FingerprintNo IS NOT NULL AND FingerprintNo=@FingerprintNo))
            """, FeatureDb.Parameters(currentUser, new
        {
            Id = id,
            EmployeeNo = employeeNo,
            FingerprintNo = fingerprintNo
        }), tx);

        if (duplicates.Any(r => FeatureDb.Int(r, "EmployeeNo") == employeeNo))
            throw new InvalidOperationException("رقم الموظف مستخدم مسبقاً");
        if (duplicates.Count != 0)
            throw new InvalidOperationException("رقم البصمة مستخدم مسبقاً");
    }

    private Dictionary<string, object?> Values(EmployeeInput input, int employeeNo)
    {
        var values = FeatureDb.Values(input);
        values["EmployeeNo"] = employeeNo;
        values["FirstNameAr"] = input.FirstNameAr.Trim();
        values["MiddleNameAr"] = input.MiddleNameAr?.Trim();
        values["LastNameAr"] = input.LastNameAr.Trim();
        values["FullNameAr"] = JoinNames(input.FirstNameAr, input.MiddleNameAr, input.LastNameAr);
        values["FullNameEn"] = JoinNames(input.FirstNameEn, input.MiddleNameEn, input.LastNameEn);
        values["IdentityNo"] = Encrypt(input.IdentityNo);
        values["PassportNo"] = Encrypt(input.PassportNo);
        values["BankAccount"] = Encrypt(input.BankAccount);
        return values;
    }

    private string? Encrypt(string? value)
    {
        var encrypted = encryptor.Encrypt(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
        if (encrypted?.Length > 200)
            throw new ArgumentException("طول الحقل الحساس بعد التشفير يتجاوز سعة التخزين");
        return encrypted;
    }

    private static string JoinNames(params string?[] names) =>
        string.Join(" ", names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()));

    private static void Validate(EmployeeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FirstNameAr) ||
            string.IsNullOrWhiteSpace(input.LastNameAr) ||
            input.Gender is not ("M" or "F") ||
            input.OrgNodeId <= 0 || input.StatusId <= 0 || input.HireDate == default)
            throw new ArgumentException("الاسم الأول واسم العائلة والجنس والإدارة وتاريخ التعيين والحالة إلزامية");

        if (input.EmployeeNo is <= 0 || input.FingerprintNo is < 0)
            throw new ArgumentException("رقم الموظف أو رقم البصمة غير صالح");
        if (input.ContractStart.HasValue && input.ContractEnd < input.ContractStart)
            throw new ArgumentException("نهاية العقد يجب ألا تسبق بدايته");
        if (input.BirthDate is { } birth &&
            birth > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-15))
            throw new ArgumentException("سن العمل يجب ألا يقل عن 15 عاماً");
        if (input.DailyHours is < 0 or > 24 || input.MonthDays is < 1 or > 31 ||
            input.HourRate < 0 || input.DayRate < 0 ||
            input.SocialCompanyPct is < 0 or > 100 || input.SocialEmployeePct is < 0 or > 100)
            throw new ArgumentException("بيانات ساعات العمل أو الأجور أو نسب التأمين غير صالحة");

        var limits = new Dictionary<string, int>
        {
            [nameof(input.Barcode)] = 50,
            [nameof(input.FirstNameAr)] = 100,
            [nameof(input.MiddleNameAr)] = 100,
            [nameof(input.LastNameAr)] = 100,
            [nameof(input.FirstNameEn)] = 100,
            [nameof(input.MiddleNameEn)] = 100,
            [nameof(input.LastNameEn)] = 100,
            [nameof(input.Mobile)] = 30,
            [nameof(input.Email)] = 200,
            [nameof(input.Notes)] = 1000,
            [nameof(input.IdentityProfession)] = 200,
            [nameof(input.IdentityCopyNo)] = 20,
            [nameof(input.IdentityOther)] = 200,
            [nameof(input.PassportIssuePlace)] = 100,
            [nameof(input.BorderNo)] = 50,
            [nameof(input.InsuranceClass)] = 50,
            [nameof(input.InsurancePolicyNo)] = 50,
            [nameof(input.SocialInsuranceNo)] = 50,
            [nameof(input.LaborOfficeNo)] = 50
        };
        var values = FeatureDb.Values(input);
        foreach (var (name, limit) in limits)
            if (values[name] is string text && text.Length > limit)
                throw new ArgumentException($"تجاوز الحقل {name} الطول المسموح");
    }
}

public sealed class EmployeeInput
{
    public int? EmployeeNo { get; init; }
    public int? FingerprintNo { get; init; }
    public string? Barcode { get; init; }
    public string FirstNameAr { get; init; } = "";
    public string? MiddleNameAr { get; init; }
    public string LastNameAr { get; init; } = "";
    public string? FirstNameEn { get; init; }
    public string? MiddleNameEn { get; init; }
    public string? LastNameEn { get; init; }
    public string Gender { get; init; } = "";
    public long? NationalityId { get; init; }
    public long? ReligionId { get; init; }
    public long? MaritalStatusId { get; init; }
    public DateOnly? BirthDate { get; init; }
    public long? BirthCountryId { get; init; }
    public long? BirthCityId { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public string? Notes { get; init; }
    public long? PhotoAttachmentId { get; init; }
    public long OrgNodeId { get; init; }
    public long? JobGradeId { get; init; }
    public long? JobId { get; init; }
    public string? IdentityProfession { get; init; }
    public long? SponsorId { get; init; }
    public long? EmployeeGroupId { get; init; }
    public long? CostCenterId { get; init; }
    public long? ProjectId { get; init; }
    public long? BankId { get; init; }
    public string? BankAccount { get; init; }
    public DateOnly HireDate { get; init; }
    public long? WorkNatureId { get; init; }
    public long? ContractTypeId { get; init; }
    public DateOnly? ContractStart { get; init; }
    public DateOnly? ContractEnd { get; init; }
    public bool IsOpenContract { get; init; }
    public long StatusId { get; init; }
    public decimal DailyHours { get; init; } = 8;
    public int MonthDays { get; init; } = 30;
    public decimal HourRate { get; init; }
    public decimal DayRate { get; init; }
    public bool OvertimeEnabled { get; init; }
    public bool MobileAttendanceEnabled { get; init; }
    public string? IdentityNo { get; init; }
    public DateOnly? IdentityIssueDate { get; init; }
    public DateOnly? IdentityExpiryDate { get; init; }
    public string? IdentityCopyNo { get; init; }
    public string? IdentityOther { get; init; }
    public string? PassportNo { get; init; }
    public string? PassportIssuePlace { get; init; }
    public DateOnly? PassportIssueDate { get; init; }
    public DateOnly? PassportExpiryDate { get; init; }
    public string? BorderNo { get; init; }
    public long? InsuranceCompanyId { get; init; }
    public string? InsuranceClass { get; init; }
    public string? InsurancePolicyNo { get; init; }
    public DateOnly? InsuranceDueDate { get; init; }
    public string? SocialInsuranceNo { get; init; }
    public string? LaborOfficeNo { get; init; }
    public decimal SocialCompanyPct { get; init; }
    public decimal SocialEmployeePct { get; init; }
}

public sealed record StartWorkInput(DateOnly StartDate, string? Notes);

internal static class FeatureDb
{
    private static readonly HashSet<string> Tables = new(StringComparer.Ordinal)
    {
        "Employees", "EmployeeWorkStarts", "Shifts", "ShiftDays",
        "EmployeeShifts", "PunchLogs", "DailyAttendance"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public static IDbConnection Open(IDbConnectionFactory factory)
    {
        var connection = factory.Create();
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public static Dictionary<string, object?> Values(object value)
    {
        if (value is IDictionary<string, object?> dictionary)
            return new(dictionary, StringComparer.OrdinalIgnoreCase);

        return value.GetType().GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, p => p.GetValue(value), StringComparer.OrdinalIgnoreCase);
    }

    public static DynamicParameters Parameters(ICurrentUser user, object? extra = null)
    {
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", user.CompanyId);
        parameters.Add("UserId", user.UserId);
        if (extra is not null)
        {
            foreach (var (name, value) in Values(extra))
                Add(parameters, name, value);
        }
        return parameters;
    }

    private static void Add(DynamicParameters parameters, string name, object? value)
    {
        switch (value)
        {
            case DateOnly date:
                parameters.Add(name, date.ToDateTime(TimeOnly.MinValue), DbType.Date);
                break;
            case TimeOnly time:
                parameters.Add(name, time.ToTimeSpan(), DbType.Time);
                break;
            default:
                parameters.Add(name, value);
                break;
        }
    }

    public static async Task<List<Dictionary<string, object?>>> Query(
        IDbConnection db, string sql, object? args = null, IDbTransaction? tx = null)
    {
        var rows = await db.QueryAsync(sql, args, tx);
        return rows.Select(row =>
        {
            var source = (IDictionary<string, object>)row;
            return source.ToDictionary(
                p => p.Key,
                p => p.Value is DBNull ? null : (object?)p.Value,
                StringComparer.OrdinalIgnoreCase);
        }).ToList();
    }

    public static Dictionary<string, object?> JsonRow(Dictionary<string, object?> row) =>
        row.ToDictionary(
            p => JsonNamingPolicy.CamelCase.ConvertName(p.Key),
            p => p.Value,
            StringComparer.Ordinal);

    public static long Long(Dictionary<string, object?> row, string name) =>
        Convert.ToInt64(row[name], CultureInfo.InvariantCulture);

    public static int Int(Dictionary<string, object?> row, string name) =>
        Convert.ToInt32(row[name], CultureInfo.InvariantCulture);

    public static bool Bool(Dictionary<string, object?> row, string name) =>
        Convert.ToBoolean(row[name], CultureInfo.InvariantCulture);

    public static long? NullableLong(Dictionary<string, object?> row, string name) =>
        row.TryGetValue(name, out var value) && value is not null
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : null;

    public static DateOnly? Date(Dictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null)
            return null;
        return value switch
        {
            DateOnly date => date,
            DateTime date => DateOnly.FromDateTime(date),
            DateTimeOffset date => DateOnly.FromDateTime(date.DateTime),
            _ => DateOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture)
        };
    }

    public static TimeOnly? Time(Dictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var value) || value is null)
            return null;
        return value switch
        {
            TimeOnly time => time,
            TimeSpan time => TimeOnly.FromTimeSpan(time),
            DateTime time => TimeOnly.FromDateTime(time),
            _ => TimeOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture)
        };
    }

    private static string Table(string table)
    {
        if (!Tables.Contains(table))
            throw new ArgumentException("اسم الجدول غير صالح");
        return "hr.[" + table + "]";
    }

    private static string Column(string column)
    {
        if (column.Length == 0 || !column.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("اسم العمود غير صالح");
        return "[" + column + "]";
    }

    public static async Task<Dictionary<string, object?>> Require(
        IDbConnection db, IDbTransaction tx, ICurrentUser user, string table, long id)
    {
        var rows = await Query(db,
            $"SELECT * FROM {Table(table)} WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Id=@Id",
            Parameters(user, new { Id = id }), tx);
        return rows.SingleOrDefault() ?? throw new KeyNotFoundException("السجل غير موجود");
    }

    public static async Task<long> Insert(
        IDbConnection db, IDbTransaction tx, ICurrentUser user,
        string table, Dictionary<string, object?> values)
    {
        var columns = string.Join(",", values.Keys.Select(Column));
        var placeholders = string.Join(",", values.Keys.Select(k => "@" + k));
        var id = await db.ExecuteScalarAsync<long>($"""
            INSERT INTO {Table(table)}
                (CompanyId,CreatedAt,CreatedBy,IsDeleted,{columns})
            OUTPUT INSERTED.Id
            VALUES (@CompanyId,SYSUTCDATETIME(),@UserId,0,{placeholders})
            """, Parameters(user, values), tx);
        await Audit(db, tx, user, table, id, "create", values);
        return id;
    }

    public static async Task Update(
        IDbConnection db, IDbTransaction tx, ICurrentUser user,
        string table, long id, Dictionary<string, object?> values, byte[]? rowVersion = null)
    {
        var args = Parameters(user, values);
        args.Add("Id", id);
        args.Add("rv", rowVersion, DbType.Binary);
        var assignments = string.Join(",", values.Keys.Select(k => Column(k) + "=@" + k));
        var concurrency = rowVersion is null ? "" : " AND RowVersion=@rv";
        var ids = (await db.QueryAsync<long>($"""
            UPDATE {Table(table)}
            SET {assignments},UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UserId
            OUTPUT INSERTED.Id
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Id=@Id
            {concurrency}
            """, args, tx)).ToArray();

        if (ids.Length == 0)
        {
            if (rowVersion is not null)
                throw new InvalidOperationException("تم تعديل السجل من مستخدم آخر");
            throw new KeyNotFoundException("السجل غير موجود");
        }

        await Audit(db, tx, user, table, id, "update", values);
    }

    public static async Task Delete(
        IDbConnection db, IDbTransaction tx, ICurrentUser user, string table, long id)
    {
        var ids = (await db.QueryAsync<long>($"""
            UPDATE {Table(table)}
            SET IsDeleted=1,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UserId
            OUTPUT INSERTED.Id
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Id=@Id
            """, Parameters(user, new { Id = id }), tx)).ToArray();
        if (ids.Length == 0)
            throw new KeyNotFoundException("السجل غير موجود");
        await Audit(db, tx, user, table, id, "delete", new { isDeleted = true });
    }

    public static Task Audit(
        IDbConnection db, IDbTransaction tx, ICurrentUser user,
        string entity, long id, string action, object values) =>
        db.ExecuteAsync("""
            INSERT INTO hr.AuditLogs
                (CompanyId,UserId,EntityName,EntityId,ActionType,NewValues,IpAddress)
            VALUES (@CompanyId,@UserId,@EntityName,@EntityId,@ActionType,@NewValues,@IpAddress)
            """, Parameters(user, new
        {
            EntityName = entity,
            EntityId = id,
            ActionType = action,
            NewValues = JsonSerializer.Serialize(values, JsonOptions),
            IpAddress = user.Ip
        }), tx);
}
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Attendance/ShiftsController.cs ===
using System.Data;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Hrms.Domain.Attendance;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Features.Attendance;

[ApiController]
[Route("api/v1/shifts")]
public sealed class ShiftsController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [HasPermission("attendance.shifts.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> List()
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var shifts = await FeatureDb.Query(db, """
            SELECT * FROM hr.Shifts
            WHERE CompanyId=@CompanyId AND IsDeleted=0 ORDER BY Code,Id
            """, FeatureDb.Parameters(currentUser), tx);
        var days = await FeatureDb.Query(db, """
            SELECT d.* FROM hr.ShiftDays d
            INNER JOIN hr.Shifts s ON s.Id=d.ShiftId AND s.CompanyId=d.CompanyId
            WHERE d.CompanyId=@CompanyId AND d.IsDeleted=0
              AND s.CompanyId=@CompanyId AND s.IsDeleted=0
            ORDER BY d.DayOfWeek,d.Id
            """, FeatureDb.Parameters(currentUser), tx);

        var lookup = days.ToLookup(d => FeatureDb.Long(d, "ShiftId"));
        foreach (var shift in shifts)
            shift["ShiftDays"] = lookup[FeatureDb.Long(shift, "Id")]
                .Select(FeatureDb.JsonRow).ToArray();

        tx.Commit();
        return new(shifts.Select(FeatureDb.JsonRow).ToArray());
    }

    [HttpGet("{id:long}")]
    [HasPermission("attendance.shifts.read")]
    public async Task<ApiResponse<Dictionary<string, object?>>> Get(long id)
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var shift = await FeatureDb.Require(db, tx, currentUser, "Shifts", id);
        var days = await FeatureDb.Query(db, """
            SELECT * FROM hr.ShiftDays
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND ShiftId=@ShiftId
            ORDER BY DayOfWeek,Id
            """, FeatureDb.Parameters(currentUser, new { ShiftId = id }), tx);
        shift["ShiftDays"] = days.Select(FeatureDb.JsonRow).ToArray();
        tx.Commit();
        return new(FeatureDb.JsonRow(shift));
    }

    [HttpPost]
    [HasPermission("attendance.shifts.create")]
    public async Task<ApiResponse<object>> Create([FromBody] ShiftInput input)
    {
        Validate(input);
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await CheckCode(db, tx, input.Code, null);
        var id = await FeatureDb.Insert(db, tx, currentUser, "Shifts", ShiftValues(input));
        await ReplaceDays(db, tx, id, input.ShiftDays!);
        var row = await FeatureDb.Require(db, tx, currentUser, "Shifts", id);
        tx.Commit();
        return new(new { id, rowVersion = row["RowVersion"] });
    }

    [HttpPut("{id:long}")]
    [HasPermission("attendance.shifts.update")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] ShiftInput input)
    {
        Validate(input);
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await FeatureDb.Require(db, tx, currentUser, "Shifts", id);
        await CheckCode(db, tx, input.Code, id);
        await FeatureDb.Update(db, tx, currentUser, "Shifts", id, ShiftValues(input));
        await ReplaceDays(db, tx, id, input.ShiftDays!);
        var row = await FeatureDb.Require(db, tx, currentUser, "Shifts", id);
        tx.Commit();
        return new(new { id, rowVersion = row["RowVersion"] });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("attendance.shifts.delete")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await FeatureDb.Require(db, tx, currentUser, "Shifts", id);
        await ReplaceDays(db, tx, id, []);
        await FeatureDb.Delete(db, tx, currentUser, "Shifts", id);
        tx.Commit();
        return new(new { id });
    }

    [HttpPost("simulate")]
    [HasPermission("attendance.shifts.read")]
    public ApiResponse<DailyResult> Simulate([FromBody] ShiftSimulationInput input)
    {
        if (input.Shift is null || input.Day is null || input.Punches is null ||
            input.Day.Excuses is null || input.Day.WorkDate == default)
            throw new ArgumentException("بيانات المحاكاة غير مكتملة");
        if (input.Shift.WorkMinutes < 0 || input.Shift.BreakMinutes < 0)
            throw new ArgumentException("دقائق العمل والاستراحة غير صالحة");
        return new(AttendanceEngine.Compute(input.Shift, input.Day, input.Punches));
    }

    [HttpPost("/api/v1/shift-assignments/apply")]
    [HasPermission("attendance.shift_assignments.create")]
    public async Task<ApiResponse<ShiftAssignmentResult>> Apply(
        [FromBody] ShiftAssignmentInput input)
    {
        if (input.EmployeeIds is null || input.EmployeeIds.Length == 0 ||
            input.EmployeeIds.Any(id => id <= 0) || input.ShiftId <= 0 ||
            input.EffectiveFrom <= DateOnly.MinValue ||
            input.EffectiveTo < input.EffectiveFrom)
            throw new ArgumentException("بيانات ربط الفترة غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var shift = await FeatureDb.Require(db, tx, currentUser, "Shifts", input.ShiftId);
        if (!FeatureDb.Bool(shift, "IsActive"))
            throw new InvalidOperationException("الفترة غير مفعلة");

        var applied = new List<long>();
        var rejected = new List<ShiftAssignmentRejection>();

        foreach (var employeeId in input.EmployeeIds.Distinct())
        {
            var args = FeatureDb.Parameters(currentUser, new
            {
                EmployeeId = employeeId,
                input.EffectiveFrom,
                input.EffectiveTo
            });

            var employees = await FeatureDb.Query(db, """
                SELECT Id,WorkStartDate FROM hr.Employees WITH (UPDLOCK,HOLDLOCK)
                WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Id=@EmployeeId
                """, args, tx);
            var employee = employees.SingleOrDefault();
            if (employee is null)
            {
                rejected.Add(new(employeeId, "الموظف غير موجود"));
                continue;
            }
            if (FeatureDb.Date(employee, "WorkStartDate") is null)
            {
                rejected.Add(new(employeeId, "لا يمكن ربط الفترة قبل تسجيل مباشرة العمل"));
                continue;
            }

            var overlapping = await FeatureDb.Query(db, """
                SELECT Id,EffectiveFrom,EffectiveTo
                FROM hr.EmployeeShifts WITH (UPDLOCK,HOLDLOCK)
                WHERE CompanyId=@CompanyId AND IsDeleted=0 AND EmployeeId=@EmployeeId
                  AND (EffectiveTo IS NULL OR EffectiveTo>=@EffectiveFrom)
                  AND (@EffectiveTo IS NULL OR EffectiveFrom<=@EffectiveTo)
                ORDER BY EffectiveFrom,Id
                """, args, tx);

            if (overlapping.Any(r => FeatureDb.Date(r, "EffectiveTo") is not null))
            {
                rejected.Add(new(employeeId, "يوجد ربط محدد النهاية متداخل مع المدة المطلوبة"));
                continue;
            }

            if (overlapping.Any(r => FeatureDb.Date(r, "EffectiveFrom") >= input.EffectiveFrom))
            {
                rejected.Add(new(employeeId, "يوجد ربط مفتوح يبدأ في تاريخ الربط المطلوب أو بعده"));
                continue;
            }

            foreach (var existing in overlapping)
                await FeatureDb.Update(db, tx, currentUser, "EmployeeShifts",
                    FeatureDb.Long(existing, "Id"),
                    FeatureDb.Values(new { EffectiveTo = input.EffectiveFrom.AddDays(-1) }));

            await FeatureDb.Insert(db, tx, currentUser, "EmployeeShifts", FeatureDb.Values(new
            {
                EmployeeId = employeeId,
                input.ShiftId,
                input.EffectiveFrom,
                input.EffectiveTo
            }));
            applied.Add(employeeId);
        }

        tx.Commit();
        return new(new(applied, rejected));
    }

    private async Task CheckCode(IDbConnection db, IDbTransaction tx, int code, long? id)
    {
        var count = await db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM hr.Shifts WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND Code=@Code
              AND (@Id IS NULL OR Id<>@Id)
            """, FeatureDb.Parameters(currentUser, new { Code = code, Id = id }), tx);
        if (count != 0)
            throw new InvalidOperationException("رمز الفترة مستخدم مسبقاً");
    }

    private async Task ReplaceDays(
        IDbConnection db, IDbTransaction tx, long shiftId, IReadOnlyList<ShiftDayInput> days)
    {
        var existing = await db.QueryAsync<long>("""
            SELECT Id FROM hr.ShiftDays
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND ShiftId=@ShiftId
            """, FeatureDb.Parameters(currentUser, new { ShiftId = shiftId }), tx);
        foreach (var id in existing)
            await FeatureDb.Delete(db, tx, currentUser, "ShiftDays", id);

        foreach (var day in days)
            await FeatureDb.Insert(db, tx, currentUser, "ShiftDays", FeatureDb.Values(new
            {
                ShiftId = shiftId,
                day.DayOfWeek,
                day.IsWorkDay,
                day.StartTime,
                day.EndTime
            }));
    }

    private static Dictionary<string, object?> ShiftValues(ShiftInput input)
    {
        var values = FeatureDb.Values(input);
        values.Remove(nameof(ShiftInput.ShiftDays));
        values["NameAr"] = input.NameAr.Trim();
        return values;
    }

    private static void Validate(ShiftInput input)
    {
        if (input.Code <= 0 || string.IsNullOrWhiteSpace(input.NameAr) ||
            input.NameAr.Length > 200 || input.NameEn?.Length > 200 ||
            input.Notes?.Length > 1000 ||
            string.IsNullOrWhiteSpace(input.CycleType) || input.CycleType.Length > 20 ||
            string.IsNullOrWhiteSpace(input.PunchMode) || input.PunchMode.Length > 20)
            throw new ArgumentException("بيانات الفترة غير صالحة");

        if (input.WorkMinutes is < 0 or > 1440 || input.BreakMinutes is < 0 or > 1440 ||
            input.BeforeInMin is < 0 or > 1440 || input.AfterInMin is < 0 or > 1440 ||
            input.BeforeOutMin is < 0 or > 1440 || input.AfterOutMin is < 0 or > 1440 ||
            input.LateGraceMin is < 0 or > 1440 || input.EarlyGraceMin is < 0 or > 1440 ||
            input.FlexibleHours is < 0 or > 24)
            throw new ArgumentException("دقائق الفترة غير صالحة");

        if (input.ShiftDays is null || input.ShiftDays.Any(d => d is null) ||
            input.ShiftDays.Any(d => d.DayOfWeek is < 0 or > 6) ||
            input.ShiftDays.Select(d => d.DayOfWeek).Distinct().Count() != input.ShiftDays.Count)
            throw new ArgumentException("أيام الفترة غير صالحة أو مكررة");
    }
}

public sealed class ShiftInput
{
    public int Code { get; init; }
    public string NameAr { get; init; } = "";
    public string? NameEn { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
    public string CycleType { get; init; } = "weekly";
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int BreakMinutes { get; init; }
    public int WorkMinutes { get; init; }
    public bool CrossesMidnight { get; init; }
    public int BeforeInMin { get; init; }
    public int AfterInMin { get; init; }
    public bool LateGraceEnabled { get; init; }
    public int LateGraceMin { get; init; }
    public bool LateAfterGrace { get; init; }
    public int BeforeOutMin { get; init; }
    public int AfterOutMin { get; init; }
    public bool EarlyGraceEnabled { get; init; }
    public int EarlyGraceMin { get; init; }
    public bool EarlyBeforeGrace { get; init; }
    public string PunchMode { get; init; } = "inout";
    public decimal? FlexibleHours { get; init; }
    public bool RoundWorkTime { get; init; }
    public bool PresentIfNoOut { get; init; }
    public bool PresentIfNoIn { get; init; }
    public bool OvertimeEnabled { get; init; }
    public TimeOnly? OvertimeFrom { get; init; }
    public TimeOnly? OvertimeTo { get; init; }
    public List<ShiftDayInput>? ShiftDays { get; init; } = [];
}

public sealed record ShiftDayInput(
    int DayOfWeek,
    bool IsWorkDay = true,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null);

public sealed record ShiftSimulationInput(
    ShiftDef? Shift,
    DayContext? Day,
    DateTime[]? Punches);

public sealed record ShiftAssignmentInput(
    long[]? EmployeeIds,
    long ShiftId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ShiftAssignmentRejection(long EmployeeId, string Reason);

public sealed record ShiftAssignmentResult(
    IReadOnlyList<long> Applied,
    IReadOnlyList<ShiftAssignmentRejection> Rejected);
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Attendance/PunchesController.cs ===
using System.Data;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Features.Attendance;

[ApiController]
[Route("api/v1/punches")]
public sealed class PunchesController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("manual")]
    [HasPermission("attendance.punches.create")]
    public async Task<ApiResponse<object>> Manual([FromBody] ManualPunchInput input)
    {
        if (input.EmployeeId <= 0 || input.PunchTime == default ||
            input.Direction is not ("in" or "out" or "unknown") ||
            string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 300)
            throw new ArgumentException("الموظف ووقت البصمة والاتجاه والسبب الصحيح إلزامية");

        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var employee = await FeatureDb.Require(db, tx, currentUser, "Employees", input.EmployeeId);
        var id = await FeatureDb.Insert(db, tx, currentUser, "PunchLogs", FeatureDb.Values(new
        {
            input.EmployeeId,
            FingerprintNo = employee["FingerprintNo"],
            input.PunchTime,
            LocalTime = DateTime.SpecifyKind(input.PunchTime.DateTime, DateTimeKind.Unspecified),
            input.Direction,
            Reason = input.Reason.Trim(),
            Source = "manual",
            Status = "accepted",
            ReviewedBy = currentUser.UserId,
            ReviewedAt = DateTimeOffset.UtcNow
        }));
        tx.Commit();
        return new(new { id });
    }

    [HttpGet]
    [HasPermission("attendance.punches.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> List(
        long? employeeId = null, DateOnly? from = null, DateOnly? to = null)
    {
        if (employeeId is <= 0 || (from.HasValue && to.HasValue && from > to))
            throw new ArgumentException("مرشحات البحث غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        var rows = await FeatureDb.Query(db, """
            SELECT p.*
            FROM hr.PunchLogs p
            WHERE p.CompanyId=@CompanyId AND p.IsDeleted=0
              AND (@EmployeeId IS NULL OR p.EmployeeId=@EmployeeId)
              AND (@FromDate IS NULL OR p.LocalTime>=@FromDate)
              AND (@ToDate IS NULL OR CONVERT(date,p.LocalTime)<=@ToDate)
              AND (p.EmployeeId IS NULL OR EXISTS (
                  SELECT 1 FROM hr.Employees e
                  WHERE e.CompanyId=@CompanyId AND e.IsDeleted=0
                    AND e.Id=p.EmployeeId))
            ORDER BY p.LocalTime,p.Id
            """, FeatureDb.Parameters(currentUser, new
        {
            EmployeeId = employeeId,
            FromDate = from,
            ToDate = to
        }));
        return new(rows.Select(FeatureDb.JsonRow).ToArray());
    }
}

public sealed record ManualPunchInput(
    long EmployeeId,
    DateTimeOffset PunchTime,
    string Direction,
    string Reason);
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Attendance/AttendanceProcessor.cs ===
using System.Data;
using System.Globalization;
using Dapper;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Hrms.Domain.Attendance;

namespace Hrms.Api.Features.Attendance;

public sealed class AttendanceProcessor(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser)
{
    public async Task<AttendanceProcessResult> ProcessAsync(AttendanceProcessInput input)
    {
        ValidateRange(input.From, input.To);
        if (input.EmployeeIds?.Any(id => id <= 0) == true)
            throw new ArgumentException("معرف الموظف غير صالح");

        using var db = FeatureDb.Open(connectionFactory);
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var employees = await FeatureDb.Query(db, """
            SELECT Id,OrgNodeId,HireDate,WorkStartDate
            FROM hr.Employees WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@CompanyId AND IsDeleted=0
            ORDER BY Id
            """, FeatureDb.Parameters(currentUser), tx);

        if (input.EmployeeIds is not null)
        {
            var requested = input.EmployeeIds.ToHashSet();
            employees = employees.Where(e => requested.Contains(FeatureDb.Long(e, "Id"))).ToList();
            if (employees.Count != requested.Count)
                throw new KeyNotFoundException("أحد الموظفين غير موجود");
        }

        var monthlyRows = await FeatureDb.Query(db, """
            SELECT * FROM hr.MonthlyAttendances WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND IsClosed=1
            """, FeatureDb.Parameters(currentUser), tx);

        var holidays = await FeatureDb.Query(db, """
            SELECT * FROM hr.OfficialHolidays
            WHERE CompanyId=@CompanyId AND IsDeleted=0 AND IsActive=1
            ORDER BY Id
            """, FeatureDb.Parameters(currentUser), tx);

        foreach (var employee in employees)
        {
            var employeeId = FeatureDb.Long(employee, "Id");
            foreach (var month in monthlyRows)
            {
                var monthlyEmployeeId = FeatureDb.NullableLong(month, "EmployeeId");
                if (monthlyEmployeeId is not null && monthlyEmployeeId != employeeId)
                    continue;
                if (ClosedPeriodOverlaps(month, input.From, input.To))
                    throw new InvalidOperationException("الشهر مقفل");
            }
        }

        var processedDays = 0;
        var skippedManual = 0;
        var shiftCache = new Dictionary<long, ShiftData>();

        foreach (var employee in employees)
        {
            var employeeId = FeatureDb.Long(employee, "Id");
            var args = FeatureDb.Parameters(currentUser, new
            {
                EmployeeId = employeeId,
                FromDate = input.From,
                ToDate = input.To,
                WindowFrom = input.From.AddDays(-1),
                WindowTo = input.To.AddDays(2)
            });

            var existingDaily = await FeatureDb.Query(db, """
                SELECT d.Id,d.WorkDate,d.IsManualOverride,d.MonthlyId
                FROM hr.DailyAttendance d WITH (UPDLOCK,HOLDLOCK)
                WHERE d.CompanyId=@CompanyId AND d.IsDeleted=0
                  AND d.EmployeeId=@EmployeeId AND d.WorkDate>=@FromDate AND d.WorkDate<=@ToDate
                ORDER BY d.WorkDate,d.Id
                """, args, tx);

            var closedIds = monthlyRows.Select(r => FeatureDb.Long(r, "Id")).ToHashSet();
            if (existingDaily.Any(r =>
                    FeatureDb.NullableLong(r, "MonthlyId") is { } monthlyId &&
                    closedIds.Contains(monthlyId)))
                throw new InvalidOperationException("الشهر مقفل");

            var manualDays = existingDaily
                .Where(r => FeatureDb.Bool(r, "IsManualOverride"))
                .Select(r => FeatureDb.Date(r, "WorkDate")!.Value)
                .ToHashSet();

            foreach (var daily in existingDaily.Where(r => !FeatureDb.Bool(r, "IsManualOverride")))
                await FeatureDb.Delete(db, tx, currentUser, "DailyAttendance", FeatureDb.Long(daily, "Id"));

            var assignments = await FeatureDb.Query(db, """
                SELECT es.*
                FROM hr.EmployeeShifts es
                INNER JOIN hr.Shifts s ON s.Id=es.ShiftId AND s.CompanyId=es.CompanyId
                WHERE es.CompanyId=@CompanyId AND es.IsDeleted=0
                  AND s.CompanyId=@CompanyId AND s.IsDeleted=0
                  AND es.EmployeeId=@EmployeeId
                  AND es.EffectiveFrom<=@ToDate
                  AND (es.EffectiveTo IS NULL OR es.EffectiveTo>=@FromDate)
                ORDER BY es.EffectiveFrom DESC,es.Id DESC
                """, args, tx);

            var leaves = await FeatureDb.Query(db, """
                SELECT Id,FromDate,ToDate,IsHalfDay
                FROM hr.Leaves
                WHERE CompanyId=@CompanyId AND IsDeleted=0
                  AND EmployeeId=@EmployeeId AND Status='approved'
                  AND FromDate<=@ToDate AND ToDate>=@FromDate
                ORDER BY IsHalfDay,Id
                """, args, tx);

            var excuses = await FeatureDb.Query(db, """
                SELECT Id,ExcuseDate,FromTime,ToTime
                FROM hr.Excuses
                WHERE CompanyId=@CompanyId AND IsDeleted=0
                  AND EmployeeId=@EmployeeId AND IsApproved=1
                  AND ExcuseDate>=@FromDate AND ExcuseDate<=@ToDate
                ORDER BY ExcuseDate,FromTime,Id
                """, args, tx);

            var punches = (await db.QueryAsync<DateTime>("""
                SELECT LocalTime
                FROM hr.PunchLogs
                WHERE CompanyId=@CompanyId AND IsDeleted=0
                  AND EmployeeId=@EmployeeId AND Status='accepted'
                  AND LocalTime>=@WindowFrom AND LocalTime<@WindowTo
                ORDER BY LocalTime,Id
                """, args, tx)).ToArray();

            for (var day = input.From; day <= input.To; day = day.AddDays(1))
            {
                if (manualDays.Contains(day))
                {
                    skippedManual++;
                    continue;
                }

                var workStart = FeatureDb.Date(employee, "WorkStartDate");
                var hireDate = FeatureDb.Date(employee, "HireDate");
                if (workStart is null || day < workStart.Value || (hireDate.HasValue && day < hireDate.Value))
                    continue;

                var assignment = assignments.FirstOrDefault(a =>
                    FeatureDb.Date(a, "EffectiveFrom")!.Value <= day &&
                    (FeatureDb.Date(a, "EffectiveTo") is not { } end || end >= day));
                if (assignment is null)
                    continue;

                var shiftId = FeatureDb.Long(assignment, "ShiftId");
                if (!shiftCache.TryGetValue(shiftId, out var shiftData))
                {
                    var shift = await FeatureDb.Require(db, tx, currentUser, "Shifts", shiftId);
                    var shiftDays = await FeatureDb.Query(db, """
                        SELECT DayOfWeek,IsWorkDay,StartTime,EndTime
                        FROM hr.ShiftDays
                        WHERE CompanyId=@CompanyId AND IsDeleted=0 AND ShiftId=@ShiftId
                        ORDER BY Id
                        """, FeatureDb.Parameters(currentUser, new { ShiftId = shiftId }), tx);
                    if (shiftDays.GroupBy(d => FeatureDb.Int(d, "DayOfWeek")).Any(g => g.Count() > 1))
                        throw new InvalidOperationException("توجد أيام مكررة في تعريف الفترة");
                    shiftData = new(ToDefinition(shift), shiftDays);
                    shiftCache.Add(shiftId, shiftData);
                }

                var shiftDay = shiftData.Days.SingleOrDefault(d =>
                    FeatureDb.Int(d, "DayOfWeek") == (int)day.DayOfWeek);
                var isWorkDay = shiftData.Days.Count == 0
                    ? day.DayOfWeek != DayOfWeek.Friday
                    : shiftDay is not null && FeatureDb.Bool(shiftDay, "IsWorkDay");

                var definition = shiftData.Definition;
                if (shiftDay is not null)
                {
                    var start = FeatureDb.Time(shiftDay, "StartTime");
                    var end = FeatureDb.Time(shiftDay, "EndTime");
                    if (start.HasValue || end.HasValue)
                    {
                        var effectiveStart = start ?? definition.Start;
                        var effectiveEnd = end ?? definition.End;
                        definition = definition with
                        {
                            Start = effectiveStart,
                            End = effectiveEnd,
                            CrossesMidnight = effectiveEnd <= effectiveStart
                        };
                    }
                }

                var holiday = holidays.FirstOrDefault(h =>
                    (FeatureDb.NullableLong(h, "OrgNodeId") is not { } org ||
                     org == FeatureDb.Long(employee, "OrgNodeId")) && IsHoliday(h, day));

                var leave = leaves.FirstOrDefault(l =>
                    FeatureDb.Date(l, "FromDate")!.Value <= day &&
                    FeatureDb.Date(l, "ToDate")!.Value >= day);

                var dayExcuses = excuses
                    .Where(e => FeatureDb.Date(e, "ExcuseDate") == day)
                    .ToArray();
                var windows = new List<ExcuseWindow>();
                foreach (var excuse in dayExcuses)
                {
                    if (FeatureDb.Time(excuse, "FromTime") is { } from &&
                        FeatureDb.Time(excuse, "ToTime") is { } to)
                        windows.Add(new(from, to));
                }

                var context = new DayContext(
                    day,
                    isWorkDay,
                    holiday is not null,
                    leave is not null,
                    leave is not null && FeatureDb.Bool(leave, "IsHalfDay"),
                    IsRamadan(day),
                    windows);

                var windowFrom = day.AddDays(-1).ToDateTime(TimeOnly.MinValue);
                var windowTo = day.AddDays(2).ToDateTime(TimeOnly.MinValue);
                var dayPunches = punches.Where(p => p >= windowFrom && p < windowTo).ToArray();
                var result = AttendanceEngine.Compute(definition, context, dayPunches);

                await FeatureDb.Insert(db, tx, currentUser, "DailyAttendance", FeatureDb.Values(new
                {
                    EmployeeId = employeeId,
                    WorkDate = day,
                    ShiftId = shiftId,
                    InTime = AsOffset(result.InTime),
                    OutTime = AsOffset(result.OutTime),
                    result.RequiredMinutes,
                    result.WorkMinutes,
                    result.LateMin,
                    result.EarlyLeaveMin,
                    result.AbsentMin,
                    result.OvertimeMin,
                    result.ExcusedMin,
                    Status = result.Status,
                    LeaveId = leave is null ? (long?)null : FeatureDb.Long(leave, "Id"),
                    ExcuseId = dayExcuses.Length == 0
                        ? (long?)null
                        : FeatureDb.Long(dayExcuses[0], "Id"),
                    HolidayId = holiday is null ? (long?)null : FeatureDb.Long(holiday, "Id"),
                    IsManualOverride = false,
                    ComputedAt = DateTimeOffset.UtcNow
                }));
                processedDays++;
            }
        }

        tx.Commit();
        return new(processedDays, employees.Count, skippedManual);
    }

    internal static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from <= DateOnly.MinValue || to > DateOnly.MaxValue.AddDays(-2) ||
            to < from || to.DayNumber - from.DayNumber >= 62)
            throw new ArgumentException("يجب أن تكون المدة صالحة وألا تتجاوز 62 يوماً");
    }

    private static DateTimeOffset? AsOffset(DateTime? value) =>
        value is { } date
            ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero)
            : null;

    private static bool IsRamadan(DateOnly day)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        var calendar = new UmAlQuraCalendar();
        if (date >= calendar.MinSupportedDateTime && date <= calendar.MaxSupportedDateTime)
            return calendar.GetMonth(date) == 9;
        return new HijriCalendar().GetMonth(date) == 9;
    }

    private static bool IsHoliday(Dictionary<string, object?> holiday, DateOnly day)
    {
        var duration = FeatureDb.Int(holiday, "DurationDays");
        if (duration <= 0)
            return false;

        if (FeatureDb.Date(holiday, "SpecificStartDate") is { } specific &&
            day.DayNumber >= specific.DayNumber &&
            (long)day.DayNumber < (long)specific.DayNumber + duration)
            return true;

        if (!FeatureDb.Bool(holiday, "RepeatsYearly") ||
            !string.Equals(holiday["CalendarType"] as string, "gregorian",
                StringComparison.OrdinalIgnoreCase))
            return false;

        var month = FeatureDb.NullableLong(holiday, "MonthOfYear");
        var dayOfMonth = FeatureDb.NullableLong(holiday, "DayOfMonth");
        if (month is null or < 1 or > 12 || dayOfMonth is null or < 1 or > 31)
            return false;

        var earliestYear = Math.Max(1, day.Year - (duration / 365) - 2);
        for (var year = day.Year; year >= earliestYear; year--)
        {
            if (dayOfMonth.Value > DateTime.DaysInMonth(year, (int)month.Value))
                continue;
            var start = new DateOnly(year, (int)month.Value, (int)dayOfMonth.Value);
            if (day.DayNumber >= start.DayNumber &&
                (long)day.DayNumber < (long)start.DayNumber + duration)
                return true;
        }
        return false;
    }

    private static bool ClosedPeriodOverlaps(
        Dictionary<string, object?> row, DateOnly from, DateOnly to)
    {
        foreach (var pair in new[]
        {
            (Start: "FromDate", End: "ToDate"),
            (Start: "StartDate", End: "EndDate"),
            (Start: "PeriodStart", End: "PeriodEnd"),
            (Start: "PeriodStartDate", End: "PeriodEndDate"),
            (Start: "MonthStartDate", End: "MonthEndDate")
        })
        {
            if (FeatureDb.Date(row, pair.Start) is { } start &&
                FeatureDb.Date(row, pair.End) is { } end)
                return start <= to && end >= from;
        }

        foreach (var pair in new[]
        {
            (Year: "Year", Month: "Month"),
            (Year: "AttendanceYear", Month: "AttendanceMonth"),
            (Year: "YearNo", Month: "MonthNo"),
            (Year: "YearNumber", Month: "MonthNumber")
        })
        {
            var year = FeatureDb.NullableLong(row, pair.Year);
            var month = FeatureDb.NullableLong(row, pair.Month);
            if (year is >= 1 and <= 9999 && month is >= 1 and <= 12)
            {
                var start = new DateOnly((int)year.Value, (int)month.Value, 1);
                var end = new DateOnly(start.Year, start.Month,
                    DateTime.DaysInMonth(start.Year, start.Month));
                return start <= to && end >= from;
            }
        }

        foreach (var name in new[] { "MonthDate", "AttendanceMonthDate", "MonthStart", "MonthStartDate" })
        {
            if (FeatureDb.Date(row, name) is { } date)
            {
                var start = new DateOnly(date.Year, date.Month, 1);
                var end = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                return start <= to && end >= from;
            }
        }

        throw new InvalidOperationException("الشهر مقفل");
    }

    private static ShiftDef ToDefinition(Dictionary<string, object?> shift) => new(
        FeatureDb.Time(shift, "StartTime")!.Value,
        FeatureDb.Time(shift, "EndTime")!.Value,
        FeatureDb.Int(shift, "WorkMinutes"),
        FeatureDb.Int(shift, "BreakMinutes"),
        FeatureDb.Bool(shift, "CrossesMidnight"),
        FeatureDb.Int(shift, "BeforeInMin"),
        FeatureDb.Int(shift, "AfterInMin"),
        FeatureDb.Int(shift, "BeforeOutMin"),
        FeatureDb.Int(shift, "AfterOutMin"),
        FeatureDb.Bool(shift, "LateGraceEnabled"),
        FeatureDb.Int(shift, "LateGraceMin"),
        FeatureDb.Bool(shift, "LateAfterGrace"),
        FeatureDb.Bool(shift, "EarlyGraceEnabled"),
        FeatureDb.Int(shift, "EarlyGraceMin"),
        FeatureDb.Bool(shift, "EarlyBeforeGrace"),
        FeatureDb.Bool(shift, "PresentIfNoOut"),
        FeatureDb.Bool(shift, "PresentIfNoIn"),
        FeatureDb.Bool(shift, "OvertimeEnabled"),
        FeatureDb.Time(shift, "OvertimeFrom"),
        FeatureDb.Time(shift, "OvertimeTo"));

    private sealed record ShiftData(
        ShiftDef Definition,
        IReadOnlyList<Dictionary<string, object?>> Days);
}

public sealed record AttendanceProcessInput(
    DateOnly From,
    DateOnly To,
    long[]? EmployeeIds = null);

public sealed record AttendanceProcessResult(
    int ProcessedDays,
    int Employees,
    int SkippedManual);
=== END FILE ===
=== FILE: src/Hrms.Api/Features/Attendance/AttendanceController.cs ===
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Features.Employees;
using Hrms.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Features.Attendance;

[ApiController]
[Route("api/v1/attendance")]
public sealed class AttendanceController(
    IDbConnectionFactory connectionFactory,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("process")]
    [HasPermission("attendance.daily.process")]
    public async Task<ApiResponse<AttendanceProcessResult>> Process(
        [FromBody] AttendanceProcessInput input)
    {
        var processor = new AttendanceProcessor(connectionFactory, currentUser);
        return new(await processor.ProcessAsync(input));
    }

    [HttpGet("daily")]
    [HasPermission("attendance.daily.read")]
    public async Task<ApiResponse<IReadOnlyList<Dictionary<string, object?>>>> Daily(
        long? employeeId = null, DateOnly? from = null, DateOnly? to = null)
    {
        if (employeeId is <= 0 || (from.HasValue && to.HasValue && from > to))
            throw new ArgumentException("مرشحات البحث غير صالحة");

        using var db = FeatureDb.Open(connectionFactory);
        var rows = await FeatureDb.Query(db, """
            SELECT d.*
            FROM hr.DailyAttendance d
            INNER JOIN hr.Employees e ON e.Id=d.EmployeeId AND e.CompanyId=d.CompanyId
            WHERE d.CompanyId=@CompanyId AND d.IsDeleted=0
              AND e.CompanyId=@CompanyId AND e.IsDeleted=0
              AND (@EmployeeId IS NULL OR d.EmployeeId=@EmployeeId)
              AND (@FromDate IS NULL OR d.WorkDate>=@FromDate)
              AND (@ToDate IS NULL OR d.WorkDate<=@ToDate)
            ORDER BY d.WorkDate,d.EmployeeId,d.Id
            """, FeatureDb.Parameters(currentUser, new
        {
            EmployeeId = employeeId,
            FromDate = from,
            ToDate = to
        }));
        return new(rows.Select(FeatureDb.JsonRow).ToArray());
    }
}
=== END FILE ===