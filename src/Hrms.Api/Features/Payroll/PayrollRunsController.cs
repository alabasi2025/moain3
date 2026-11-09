using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Security;
using Hrms.Domain.Payroll;
using Microsoft.AspNetCore.Mvc;
using PItemType = Hrms.Domain.Payroll.ItemType;
using PCalcMethod = Hrms.Domain.Payroll.CalcMethod;

namespace Hrms.Api.Features.Payroll;

public sealed record CreatePayrollRunRequest(int PeriodYear, int PeriodMonth, string? RunType, long? OrgNodeId, string? Description);

public sealed record PayrollWarning(long EmployeeId, string Reason);

public sealed record CalculatePayrollResult(
    int Employees,
    decimal TotalNet,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal EmployerInsurance,
    IReadOnlyList<PayrollWarning> Warnings);

public sealed record PayrollRunStatusResult(long Id, string Status);

public sealed class PayrollRunDto
{
    public long Id { get; set; }
    public int DocNo { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string RunType { get; set; } = "";
    public string? ScopeJson { get; set; }
    public DateTime IssueDate { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public long? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public int LinesCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
}

public sealed class PayrollRunLineDto
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public long EmployeeId { get; set; }
    public int EmployeeNo { get; set; }
    public string FullNameAr { get; set; } = "";
    public decimal WorkDays { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string? Notes { get; set; }
}

public sealed class PayrollRunLineItemDto
{
    public long Id { get; set; }
    public long LineId { get; set; }
    public long PayrollItemId { get; set; }
    public int Code { get; set; }
    public string NameAr { get; set; } = "";
    public string? SystemKey { get; set; }
    public string ItemType { get; set; } = "";
    public decimal Amount { get; set; }
    public string Source { get; set; } = "";
    public long? SourceRef { get; set; }
}

[ApiController]
[Route("api/v1/payroll-runs")]
public sealed class PayrollRunsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<string, string> SystemCodeToKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OVERTIME"] = "overtime",
        ["ABSENCE"] = "absence",
        ["LATE"] = "late",
        ["ADVANCE"] = "advance",
        ["INSURANCE"] = "insurance",
        ["CUSTODY"] = "custody",
        ["UNPAID_LEAVE"] = "unpaid_leave"
    };

    private const string VariablePrefix = "VAR-";

    private const string RunSelectSql = @"
SELECT r.Id, r.DocNo, r.PeriodYear, r.PeriodMonth, r.RunType, r.ScopeJson, r.IssueDate, r.Description, r.Status,
       r.ApprovedBy, r.ApprovedAt, r.PaidAt,
       ISNULL(t.LinesCount, 0) AS LinesCount,
       ISNULL(t.TotalEarnings, 0) AS TotalEarnings,
       ISNULL(t.TotalDeductions, 0) AS TotalDeductions,
       ISNULL(t.TotalNet, 0) AS TotalNet
FROM hr.PayrollRuns r
OUTER APPLY (
    SELECT COUNT(1) AS LinesCount,
           SUM(l.TotalEarnings) AS TotalEarnings,
           SUM(l.TotalDeductions) AS TotalDeductions,
           SUM(l.NetPay) AS TotalNet
    FROM hr.PayrollRunLines l
    WHERE l.RunId = r.Id AND l.CompanyId = r.CompanyId AND l.IsDeleted = 0
) t
WHERE r.CompanyId = @CompanyId AND r.IsDeleted = 0";

    private const string EmployeesSql = @"
SELECT e.Id, e.DailyHours, e.SocialInsuranceNo, e.SocialEmployeePct
FROM hr.Employees e
WHERE e.CompanyId = @CompanyId AND e.IsDeleted = 0
  AND e.WorkStartDate IS NOT NULL AND e.WorkStartDate <= @LastDay
ORDER BY e.EmployeeNo;";

    private const string EmployeesScopedSql = @"
WITH Tree AS (
    SELECT n.Id FROM hr.OrgNodes n
    WHERE n.Id = @OrgNodeId AND n.CompanyId = @CompanyId AND n.IsDeleted = 0
    UNION ALL
    SELECT c.Id FROM hr.OrgNodes c
    INNER JOIN Tree t ON c.ParentId = t.Id
    WHERE c.CompanyId = @CompanyId AND c.IsDeleted = 0
)
SELECT e.Id, e.DailyHours, e.SocialInsuranceNo, e.SocialEmployeePct
FROM hr.Employees e
WHERE e.CompanyId = @CompanyId AND e.IsDeleted = 0
  AND e.WorkStartDate IS NOT NULL AND e.WorkStartDate <= @LastDay
  AND e.OrgNodeId IN (SELECT Id FROM Tree)
ORDER BY e.EmployeeNo
OPTION (MAXRECURSION 200);";

    private const string FixedItemsSql = @"
SELECT esi.EmployeeId, esi.PayrollItemId, pi.Code, pi.NameAr, pi.ItemType, pi.CalcMethod, pi.IsBasic,
       pi.SubjectToInsurance, esi.Amount, esi.Percentage, CAST(0 AS bigint) AS SourceId
FROM hr.EmployeeSalaryItems esi
INNER JOIN hr.PayrollItems pi ON pi.Id = esi.PayrollItemId AND pi.CompanyId = @CompanyId AND pi.IsDeleted = 0
WHERE esi.CompanyId = @CompanyId AND esi.IsDeleted = 0
  AND pi.SystemKey IS NULL AND pi.IsActive = 1;";

    private const string VariableItemsSql = @"
SELECT v.EmployeeId, v.PayrollItemId, pi.Code, pi.NameAr, pi.ItemType, pi.CalcMethod, pi.IsBasic,
       pi.SubjectToInsurance, v.Amount, v.Percentage, v.Id AS SourceId
FROM hr.VariableItems v
INNER JOIN hr.PayrollItems pi ON pi.Id = v.PayrollItemId AND pi.CompanyId = @CompanyId AND pi.IsDeleted = 0
WHERE v.CompanyId = @CompanyId AND v.IsDeleted = 0 AND v.IsApplied = 0
  AND v.PeriodYear = @Year AND v.PeriodMonth = @Month;";

    private const string AttendanceSql = @"
SELECT d.EmployeeId,
       SUM(CASE WHEN d.Status = 'absent' THEN 1 ELSE 0 END) AS AbsentDays,
       SUM(CASE WHEN d.Status IN ('present','excused') THEN d.AbsentMin ELSE 0 END) AS AbsentMinutesPartial,
       SUM(d.LateMin) AS LateMinutes,
       SUM(d.EarlyLeaveMin) AS EarlyLeaveMinutes,
       SUM(CASE WHEN d.Status = 'present' AND d.RequiredMinutes > 0 THEN d.OvertimeMin ELSE 0 END) AS OvertimeNormal,
       SUM(CASE WHEN d.Status = 'holiday_work' OR (d.Status = 'present' AND d.RequiredMinutes = 0) THEN d.OvertimeMin ELSE 0 END) AS OvertimeSpecial
FROM hr.DailyAttendance d
WHERE d.CompanyId = @CompanyId AND d.IsDeleted = 0
  AND d.WorkDate >= @FirstDay AND d.WorkDate <= @LastDay
GROUP BY d.EmployeeId;";

    private const string AdvancesSql = @"
SELECT a.EmployeeId, SUM(ai.Amount - ai.PaidAmount) AS Amount
FROM hr.AdvanceInstallments ai
INNER JOIN hr.Advances a ON a.Id = ai.AdvanceId AND a.CompanyId = @CompanyId AND a.IsDeleted = 0
WHERE ai.CompanyId = @CompanyId AND ai.IsDeleted = 0 AND ai.Status = 'pending'
  AND ai.PeriodYear = @Year AND ai.PeriodMonth = @Month
GROUP BY a.EmployeeId;";

    private readonly IDbConnectionFactory _db;
    private readonly ICurrentUser _user;

    public PayrollRunsController(IDbConnectionFactory db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    [HasPermission("finance.payroll_runs.read")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollRunDto>>>> List([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        if (month is not null && (month < 1 || month > 12))
            throw new ArgumentException("الشهر يجب أن يكون بين 1 و 12");

        using var conn = await OpenAsync(ct);
        var sql = RunSelectSql + @"
  AND (@Year IS NULL OR r.PeriodYear = @Year)
  AND (@Month IS NULL OR r.PeriodMonth = @Month)
ORDER BY r.PeriodYear DESC, r.PeriodMonth DESC, r.DocNo DESC;";
        var rows = (await conn.QueryAsync<PayrollRunDto>(sql, new { CompanyId = _user.CompanyId, Year = year, Month = month })).ToList();
        return Ok(new ApiResponse<IReadOnlyList<PayrollRunDto>>(rows));
    }

    [HttpGet("{id:long}")]
    [HasPermission("finance.payroll_runs.read")]
    public async Task<ActionResult<ApiResponse<PayrollRunDto>>> GetById(long id, CancellationToken ct)
    {
        using var conn = await OpenAsync(ct);
        var dto = await LoadRunAsync(conn, null, id);
        return Ok(new ApiResponse<PayrollRunDto>(dto));
    }

    [HttpPost]
    [HasPermission("finance.payroll_runs.create")]
    public async Task<ActionResult<ApiResponse<PayrollRunDto>>> Create([FromBody] CreatePayrollRunRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentException("بيانات الطلب مطلوبة");
        if (request.PeriodYear < 2000 || request.PeriodYear > 2100)
            throw new ArgumentException("سنة الفترة غير صالحة");
        if (request.PeriodMonth < 1 || request.PeriodMonth > 12)
            throw new ArgumentException("الشهر يجب أن يكون بين 1 و 12");

        var runType = string.IsNullOrWhiteSpace(request.RunType) ? "standard" : request.RunType.Trim().ToLowerInvariant();
        if (runType != "standard" && runType != "custom")
            throw new ArgumentException("نوع الكشف يجب أن يكون standard أو custom");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description is not null && description.Length > 500)
            throw new ArgumentException("الوصف يجب ألا يتجاوز 500 حرف");

        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        string? scopeJson = null;
        long? orgNodeId = null;
        if (runType == "custom")
        {
            if (request.OrgNodeId is null || request.OrgNodeId <= 0)
                throw new ArgumentException("الوحدة التنظيمية إلزامية للكشف المخصص");

            var nodeExists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM hr.OrgNodes WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
                new { Id = request.OrgNodeId.Value, CompanyId = companyId }, tx);
            if (nodeExists == 0)
                throw new ArgumentException("الوحدة التنظيمية غير موجودة");

            orgNodeId = request.OrgNodeId.Value;
            scopeJson = JsonSerializer.Serialize(new { orgNodeId = orgNodeId.Value }, JsonOpts);
        }
        else
        {
            var dup = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM hr.PayrollRuns WITH (UPDLOCK, HOLDLOCK)
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND RunType = 'standard'
  AND PeriodYear = @Year AND PeriodMonth = @Month;",
                new { CompanyId = companyId, Year = request.PeriodYear, Month = request.PeriodMonth }, tx);
            if (dup > 0)
                throw new InvalidOperationException("يوجد كشف نمطي لهذه الفترة");
        }

        var docNo = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(DocNo), 0) + 1 FROM hr.PayrollRuns WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = @CompanyId;",
            new { CompanyId = companyId }, tx);

        var issueDate = DateTime.Today;
        var newId = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO hr.PayrollRuns (CompanyId, DocNo, PeriodYear, PeriodMonth, RunType, ScopeJson, IssueDate, Description, Status)
OUTPUT INSERTED.Id
VALUES (@CompanyId, @DocNo, @PeriodYear, @PeriodMonth, @RunType, @ScopeJson, @IssueDate, @Description, 'draft');",
            new
            {
                CompanyId = companyId,
                DocNo = docNo,
                PeriodYear = request.PeriodYear,
                PeriodMonth = request.PeriodMonth,
                RunType = runType,
                ScopeJson = scopeJson,
                IssueDate = issueDate,
                Description = description
            }, tx);

        await AuditAsync(conn, tx, newId, "create", new
        {
            docNo,
            periodYear = request.PeriodYear,
            periodMonth = request.PeriodMonth,
            runType,
            orgNodeId,
            issueDate = issueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            description,
            status = "draft"
        });

        tx.Commit();

        var dto = await LoadRunAsync(conn, null, newId);
        return CreatedAtAction(nameof(GetById), new { id = newId }, new ApiResponse<PayrollRunDto>(dto));
    }

    [HttpDelete("{id:long}")]
    [HasPermission("finance.payroll_runs.delete")]
    public async Task<ActionResult<ApiResponse<PayrollRunStatusResult>>> Delete(long id, CancellationToken ct)
    {
        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var run = await LockRunAsync(conn, tx, id);
        if (run.Status != "draft")
            throw new InvalidOperationException("لا يمكن حذف الكشف إلا في حالة المسودة");

        var p = new { CompanyId = companyId, UserId = userId, RunId = id };
        await conn.ExecuteAsync(SoftDeleteLineItemsSql, p, tx);
        await conn.ExecuteAsync(SoftDeleteLinesSql, p, tx);
        await conn.ExecuteAsync(@"
UPDATE hr.PayrollRuns SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;", p, tx);

        await AuditAsync(conn, tx, id, "delete", new { action = "delete", isDeleted = true });

        tx.Commit();
        return Ok(new ApiResponse<PayrollRunStatusResult>(new PayrollRunStatusResult(id, "deleted")));
    }

    [HttpPost("{id:long}/calculate")]
    [HasPermission("finance.payroll_runs.process")]
    public async Task<ActionResult<ApiResponse<CalculatePayrollResult>>> Calculate(long id, CancellationToken ct)
    {
        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var run = await LockRunAsync(conn, tx, id);
        if (run.Status != "draft")
            throw new InvalidOperationException("لا يمكن احتساب الكشف إلا في حالة المسودة");

        var p = new { CompanyId = companyId, UserId = userId, RunId = id };
        await conn.ExecuteAsync(SoftDeleteLineItemsSql, p, tx);
        await conn.ExecuteAsync(SoftDeleteLinesSql, p, tx);

        var firstDay = new DateTime(run.PeriodYear, run.PeriodMonth, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        long? scopeNode = null;
        if (run.RunType == "custom")
        {
            scopeNode = ParseScopeOrgNode(run.ScopeJson)
                ?? throw new InvalidOperationException("نطاق الكشف المخصص غير صالح");
        }

        var employees = (await conn.QueryAsync<EmpRow>(
            scopeNode is null ? EmployeesSql : EmployeesScopedSql,
            new { CompanyId = companyId, LastDay = lastDay, OrgNodeId = scopeNode }, tx)).ToList();

        var periodParams = new { CompanyId = companyId, Year = run.PeriodYear, Month = run.PeriodMonth, FirstDay = firstDay, LastDay = lastDay };

        var fixedByEmp = (await conn.QueryAsync<SalaryRow>(FixedItemsSql, periodParams, tx))
            .GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var variableByEmp = (await conn.QueryAsync<SalaryRow>(VariableItemsSql, periodParams, tx))
            .GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
        var attendanceByEmp = (await conn.QueryAsync<AttRow>(AttendanceSql, periodParams, tx))
            .ToDictionary(r => r.EmployeeId);
        var advanceByEmp = (await conn.QueryAsync<AdvRow>(AdvancesSql, periodParams, tx))
            .ToDictionary(r => r.EmployeeId, r => r.Amount);

        var settingKeys = new[]
        {
            "insurance.employee_pct", "insurance.employer_pct", "overtime.rate_normal",
            "overtime.rate_night_rest_holiday", "labor.month_days"
        };
        var settingsMap = (await conn.QueryAsync<SettingRow>(@"
SELECT SettingKey, SettingValue FROM hr.Settings
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND SettingKey IN @Keys;",
                new { CompanyId = companyId, Keys = settingKeys }, tx))
            .GroupBy(s => s.SettingKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SettingValue, StringComparer.OrdinalIgnoreCase);

        var defaults = new PayrollSettings();
        var monthDays = GetInt(settingsMap, "labor.month_days", defaults.MonthDays);
        if (monthDays <= 0) monthDays = defaults.MonthDays;
        var baseSettings = defaults with
        {
            MonthDays = monthDays,
            OvertimeRateNormal = GetDecimal(settingsMap, "overtime.rate_normal", defaults.OvertimeRateNormal),
            OvertimeRateSpecial = GetDecimal(settingsMap, "overtime.rate_night_rest_holiday", defaults.OvertimeRateSpecial),
            InsuranceEmployeePct = GetDecimal(settingsMap, "insurance.employee_pct", defaults.InsuranceEmployeePct),
            InsuranceEmployerPct = GetDecimal(settingsMap, "insurance.employer_pct", defaults.InsuranceEmployerPct)
        };

        var systemItems = (await conn.QueryAsync<SystemItemRow>(@"
SELECT Id, SystemKey FROM hr.PayrollItems
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND SystemKey IS NOT NULL
ORDER BY IsActive DESC, Id;", new { CompanyId = companyId }, tx))
            .GroupBy(s => s.SystemKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var warnings = new List<PayrollWarning>();
        int employeesCount = 0;
        decimal totalNet = 0m, totalEarnings = 0m, totalDeductions = 0m, employerInsurance = 0m;

        foreach (var emp in employees)
        {
            ct.ThrowIfCancellationRequested();

            var fixedRows = fixedByEmp.TryGetValue(emp.Id, out var fr) ? fr : new List<SalaryRow>();
            var variableRows = variableByEmp.TryGetValue(emp.Id, out var vr) ? vr : new List<SalaryRow>();

            if (!fixedRows.Any(r => r.IsBasic))
            {
                warnings.Add(new PayrollWarning(emp.Id, "لا يوجد بند راتب أساسي"));
                continue;
            }

            var fixedCodeMap = new Dictionary<string, long>(StringComparer.Ordinal);
            var fixedItems = new List<SalaryItem>(fixedRows.Count);
            foreach (var r in fixedRows)
            {
                var code = r.Code.ToString(CultureInfo.InvariantCulture);
                fixedCodeMap.TryAdd(code, r.PayrollItemId);
                fixedItems.Add(new SalaryItem(code, r.NameAr, MapItemType(r.ItemType), MapCalcMethod(r.CalcMethod),
                    r.Amount, r.Percentage, r.SubjectToInsurance, r.IsBasic));
            }

            var variableCodeMap = new Dictionary<string, (long PayrollItemId, long SourceId)>(StringComparer.Ordinal);
            var variableItems = new List<SalaryItem>(variableRows.Count);
            foreach (var r in variableRows)
            {
                var code = VariablePrefix + r.SourceId.ToString(CultureInfo.InvariantCulture);
                variableCodeMap[code] = (r.PayrollItemId, r.SourceId);
                variableItems.Add(new SalaryItem(code, r.NameAr, MapItemType(r.ItemType), MapCalcMethod(r.CalcMethod),
                    r.Amount, r.Percentage, r.SubjectToInsurance, r.IsBasic));
            }

            var att = attendanceByEmp.TryGetValue(emp.Id, out var a) ? a : new AttRow { EmployeeId = emp.Id };
            var attendance = new AttendanceTotals(
                att.AbsentDays,
                att.AbsentMinutesPartial,
                att.LateMinutes,
                att.EarlyLeaveMinutes,
                att.OvertimeNormal,
                att.OvertimeSpecial,
                0m);

            var advance = advanceByEmp.TryGetValue(emp.Id, out var adv) ? adv : 0m;
            if (advance < 0m) advance = 0m;

            var dailyHours = emp.DailyHours is null or <= 0m ? 8m : emp.DailyHours.Value;

            var empSettings = baseSettings with
            {
                DeductInsurance = emp.SocialInsuranceNo is not null || emp.SocialEmployeePct > 0m,
                InsuranceEmployeePct = emp.SocialEmployeePct > 0m ? emp.SocialEmployeePct : baseSettings.InsuranceEmployeePct
            };

            var input = new PayrollInput(emp.Id, dailyHours, fixedItems, variableItems, attendance, advance, 0m, empSettings);

            PayrollResult result;
            try
            {
                result = PayrollCalculator.Calculate(input);
            }
            catch (ArgumentException ex)
            {
                warnings.Add(new PayrollWarning(emp.Id, string.IsNullOrWhiteSpace(ex.Message) ? "لا يوجد بند راتب أساسي" : ex.Message));
                continue;
            }
            catch (InvalidOperationException ex)
            {
                warnings.Add(new PayrollWarning(emp.Id, string.IsNullOrWhiteSpace(ex.Message) ? "صافي الراتب سالب" : ex.Message));
                continue;
            }

            var workDays = Math.Max(0m, empSettings.MonthDays - attendance.AbsentDays);

            var lineId = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO hr.PayrollRunLines (CompanyId, RunId, EmployeeId, WorkDays, BasicSalary, TotalEarnings, TotalDeductions, NetPay)
OUTPUT INSERTED.Id
VALUES (@CompanyId, @RunId, @EmployeeId, @WorkDays, @BasicSalary, @TotalEarnings, @TotalDeductions, @NetPay);",
                new
                {
                    CompanyId = companyId,
                    RunId = id,
                    EmployeeId = emp.Id,
                    WorkDays = workDays,
                    BasicSalary = result.BasicSalary,
                    TotalEarnings = result.TotalEarnings,
                    TotalDeductions = result.TotalDeductions,
                    NetPay = result.NetPay
                }, tx);

            var inserts = new List<LineItemInsert>(result.Items.Count);
            foreach (var item in result.Items)
            {
                var itemType = item.Type == PItemType.Earning ? "earning" : "deduction";
                if (variableCodeMap.TryGetValue(item.Code, out var vInfo))
                {
                    inserts.Add(new LineItemInsert(companyId, lineId, vInfo.PayrollItemId, itemType, item.Amount, "variable", vInfo.SourceId));
                }
                else if (fixedCodeMap.TryGetValue(item.Code, out var fixedItemId))
                {
                    inserts.Add(new LineItemInsert(companyId, lineId, fixedItemId, itemType, item.Amount, "fixed", null));
                }
                else if (SystemCodeToKey.TryGetValue(item.Code, out var sysKey) && systemItems.TryGetValue(sysKey, out var sysItemId))
                {
                    inserts.Add(new LineItemInsert(companyId, lineId, sysItemId, itemType, item.Amount, "system", null));
                }
                else
                {
                    warnings.Add(new PayrollWarning(emp.Id, $"لا يوجد بند رواتب مطابق للكود {item.Code} ({item.NameAr})؛ تم تخطي السطر"));
                }
            }

            if (inserts.Count > 0)
            {
                await conn.ExecuteAsync(@"
INSERT INTO hr.PayrollRunLineItems (CompanyId, LineId, PayrollItemId, ItemType, Amount, Source, SourceRef)
VALUES (@CompanyId, @LineId, @PayrollItemId, @ItemType, @Amount, @Source, @SourceRef);", inserts, tx);
            }

            employeesCount++;
            totalNet += result.NetPay;
            totalEarnings += result.TotalEarnings;
            totalDeductions += result.TotalDeductions;
            employerInsurance += result.EmployerInsurance;
        }

        await conn.ExecuteAsync(@"
UPDATE hr.PayrollRuns SET UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;", p, tx);

        await AuditAsync(conn, tx, id, "update", new
        {
            action = "calculate",
            employees = employeesCount,
            totalNet,
            totalEarnings,
            totalDeductions,
            employerInsurance,
            warnings = warnings.Count
        });

        tx.Commit();

        return Ok(new ApiResponse<CalculatePayrollResult>(new CalculatePayrollResult(
            employeesCount, totalNet, totalEarnings, totalDeductions, employerInsurance, warnings)));
    }

    [HttpGet("{id:long}/lines")]
    [HasPermission("finance.payroll_runs.read")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollRunLineDto>>>> GetLines(long id, CancellationToken ct)
    {
        using var conn = await OpenAsync(ct);
        await EnsureRunExistsAsync(conn, id);

        var rows = (await conn.QueryAsync<PayrollRunLineDto>(@"
SELECT l.Id, l.RunId, l.EmployeeId, e.EmployeeNo, e.FullNameAr, l.WorkDays, l.BasicSalary,
       l.TotalEarnings, l.TotalDeductions, l.NetPay, l.Notes
FROM hr.PayrollRunLines l
INNER JOIN hr.Employees e ON e.Id = l.EmployeeId AND e.CompanyId = @CompanyId
WHERE l.RunId = @RunId AND l.CompanyId = @CompanyId AND l.IsDeleted = 0
ORDER BY e.EmployeeNo;", new { CompanyId = _user.CompanyId, RunId = id })).ToList();

        return Ok(new ApiResponse<IReadOnlyList<PayrollRunLineDto>>(rows));
    }

    [HttpGet("{id:long}/lines/{lineId:long}/items")]
    [HasPermission("finance.payroll_runs.read")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollRunLineItemDto>>>> GetLineItems(long id, long lineId, CancellationToken ct)
    {
        using var conn = await OpenAsync(ct);
        await EnsureRunExistsAsync(conn, id);

        var lineExists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM hr.PayrollRunLines
WHERE Id = @LineId AND RunId = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { LineId = lineId, RunId = id, CompanyId = _user.CompanyId });
        if (lineExists == 0)
            throw new KeyNotFoundException("سطر الكشف غير موجود");

        var rows = (await conn.QueryAsync<PayrollRunLineItemDto>(@"
SELECT li.Id, li.LineId, li.PayrollItemId, pi.Code, pi.NameAr, pi.SystemKey, li.ItemType, li.Amount, li.Source, li.SourceRef
FROM hr.PayrollRunLineItems li
INNER JOIN hr.PayrollItems pi ON pi.Id = li.PayrollItemId AND pi.CompanyId = @CompanyId
WHERE li.LineId = @LineId AND li.CompanyId = @CompanyId AND li.IsDeleted = 0
ORDER BY CASE WHEN li.ItemType = 'earning' THEN 0 ELSE 1 END, li.Id;",
            new { LineId = lineId, CompanyId = _user.CompanyId })).ToList();

        return Ok(new ApiResponse<IReadOnlyList<PayrollRunLineItemDto>>(rows));
    }

    [HttpPost("{id:long}/approve")]
    [HasPermission("finance.payroll_runs.approve")]
    public async Task<ActionResult<ApiResponse<PayrollRunDto>>> Approve(long id, CancellationToken ct)
    {
        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var run = await LockRunAsync(conn, tx, id);
        if (run.Status != "draft" && run.Status != "review")
            throw new InvalidOperationException("لا يمكن اعتماد الكشف إلا في حالة المسودة أو المراجعة");

        var linesCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM hr.PayrollRunLines
WHERE RunId = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { RunId = id, CompanyId = companyId }, tx);
        if (linesCount == 0)
            throw new InvalidOperationException("لا يمكن اعتماد كشف لا يحتوي على أسطر");

        var installments = (await conn.QueryAsync<InstallmentRow>(@"
SELECT ai.Id, ai.AdvanceId, ai.Amount, ai.PaidAmount, l.Id AS LineId
FROM hr.AdvanceInstallments ai WITH (UPDLOCK, ROWLOCK)
INNER JOIN hr.Advances a ON a.Id = ai.AdvanceId AND a.CompanyId = @CompanyId AND a.IsDeleted = 0
INNER JOIN hr.PayrollRunLines l ON l.EmployeeId = a.EmployeeId AND l.RunId = @RunId
                                AND l.CompanyId = @CompanyId AND l.IsDeleted = 0
WHERE ai.CompanyId = @CompanyId AND ai.IsDeleted = 0 AND ai.Status = 'pending'
  AND ai.PeriodYear = @Year AND ai.PeriodMonth = @Month;",
            new { CompanyId = companyId, RunId = id, Year = run.PeriodYear, Month = run.PeriodMonth }, tx)).ToList();

        if (installments.Count > 0)
        {
            await conn.ExecuteAsync(@"
UPDATE hr.Advances
SET RemainingAmount = RemainingAmount - @Delta, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @AdvanceId AND CompanyId = @CompanyId AND IsDeleted = 0;",
                installments.Select(i => new AdvanceDelta(i.AdvanceId, i.Amount - i.PaidAmount, companyId, userId)).ToList(), tx);

            await conn.ExecuteAsync(@"
UPDATE hr.AdvanceInstallments
SET Status = 'deducted', PaidAmount = Amount, PayrollRunLineId = @LineId,
    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
                installments.Select(i => new InstallmentLink(i.Id, i.LineId, companyId, userId)).ToList(), tx);
        }

        var variableCount = await conn.ExecuteAsync(@"
UPDATE v
SET v.IsApplied = 1, v.PayrollRunId = @RunId, v.UpdatedAt = SYSDATETIMEOFFSET(), v.UpdatedBy = @UserId
FROM hr.VariableItems v
WHERE v.CompanyId = @CompanyId AND v.IsDeleted = 0 AND v.IsApplied = 0
  AND v.PeriodYear = @Year AND v.PeriodMonth = @Month
  AND v.Id IN (
      SELECT li.SourceRef
      FROM hr.PayrollRunLineItems li
      INNER JOIN hr.PayrollRunLines l ON l.Id = li.LineId
      WHERE l.RunId = @RunId AND l.CompanyId = @CompanyId AND l.IsDeleted = 0
        AND li.CompanyId = @CompanyId AND li.IsDeleted = 0
        AND li.Source = 'variable' AND li.SourceRef IS NOT NULL
  );",
            new { CompanyId = companyId, UserId = userId, RunId = id, Year = run.PeriodYear, Month = run.PeriodMonth }, tx);

        await conn.ExecuteAsync(@"
UPDATE hr.PayrollRuns
SET Status = 'approved', ApprovedBy = @UserId, ApprovedAt = SYSDATETIMEOFFSET(),
    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { CompanyId = companyId, UserId = userId, RunId = id }, tx);

        await AuditAsync(conn, tx, id, "update", new
        {
            action = "approve",
            previousStatus = run.Status,
            status = "approved",
            approvedBy = userId,
            variableItemsApplied = variableCount,
            installments = installments.Select(i => new { id = i.Id, advanceId = i.AdvanceId, prevPaid = i.PaidAmount, lineId = i.LineId }).ToList()
        });

        tx.Commit();

        var dto = await LoadRunAsync(conn, null, id);
        return Ok(new ApiResponse<PayrollRunDto>(dto));
    }

    [HttpPost("{id:long}/unapprove")]
    [HasPermission("finance.payroll_runs.approve")]
    public async Task<ActionResult<ApiResponse<PayrollRunDto>>> Unapprove(long id, CancellationToken ct)
    {
        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var run = await LockRunAsync(conn, tx, id);
        if (run.Status == "paid")
            throw new InvalidOperationException("لا يمكن إلغاء اعتماد كشف مدفوع");
        if (run.Status != "approved")
            throw new InvalidOperationException("لا يمكن إلغاء الاعتماد إلا لكشف معتمد");

        var installments = (await conn.QueryAsync<InstallmentRow>(@"
SELECT ai.Id, ai.AdvanceId, ai.Amount, ai.PaidAmount, ai.PayrollRunLineId AS LineId
FROM hr.AdvanceInstallments ai WITH (UPDLOCK, ROWLOCK)
INNER JOIN hr.PayrollRunLines l ON l.Id = ai.PayrollRunLineId AND l.RunId = @RunId AND l.CompanyId = @CompanyId
WHERE ai.CompanyId = @CompanyId AND ai.IsDeleted = 0 AND ai.Status = 'deducted';",
            new { CompanyId = companyId, RunId = id }, tx)).ToList();

        var prevPaidMap = new Dictionary<long, decimal>();
        var snapshot = await conn.ExecuteScalarAsync<string?>(@"
SELECT TOP (1) NewValues FROM hr.AuditLogs
WHERE CompanyId = @CompanyId AND EntityName = 'PayrollRun' AND EntityId = @RunId
  AND ActionType = 'update' AND NewValues LIKE @Pattern
ORDER BY Id DESC;",
            new { CompanyId = companyId, RunId = id, Pattern = "%\"action\":\"approve\"%" }, tx);
        if (!string.IsNullOrWhiteSpace(snapshot))
        {
            try
            {
                using var doc = JsonDocument.Parse(snapshot);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("installments", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        if (el.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var instId) &&
                            el.TryGetProperty("prevPaid", out var ppEl) && ppEl.TryGetDecimal(out var prevPaid))
                        {
                            prevPaidMap[instId] = prevPaid;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                prevPaidMap.Clear();
            }
        }

        if (installments.Count > 0)
        {
            var reversals = installments.Select(i =>
            {
                var prevPaid = prevPaidMap.TryGetValue(i.Id, out var pp) ? pp : 0m;
                return new { Inst = i, PrevPaid = prevPaid, Delta = i.Amount - prevPaid };
            }).ToList();

            await conn.ExecuteAsync(@"
UPDATE hr.Advances
SET RemainingAmount = RemainingAmount + @Delta, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @AdvanceId AND CompanyId = @CompanyId AND IsDeleted = 0;",
                reversals.Select(r => new AdvanceDelta(r.Inst.AdvanceId, r.Delta, companyId, userId)).ToList(), tx);

            await conn.ExecuteAsync(@"
UPDATE hr.AdvanceInstallments
SET Status = 'pending', PaidAmount = @PrevPaid, PayrollRunLineId = NULL,
    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
                reversals.Select(r => new InstallmentReset(r.Inst.Id, r.PrevPaid, companyId, userId)).ToList(), tx);
        }

        var variableCount = await conn.ExecuteAsync(@"
UPDATE hr.VariableItems
SET IsApplied = 0, PayrollRunId = NULL, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE CompanyId = @CompanyId AND IsDeleted = 0 AND PayrollRunId = @RunId;",
            new { CompanyId = companyId, UserId = userId, RunId = id }, tx);

        await conn.ExecuteAsync(@"
UPDATE hr.PayrollRuns
SET Status = 'draft', ApprovedBy = NULL, ApprovedAt = NULL,
    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { CompanyId = companyId, UserId = userId, RunId = id }, tx);

        await AuditAsync(conn, tx, id, "update", new
        {
            action = "unapprove",
            previousStatus = "approved",
            status = "draft",
            variableItemsReverted = variableCount,
            installmentsReverted = installments.Select(i => i.Id).ToList()
        });

        tx.Commit();

        var dto = await LoadRunAsync(conn, null, id);
        return Ok(new ApiResponse<PayrollRunDto>(dto));
    }

    [HttpPost("{id:long}/pay")]
    [HasPermission("finance.payroll_runs.process")]
    public async Task<ActionResult<ApiResponse<PayrollRunDto>>> Pay(long id, CancellationToken ct)
    {
        var companyId = _user.CompanyId;
        var userId = _user.UserId;

        using var conn = await OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var run = await LockRunAsync(conn, tx, id);
        if (run.Status != "approved")
            throw new InvalidOperationException("لا يمكن صرف الكشف إلا بعد اعتماده");

        await conn.ExecuteAsync(@"
UPDATE hr.PayrollRuns
SET Status = 'paid', PaidAt = SYSDATETIMEOFFSET(), UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE Id = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { CompanyId = companyId, UserId = userId, RunId = id }, tx);

        await AuditAsync(conn, tx, id, "update", new { action = "pay", previousStatus = "approved", status = "paid" });

        tx.Commit();

        var dto = await LoadRunAsync(conn, null, id);
        return Ok(new ApiResponse<PayrollRunDto>(dto));
    }

    private const string SoftDeleteLineItemsSql = @"
UPDATE li SET li.IsDeleted = 1, li.UpdatedAt = SYSDATETIMEOFFSET(), li.UpdatedBy = @UserId
FROM hr.PayrollRunLineItems li
INNER JOIN hr.PayrollRunLines l ON l.Id = li.LineId
WHERE l.RunId = @RunId AND l.CompanyId = @CompanyId AND l.IsDeleted = 0
  AND li.CompanyId = @CompanyId AND li.IsDeleted = 0;";

    private const string SoftDeleteLinesSql = @"
UPDATE hr.PayrollRunLines SET IsDeleted = 1, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedBy = @UserId
WHERE RunId = @RunId AND CompanyId = @CompanyId AND IsDeleted = 0;";

    private async Task<IDbConnection> OpenAsync(CancellationToken ct)
    {
        var conn = _db.Create();
        if (conn is DbConnection dbConn)
            await dbConn.OpenAsync(ct);
        else
            conn.Open();
        return conn;
    }

    private async Task<PayrollRunDto> LoadRunAsync(IDbConnection conn, IDbTransaction? tx, long id)
    {
        var dto = await conn.QuerySingleOrDefaultAsync<PayrollRunDto>(
            RunSelectSql + " AND r.Id = @Id;",
            new { CompanyId = _user.CompanyId, Id = id }, tx);
        return dto ?? throw new KeyNotFoundException("كشف الرواتب غير موجود");
    }

    private async Task<RunState> LockRunAsync(IDbConnection conn, IDbTransaction tx, long id)
    {
        var run = await conn.QuerySingleOrDefaultAsync<RunState>(@"
SELECT Id, PeriodYear, PeriodMonth, RunType, ScopeJson, Status
FROM hr.PayrollRuns WITH (UPDLOCK, ROWLOCK)
WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, CompanyId = _user.CompanyId }, tx);
        return run ?? throw new KeyNotFoundException("كشف الرواتب غير موجود");
    }

    private async Task EnsureRunExistsAsync(IDbConnection conn, long id)
    {
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM hr.PayrollRuns WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = 0;",
            new { Id = id, CompanyId = _user.CompanyId });
        if (exists == 0)
            throw new KeyNotFoundException("كشف الرواتب غير موجود");
    }

    private async Task AuditAsync(IDbConnection conn, IDbTransaction tx, long entityId, string actionType, object values)
    {
        var json = JsonSerializer.Serialize(values, JsonOpts);
        await conn.ExecuteAsync(@"
INSERT INTO hr.AuditLogs (CompanyId, UserId, EntityName, EntityId, ActionType, NewValues, IpAddress)
VALUES (@CompanyId, @UserId, @EntityName, @EntityId, @ActionType, @NewValues, @IpAddress);",
            new
            {
                CompanyId = _user.CompanyId,
                UserId = _user.UserId,
                EntityName = "PayrollRun",
                EntityId = entityId,
                ActionType = actionType,
                NewValues = json,
                IpAddress = _user.Ip
            }, tx);
    }

    private static long? ParseScopeOrgNode(string? scopeJson)
    {
        if (string.IsNullOrWhiteSpace(scopeJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(scopeJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "orgNodeId", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var n)) return n;
                    if (prop.Value.ValueKind == JsonValueKind.String &&
                        long.TryParse(prop.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PItemType MapItemType(string value) =>
        string.Equals(value, "earning", StringComparison.OrdinalIgnoreCase) ? PItemType.Earning : PItemType.Deduction;

    private static PCalcMethod MapCalcMethod(string value) => value?.ToLowerInvariant() switch
    {
        "pct_basic" => PCalcMethod.PctBasic,
        "pct_gross" => PCalcMethod.PctGross,
        _ => PCalcMethod.Fixed
    };

    private static decimal GetDecimal(Dictionary<string, string?> map, string key, decimal fallback)
    {
        if (map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) &&
            decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;
        return fallback;
    }

    private static int GetInt(Dictionary<string, string?> map, string key, int fallback)
    {
        if (map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            if (decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return (int)d;
        }
        return fallback;
    }

    private sealed class RunState
    {
        public long Id { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string RunType { get; set; } = "";
        public string? ScopeJson { get; set; }
        public string Status { get; set; } = "";
    }

    private sealed class EmpRow
    {
        public long Id { get; set; }
        public decimal? DailyHours { get; set; }
        public string? SocialInsuranceNo { get; set; }
        public decimal SocialEmployeePct { get; set; }
    }

    private sealed class SalaryRow
    {
        public long EmployeeId { get; set; }
        public long PayrollItemId { get; set; }
        public int Code { get; set; }
        public string NameAr { get; set; } = "";
        public string ItemType { get; set; } = "";
        public string CalcMethod { get; set; } = "";
        public bool IsBasic { get; set; }
        public bool SubjectToInsurance { get; set; }
        public decimal Amount { get; set; }
        public decimal? Percentage { get; set; }
        public long SourceId { get; set; }
    }

    private sealed class AttRow
    {
        public long EmployeeId { get; set; }
        public int AbsentDays { get; set; }
        public int AbsentMinutesPartial { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public int OvertimeNormal { get; set; }
        public int OvertimeSpecial { get; set; }
    }

    private sealed class AdvRow
    {
        public long EmployeeId { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class SettingRow
    {
        public string SettingKey { get; set; } = "";
        public string? SettingValue { get; set; }
    }

    private sealed class SystemItemRow
    {
        public long Id { get; set; }
        public string SystemKey { get; set; } = "";
    }

    private sealed class InstallmentRow
    {
        public long Id { get; set; }
        public long AdvanceId { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public long LineId { get; set; }
    }

    private sealed record LineItemInsert(long CompanyId, long LineId, long PayrollItemId, string ItemType, decimal Amount, string Source, long? SourceRef);

    private sealed record AdvanceDelta(long AdvanceId, decimal Delta, long CompanyId, long UserId);

    private sealed record InstallmentLink(long Id, long LineId, long CompanyId, long UserId);

    private sealed record InstallmentReset(long Id, decimal PrevPaid, long CompanyId, long UserId);
}
