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
