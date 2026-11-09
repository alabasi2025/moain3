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
