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
