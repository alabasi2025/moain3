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
