namespace Hrms.Domain.Payroll;

public enum ItemType { Earning, Deduction }

public enum CalcMethod { Fixed, PctBasic, PctGross }

public sealed record SalaryItem(
    string Code,
    string NameAr,
    ItemType Type,
    CalcMethod Method,
    decimal Amount,
    decimal? Percentage,
    bool SubjectToInsurance,
    bool IsBasic);

public sealed record AttendanceTotals(
    decimal AbsentDays,
    int AbsentMinutesPartial,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeNormalMinutes,
    int OvertimeSpecialMinutes,
    decimal UnpaidLeaveDays);

public sealed record PayrollSettings(
    int MonthDays = 30,
    decimal OvertimeRateNormal = 1.5m,
    decimal OvertimeRateSpecial = 2.0m,
    decimal InsuranceEmployeePct = 6m,
    decimal InsuranceEmployerPct = 9m,
    bool DeductInsurance = true);

public sealed record PayrollInput(
    long EmployeeId,
    decimal DailyHours,
    IReadOnlyList<SalaryItem> FixedItems,
    IReadOnlyList<SalaryItem> VariableItems,
    AttendanceTotals Attendance,
    decimal AdvanceInstallment,
    decimal CustodyDeduction,
    PayrollSettings Settings);

public sealed record PayrollLineItem(string Code, string NameAr, ItemType Type, decimal Amount);

public sealed record PayrollResult(
    long EmployeeId,
    decimal BasicSalary,
    decimal DayRate,
    decimal HourRate,
    decimal InsurableWage,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    decimal EmployerInsurance,
    IReadOnlyList<PayrollLineItem> Items);
