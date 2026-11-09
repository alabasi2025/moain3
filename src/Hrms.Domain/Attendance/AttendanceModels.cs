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
