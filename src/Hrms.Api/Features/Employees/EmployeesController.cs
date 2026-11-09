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
