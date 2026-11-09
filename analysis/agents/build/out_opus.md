=== FILE: src/Hrms.Domain/Attendance/AttendanceModels.cs ===
namespace Hrms.Domain.Attendance;

public sealed record ShiftDef(TimeOnly Start, TimeOnly End, int WorkMinutes, int BreakMinutes, bool CrossesMidnight,
    int BeforeInMin, int AfterInMin, int BeforeOutMin, int AfterOutMin,
    bool LateGraceEnabled, int LateGraceMin, bool LateAfterGrace,
    bool EarlyGraceEnabled, int EarlyGraceMin, bool EarlyBeforeGrace,
    bool PresentIfNoOut, bool PresentIfNoIn, bool OvertimeEnabled, TimeOnly? OvertimeFrom, TimeOnly? OvertimeTo);

public sealed record ExcuseWindow(TimeOnly From, TimeOnly To);

public sealed record DayContext(DateOnly WorkDate, bool IsWorkDay, bool IsHoliday, bool OnApprovedLeave, bool IsHalfDayLeave, bool IsRamadan, IReadOnlyList<ExcuseWindow> Excuses);

public sealed record AttendanceSettings(int RamadanMaxMinutes = 360, int MaxDailyTotalMinutes = 720, int DuplicateWindowSeconds = 60, decimal OvertimeRateNormal = 1.5m, decimal OvertimeRateRestHoliday = 2.0m);

public sealed record DailyResult(string Status, DateTime? InTime, DateTime? OutTime, int RequiredMinutes, int WorkMinutes, int LateMin, int EarlyLeaveMin, int AbsentMin, int OvertimeMin, int ExcusedMin, decimal OvertimeRate);

public static class AttendanceStatus
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Leave = "leave";
    public const string Holiday = "holiday";
    public const string HolidayWork = "holiday_work";
    public const string Weekend = "weekend";
    public const string Excused = "excused";
}
=== END FILE ===
=== FILE: src/Hrms.Domain/Attendance/AttendanceEngine.cs ===
namespace Hrms.Domain.Attendance;

public static class AttendanceEngine
{
    public static DailyResult Compute(ShiftDef shift, DayContext day, IReadOnlyList<DateTime> punches, AttendanceSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(day);
        settings ??= new AttendanceSettings();

        var p = Normalize(punches ?? Array.Empty<DateTime>(), settings.DuplicateWindowSeconds);

        var startDt = day.WorkDate.ToDateTime(shift.Start);
        var endDt = day.WorkDate.ToDateTime(shift.End);
        if (shift.CrossesMidnight || shift.End <= shift.Start)
        {
            endDt = endDt.AddDays(1);
        }

        // 1) Full-day approved leave
        if (day.OnApprovedLeave && !day.IsHalfDayLeave)
        {
            return Zero(AttendanceStatus.Leave);
        }

        // 2) Holiday
        if (day.IsHoliday)
        {
            if (p.Count >= 2)
            {
                var ot = Math.Max(0, Minutes(p[^1] - p[0]) - shift.BreakMinutes);
                return new DailyResult(AttendanceStatus.HolidayWork, p[0], p[^1], 0, 0, 0, 0, 0, ot, 0, settings.OvertimeRateRestHoliday);
            }
            return Zero(AttendanceStatus.Holiday);
        }

        // 3) Rest day
        if (!day.IsWorkDay)
        {
            if (p.Count >= 2)
            {
                var ot = Math.Max(0, Minutes(p[^1] - p[0]) - shift.BreakMinutes);
                return new DailyResult(AttendanceStatus.Present, p[0], p[^1], 0, 0, 0, 0, 0, ot, 0, settings.OvertimeRateRestHoliday);
            }
            return Zero(AttendanceStatus.Weekend);
        }

        // 4) Work day
        var required = shift.WorkMinutes;
        if (day.IsRamadan)
        {
            required = Math.Min(required, settings.RamadanMaxMinutes);
        }
        if (day.IsHalfDayLeave)
        {
            required /= 2;
        }
        required = Math.Max(0, required);

        if (day.IsHalfDayLeave || day.IsRamadan)
        {
            endDt = startDt.AddMinutes(required + shift.BreakMinutes);
        }

        var inFrom = startDt.AddMinutes(-shift.BeforeInMin);
        var inTo = startDt.AddMinutes(shift.AfterInMin);
        var outFrom = endDt.AddMinutes(-shift.BeforeOutMin);
        var outTo = endDt.AddMinutes(shift.AfterOutMin);

        DateTime? inTime = null;
        foreach (var x in p)
        {
            if (x >= inFrom && x <= inTo)
            {
                inTime = x;
                break;
            }
        }

        DateTime? outTime = null;
        for (var i = p.Count - 1; i >= 0; i--)
        {
            var x = p[i];
            if (x >= outFrom && x <= outTo && (inTime is null || x > inTime.Value))
            {
                outTime = x;
                break;
            }
        }

        if (inTime is null && outTime is null)
        {
            return new DailyResult(AttendanceStatus.Absent, null, null, required, 0, 0, 0, required, 0, 0, 0m);
        }

        if (inTime is not null && outTime is null)
        {
            return shift.PresentIfNoOut
                ? new DailyResult(AttendanceStatus.Present, inTime, null, required, required, 0, 0, 0, 0, 0, 0m)
                : new DailyResult(AttendanceStatus.Absent, inTime, null, required, 0, 0, 0, required, 0, 0, 0m);
        }

        if (inTime is null && outTime is not null)
        {
            return shift.PresentIfNoIn
                ? new DailyResult(AttendanceStatus.Present, null, outTime, required, required, 0, 0, 0, 0, 0, 0m)
                : new DailyResult(AttendanceStatus.Absent, null, outTime, required, 0, 0, 0, required, 0, 0, 0m);
        }

        var inV = inTime!.Value;
        var outV = outTime!.Value;

        var lateRaw = Math.Max(0, Minutes(inV - startDt));
        var earlyRaw = Math.Max(0, Minutes(endDt - outV));

        var excuses = MergeExcuses(day.WorkDate, day.Excuses ?? Array.Empty<ExcuseWindow>());

        var excusedLate = inV > startDt ? Math.Min(lateRaw, IntersectAll(excuses, startDt, inV)) : 0;
        var excusedEarly = endDt > outV ? Math.Min(earlyRaw, IntersectAll(excuses, outV, endDt)) : 0;

        var lateNet = Math.Max(0, lateRaw - excusedLate);
        var earlyNet = Math.Max(0, earlyRaw - excusedEarly);

        var lateMin = ApplyGrace(lateNet, shift.LateGraceEnabled, shift.LateGraceMin, shift.LateAfterGrace);
        var earlyMin = ApplyGrace(earlyNet, shift.EarlyGraceEnabled, shift.EarlyGraceMin, shift.EarlyBeforeGrace);

        var excusedMin = excusedLate + excusedEarly;

        var effIn = inV > startDt ? inV : startDt;
        var effOut = outV < endDt ? outV : endDt;
        var work = Math.Max(0, Minutes(effOut - effIn) - shift.BreakMinutes);
        work = Math.Min(work, required);

        var absent = Math.Max(0, required - work - excusedMin - lateMin - earlyMin);

        var overtime = 0;
        if (shift.OvertimeEnabled && shift.OvertimeFrom is not null && shift.OvertimeTo is not null && outV > endDt)
        {
            var otFrom = day.WorkDate.ToDateTime(shift.OvertimeFrom.Value);
            var otTo = day.WorkDate.ToDateTime(shift.OvertimeTo.Value);
            if (shift.OvertimeTo.Value <= shift.OvertimeFrom.Value)
            {
                otTo = otTo.AddDays(1);
            }
            overtime = Overlap(endDt, outV, otFrom, otTo);
        }
        overtime = Math.Min(overtime, Math.Max(0, settings.MaxDailyTotalMinutes - work));
        overtime = Math.Max(0, overtime);

        var status = excusedMin > 0 && lateMin == 0 && earlyMin == 0 && absent == 0
            ? AttendanceStatus.Excused
            : AttendanceStatus.Present;

        return new DailyResult(status, inV, outV, required, work, lateMin, earlyMin, absent, overtime, excusedMin, settings.OvertimeRateNormal);
    }

    private static DailyResult Zero(string status) =>
        new(status, null, null, 0, 0, 0, 0, 0, 0, 0, 0m);

    private static List<DateTime> Normalize(IReadOnlyList<DateTime> punches, int windowSeconds)
    {
        var sorted = punches.OrderBy(x => x).ToList();
        var result = new List<DateTime>(sorted.Count);
        foreach (var x in sorted)
        {
            if (result.Count > 0 && (x - result[^1]).TotalSeconds < windowSeconds)
            {
                continue;
            }
            result.Add(x);
        }
        return result;
    }

    private static int Minutes(TimeSpan ts) => (int)Math.Floor(ts.TotalMinutes);

    private static int ApplyGrace(int net, bool enabled, int grace, bool afterGrace)
    {
        if (net <= 0)
        {
            return 0;
        }
        if (enabled && net <= grace)
        {
            return 0;
        }
        if (enabled && afterGrace)
        {
            return Math.Max(0, net - grace);
        }
        return net;
    }

    private static int Overlap(DateTime a1, DateTime a2, DateTime b1, DateTime b2)
    {
        var s = a1 > b1 ? a1 : b1;
        var e = a2 < b2 ? a2 : b2;
        return e > s ? Minutes(e - s) : 0;
    }

    private static List<(DateTime From, DateTime To)> MergeExcuses(DateOnly workDate, IReadOnlyList<ExcuseWindow> excuses)
    {
        var intervals = new List<(DateTime From, DateTime To)>(excuses.Count);
        foreach (var e in excuses)
        {
            var from = workDate.ToDateTime(e.From);
            var to = workDate.ToDateTime(e.To);
            if (e.To <= e.From)
            {
                to = to.AddDays(1);
            }
            intervals.Add((from, to));
        }

        intervals.Sort((a, b) => a.From.CompareTo(b.From));
        var merged = new List<(DateTime From, DateTime To)>(intervals.Count);
        foreach (var iv in intervals)
        {
            if (merged.Count > 0 && iv.From <= merged[^1].To)
            {
                var last = merged[^1];
                merged[^1] = (last.From, iv.To > last.To ? iv.To : last.To);
            }
            else
            {
                merged.Add(iv);
            }
        }
        return merged;
    }

    private static int IntersectAll(List<(DateTime From, DateTime To)> intervals, DateTime from, DateTime to)
    {
        var total = 0;
        foreach (var iv in intervals)
        {
            total += Overlap(iv.From, iv.To, from, to);
        }
        return total;
    }
}
=== END FILE ===
=== FILE: tests/Hrms.Tests/AttendanceEngineTests.cs ===
using System.Globalization;
using Hrms.Domain.Attendance;
using Xunit;

namespace Hrms.Tests;

public class AttendanceEngineTests
{
    private static readonly DateOnly Date = new(2026, 11, 9);

    private static ShiftDef Std() => new(
        new TimeOnly(8, 0), new TimeOnly(16, 0), 480, 0, false,
        60, 240, 240, 240,
        true, 10, true,
        true, 10, true,
        false, false, false, null, null);

    private static DayContext WorkDay(params ExcuseWindow[] excuses) =>
        new(Date, true, false, false, false, false, excuses);

    private static DateTime T(string hhmm) =>
        Date.ToDateTime(TimeOnly.Parse(hhmm, CultureInfo.InvariantCulture));

    private static DateTime[] P(params string[] times) => times.Select(T).ToArray();

    private static void AssertNumbers(DailyResult r, int late, int early, int work, int absent, int ot, int excused, string status)
    {
        Assert.Equal(late, r.LateMin);
        Assert.Equal(early, r.EarlyLeaveMin);
        Assert.Equal(work, r.WorkMinutes);
        Assert.Equal(absent, r.AbsentMin);
        Assert.Equal(ot, r.OvertimeMin);
        Assert.Equal(excused, r.ExcusedMin);
        Assert.Equal(status, r.Status);
    }

    private static void AssertAllZero(DailyResult r)
    {
        Assert.Equal(0, r.RequiredMinutes);
        Assert.Equal(0, r.WorkMinutes);
        Assert.Equal(0, r.LateMin);
        Assert.Equal(0, r.EarlyLeaveMin);
        Assert.Equal(0, r.AbsentMin);
        Assert.Equal(0, r.OvertimeMin);
        Assert.Equal(0, r.ExcusedMin);
    }

    [Theory]
    [InlineData("08:00", "16:00", 0, 0, 480, 0, 0, 0, "present")]
    [InlineData("08:09", "16:00", 0, 0, 471, 9, 0, 0, "present")]
    [InlineData("08:25", "16:00", 15, 0, 455, 10, 0, 0, "present")]
    [InlineData("08:00", "15:30", 0, 20, 450, 10, 0, 0, "present")]
    public void Case1to4_BasicInOut(string inT, string outT, int late, int early, int work, int absent, int ot, int excused, string status)
    {
        var r = AttendanceEngine.Compute(Std(), WorkDay(), P(inT, outT));
        AssertNumbers(r, late, early, work, absent, ot, excused, status);
        Assert.Equal(480, r.RequiredMinutes);
    }

    [Fact]
    public void Case5_NoPunches_Absent()
    {
        var r = AttendanceEngine.Compute(Std(), WorkDay(), Array.Empty<DateTime>());
        AssertNumbers(r, 0, 0, 0, 480, 0, 0, "absent");
    }

    [Fact]
    public void Case6_InOnly_Absent()
    {
        var r = AttendanceEngine.Compute(Std(), WorkDay(), P("08:00"));
        AssertNumbers(r, 0, 0, 0, 480, 0, 0, "absent");
    }

    [Fact]
    public void Case7_InOnly_PresentIfNoOut()
    {
        var shift = Std() with { PresentIfNoOut = true };
        var r = AttendanceEngine.Compute(shift, WorkDay(), P("08:00"));
        AssertNumbers(r, 0, 0, 480, 0, 0, 0, "present");
    }

    [Fact]
    public void Case8_DuplicatePunchesMerged()
    {
        var r = AttendanceEngine.Compute(Std(), WorkDay(), P("08:00", "08:00:30", "16:00"));
        AssertNumbers(r, 0, 0, 480, 0, 0, 0, "present");
        Assert.Equal(T("08:00"), r.InTime);
        Assert.Equal(T("16:00"), r.OutTime);
    }

    [Fact]
    public void Case9_Holiday_NoPunches()
    {
        var day = new DayContext(Date, true, true, false, false, false, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, Array.Empty<DateTime>());
        Assert.Equal("holiday", r.Status);
        AssertAllZero(r);
    }

    [Fact]
    public void Case10_HolidayWork()
    {
        var day = new DayContext(Date, true, true, false, false, false, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, P("08:00", "12:00"));
        Assert.Equal("holiday_work", r.Status);
        Assert.Equal(0, r.RequiredMinutes);
        Assert.Equal(0, r.WorkMinutes);
        Assert.Equal(240, r.OvertimeMin);
        Assert.Equal(2.0m, r.OvertimeRate);
    }

    [Fact]
    public void Case11_Weekend_NoPunches()
    {
        var day = new DayContext(Date, false, false, false, false, false, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, Array.Empty<DateTime>());
        Assert.Equal("weekend", r.Status);
        AssertAllZero(r);
    }

    [Fact]
    public void Case12_ApprovedLeave()
    {
        var day = new DayContext(Date, true, false, true, false, false, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, P("08:00", "16:00"));
        Assert.Equal("leave", r.Status);
        AssertAllZero(r);
    }

    [Fact]
    public void Case13_Excused()
    {
        var day = WorkDay(new ExcuseWindow(new TimeOnly(8, 0), new TimeOnly(10, 0)));
        var r = AttendanceEngine.Compute(Std(), day, P("10:00", "16:00"));
        AssertNumbers(r, 0, 0, 360, 0, 0, 120, "excused");
    }

    [Fact]
    public void Case14_Overtime()
    {
        var shift = Std() with { OvertimeEnabled = true, OvertimeFrom = new TimeOnly(16, 0), OvertimeTo = new TimeOnly(20, 0) };
        var r = AttendanceEngine.Compute(shift, WorkDay(), P("08:00", "18:00"));
        AssertNumbers(r, 0, 0, 480, 0, 120, 0, "present");
        Assert.Equal(1.5m, r.OvertimeRate);
    }

    [Fact]
    public void Case15_NightShift_CrossesMidnight()
    {
        var shift = Std() with { Start = new TimeOnly(22, 0), End = new TimeOnly(6, 0), CrossesMidnight = true };
        var punches = new[] { new DateTime(2026, 11, 9, 22, 5, 0), new DateTime(2026, 11, 10, 6, 0, 0) };
        var r = AttendanceEngine.Compute(shift, WorkDay(), punches);
        AssertNumbers(r, 0, 0, 475, 5, 0, 0, "present");
    }

    [Fact]
    public void Case16_Ramadan()
    {
        var day = new DayContext(Date, true, false, false, false, true, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, P("08:00", "14:00"));
        Assert.Equal(360, r.RequiredMinutes);
        Assert.Equal(360, r.WorkMinutes);
        Assert.Equal(0, r.AbsentMin);
        Assert.Equal(0, r.EarlyLeaveMin);
        Assert.Equal("present", r.Status);
    }

    [Fact]
    public void Case17_HalfDayLeave()
    {
        var day = new DayContext(Date, true, false, true, true, false, Array.Empty<ExcuseWindow>());
        var r = AttendanceEngine.Compute(Std(), day, P("08:00", "12:00"));
        Assert.Equal(240, r.RequiredMinutes);
        Assert.Equal(240, r.WorkMinutes);
        Assert.Equal(0, r.AbsentMin);
        Assert.Equal(0, r.EarlyLeaveMin);
        Assert.Equal("present", r.Status);
    }

    public static IEnumerable<object[]> InvariantCases()
    {
        yield return new object[] { new[] { "08:00", "16:00" }, false, false, false };
        yield return new object[] { new[] { "08:09", "16:00" }, false, false, false };
        yield return new object[] { new[] { "08:25", "16:00" }, false, false, false };
        yield return new object[] { new[] { "08:00", "15:30" }, false, false, false };
        yield return new object[] { new[] { "08:40", "15:10" }, false, false, false };
        yield return new object[] { new[] { "09:30", "14:00" }, false, false, false };
        yield return new object[] { new[] { "07:30", "17:00" }, false, false, false };
        yield return new object[] { Array.Empty<string>(), false, false, false };
        yield return new object[] { new[] { "08:00" }, false, false, false };
        yield return new object[] { new[] { "08:00" }, true, false, false };
        yield return new object[] { new[] { "16:00" }, false, false, false };
        yield return new object[] { new[] { "16:00" }, false, true, false };
        yield return new object[] { new[] { "10:00", "16:00" }, false, false, true };
        yield return new object[] { new[] { "10:30", "15:00" }, false, false, true };
        yield return new object[] { new[] { "08:00", "08:00:30", "16:00" }, false, false, false };
    }

    [Theory]
    [MemberData(nameof(InvariantCases))]
    public void Case18_Invariant_SumEqualsRequired(string[] punches, bool presentIfNoOut, bool presentIfNoIn, bool withExcuse)
    {
        var shift = Std() with { PresentIfNoOut = presentIfNoOut, PresentIfNoIn = presentIfNoIn };
        var day = withExcuse
            ? WorkDay(new ExcuseWindow(new TimeOnly(8, 0), new TimeOnly(10, 0)))
            : WorkDay();
        var r = AttendanceEngine.Compute(shift, day, P(punches));
        Assert.Equal(r.RequiredMinutes, r.WorkMinutes + r.LateMin + r.EarlyLeaveMin + r.AbsentMin + r.ExcusedMin);
    }
}
=== END FILE ===