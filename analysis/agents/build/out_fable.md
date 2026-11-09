=== FILE: src/Hrms.Domain/Payroll/PayrollModels.cs ===
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
=== END FILE ===

=== FILE: src/Hrms.Domain/Payroll/PayrollCalculator.cs ===
namespace Hrms.Domain.Payroll;

public static class PayrollCalculator
{
    private const string CodeOvertime = "OVERTIME";
    private const string CodeAbsence = "ABSENCE";
    private const string CodeLate = "LATE";
    private const string CodeUnpaidLeave = "UNPAID_LEAVE";
    private const string CodeAdvance = "ADVANCE";
    private const string CodeCustody = "CUSTODY";
    private const string CodeInsurance = "INSURANCE";

    public static PayrollResult Calculate(PayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.FixedItems);
        ArgumentNullException.ThrowIfNull(input.VariableItems);
        ArgumentNullException.ThrowIfNull(input.Attendance);
        ArgumentNullException.ThrowIfNull(input.Settings);

        if (input.DailyHours <= 0)
            throw new ArgumentException("عدد ساعات العمل اليومية يجب أن يكون أكبر من صفر.", nameof(input));

        if (input.Settings.MonthDays <= 0)
            throw new ArgumentException("عدد أيام الشهر يجب أن يكون أكبر من صفر.", nameof(input));

        var basicItems = input.FixedItems
            .Where(i => i.IsBasic && i.Type == ItemType.Earning)
            .ToList();

        if (basicItems.Count == 0)
            throw new ArgumentException("لا يوجد بند راتب أساسي (IsBasic) ضمن البنود الثابتة.", nameof(input));

        decimal basic = 0m;
        foreach (var item in basicItems)
        {
            basic += item.Method switch
            {
                CalcMethod.Fixed => item.Amount,
                _ => throw new ArgumentException($"بند الراتب الأساسي '{item.Code}' يجب أن يكون بطريقة حساب ثابتة (Fixed).", nameof(input))
            };
        }

        if (basic <= 0)
            throw new ArgumentException("الراتب الأساسي يجب أن يكون أكبر من صفر.", nameof(input));

        decimal dayRate = basic / input.Settings.MonthDays;
        decimal hourRate = dayRate / input.DailyHours;

        var items = new List<PayrollLineItem>();
        decimal totalEarnings = 0m;
        decimal totalDeductions = 0m;
        decimal insurableWage = 0m;

        // First pass: Fixed and PctBasic items (fixed list then variable list).
        var fixedComputed = new List<(SalaryItem Item, decimal Amount)>();
        var variableComputed = new List<(SalaryItem Item, decimal Amount)>();
        var pendingPctGross = new List<(SalaryItem Item, bool IsFixedList)>();

        foreach (var item in input.FixedItems)
        {
            if (item.Method == CalcMethod.PctGross)
            {
                pendingPctGross.Add((item, true));
                continue;
            }
            fixedComputed.Add((item, ComputeFirstPass(item, basic)));
        }

        foreach (var item in input.VariableItems)
        {
            if (item.Method == CalcMethod.PctGross)
            {
                pendingPctGross.Add((item, false));
                continue;
            }
            variableComputed.Add((item, ComputeFirstPass(item, basic)));
        }

        decimal grossBase = fixedComputed.Concat(variableComputed)
            .Where(x => x.Item.Type == ItemType.Earning)
            .Sum(x => x.Amount);

        foreach (var (item, isFixedList) in pendingPctGross)
        {
            decimal amount = grossBase * RequirePercentage(item) / 100m;
            if (isFixedList)
                fixedComputed.Add((item, amount));
            else
                variableComputed.Add((item, amount));
        }

        foreach (var (item, rawAmount) in fixedComputed)
        {
            decimal amount = Round2(rawAmount);
            items.Add(new PayrollLineItem(item.Code, item.NameAr, item.Type, amount));
            if (item.Type == ItemType.Earning)
            {
                totalEarnings += amount;
                if (item.SubjectToInsurance)
                    insurableWage += amount;
            }
            else
            {
                totalDeductions += amount;
            }
        }

        foreach (var (item, rawAmount) in variableComputed)
        {
            decimal amount = Round2(rawAmount);
            items.Add(new PayrollLineItem(item.Code, item.NameAr, item.Type, amount));
            if (item.Type == ItemType.Earning)
                totalEarnings += amount;
            else
                totalDeductions += amount;
        }

        var att = input.Attendance;
        var s = input.Settings;

        // OVERTIME (م56)
        decimal overtime =
            (att.OvertimeNormalMinutes / 60m * hourRate * s.OvertimeRateNormal) +
            (att.OvertimeSpecialMinutes / 60m * hourRate * s.OvertimeRateSpecial);
        AddEarning(items, ref totalEarnings, CodeOvertime, "عمل إضافي", overtime);

        // ABSENCE
        decimal absence = att.AbsentDays * dayRate + att.AbsentMinutesPartial / 60m * hourRate;
        AddDeduction(items, ref totalDeductions, CodeAbsence, "غياب", absence);

        // LATE
        decimal late = (att.LateMinutes + att.EarlyLeaveMinutes) / 60m * hourRate;
        AddDeduction(items, ref totalDeductions, CodeLate, "تأخير وانصراف مبكر", late);

        // UNPAID_LEAVE
        decimal unpaid = att.UnpaidLeaveDays * dayRate;
        AddDeduction(items, ref totalDeductions, CodeUnpaidLeave, "إجازة بدون راتب", unpaid);

        // ADVANCE
        AddDeduction(items, ref totalDeductions, CodeAdvance, "قسط سلفة", input.AdvanceInstallment);

        // CUSTODY (م64) - سقف 25% من الأساسي
        decimal custody = Math.Min(input.CustodyDeduction, basic * 0.25m);
        AddDeduction(items, ref totalDeductions, CodeCustody, "خصم عهدة", custody);

        // INSURANCE
        if (s.DeductInsurance)
        {
            decimal insurance = insurableWage * s.InsuranceEmployeePct / 100m;
            AddDeduction(items, ref totalDeductions, CodeInsurance, "تأمينات اجتماعية (حصة الموظف)", insurance);
        }

        decimal employerInsurance = Round2(insurableWage * s.InsuranceEmployerPct / 100m);

        totalEarnings = Round2(totalEarnings);
        totalDeductions = Round2(totalDeductions);
        decimal net = Round2(totalEarnings - totalDeductions);

        if (net < 0)
            throw new InvalidOperationException(
                $"صافي الراتب للموظف {input.EmployeeId} سالب ({net}). مجموع الاستقطاعات يتجاوز مجموع المستحقات.");

        return new PayrollResult(
            input.EmployeeId,
            Round2(basic),
            Round2(dayRate),
            Round2(hourRate),
            Round2(insurableWage),
            totalEarnings,
            totalDeductions,
            net,
            employerInsurance,
            items);
    }

    private static decimal ComputeFirstPass(SalaryItem item, decimal basic)
    {
        return item.Method switch
        {
            CalcMethod.Fixed => item.Amount,
            CalcMethod.PctBasic => basic * RequirePercentage(item) / 100m,
            _ => throw new ArgumentException($"طريقة حساب غير مدعومة للبند '{item.Code}'.")
        };
    }

    private static decimal RequirePercentage(SalaryItem item)
    {
        if (item.Percentage is null)
            throw new ArgumentException($"البند '{item.Code}' يستخدم نسبة مئوية ولكن النسبة غير محددة.");
        return item.Percentage.Value;
    }

    private static void AddEarning(List<PayrollLineItem> items, ref decimal total, string code, string nameAr, decimal rawAmount)
    {
        decimal amount = Round2(rawAmount);
        if (amount <= 0) return;
        items.Add(new PayrollLineItem(code, nameAr, ItemType.Earning, amount));
        total += amount;
    }

    private static void AddDeduction(List<PayrollLineItem> items, ref decimal total, string code, string nameAr, decimal rawAmount)
    {
        decimal amount = Round2(rawAmount);
        if (amount <= 0) return;
        items.Add(new PayrollLineItem(code, nameAr, ItemType.Deduction, amount));
        total += amount;
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
=== END FILE ===

=== FILE: src/Hrms.Domain/Labor/YemenLaborRules.cs ===
namespace Hrms.Domain.Labor;

public static class YemenLaborRules
{
    private static readonly (int UpTo, decimal PayPct)[] SickTiers =
    {
        (60, 100m),
        (120, 85m),
        (180, 75m),
        (240, 50m)
    };

    public static decimal AnnualLeaveAccrued(int fullMonthsOfService)
    {
        if (fullMonthsOfService < 0)
            throw new ArgumentOutOfRangeException(nameof(fullMonthsOfService), "عدد الأشهر لا يمكن أن يكون سالباً.");
        return Round2(2.5m * fullMonthsOfService);
    }

    public static decimal MaxCarryOver(decimal entitlement)
    {
        if (entitlement < 0)
            throw new ArgumentOutOfRangeException(nameof(entitlement), "الاستحقاق لا يمكن أن يكون سالباً.");
        return Round2(entitlement / 2m);
    }

    public static IReadOnlyList<(int Days, decimal PayPct)> SickLeavePay(int sickDayIndexStart, int days)
    {
        if (sickDayIndexStart < 1)
            throw new ArgumentOutOfRangeException(nameof(sickDayIndexStart), "ترتيب اليوم المرضي يبدأ من 1.");
        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(days), "عدد الأيام لا يمكن أن يكون سالباً.");

        var result = new List<(int Days, decimal PayPct)>();
        if (days == 0) return result;

        int current = sickDayIndexStart;
        int end = sickDayIndexStart + days - 1;

        foreach (var (upTo, pct) in SickTiers)
        {
            if (current > end) break;
            if (current > upTo) continue;

            int tierEnd = Math.Min(upTo, end);
            int count = tierEnd - current + 1;
            if (count > 0)
            {
                result.Add((count, pct));
                current = tierEnd + 1;
            }
        }

        if (current <= end)
        {
            result.Add((end - current + 1, 0m));
        }

        return result;
    }

    public static decimal SickLeavePayAmount(decimal dayRate, int priorSickDaysThisYear, int days)
    {
        if (dayRate < 0)
            throw new ArgumentOutOfRangeException(nameof(dayRate), "أجر اليوم لا يمكن أن يكون سالباً.");
        if (priorSickDaysThisYear < 0)
            throw new ArgumentOutOfRangeException(nameof(priorSickDaysThisYear), "الأيام المرضية السابقة لا يمكن أن تكون سالبة.");

        var tiers = SickLeavePay(priorSickDaysThisYear + 1, days);
        decimal total = 0m;
        foreach (var (tierDays, pct) in tiers)
        {
            total += tierDays * dayRate * pct / 100m;
        }
        return Round2(total);
    }

    public static bool HajjEligible(DateOnly hireDate, DateOnly onDate, bool usedBefore)
    {
        if (usedBefore) return false;
        return FullYearsBetween(hireDate, onDate) >= 4;
    }

    public static int MaternityDays(bool difficultOrTwins) => difficultOrTwins ? 80 : 60;

    public static int NoticeDays(string payFrequency)
    {
        if (string.IsNullOrWhiteSpace(payFrequency)) return 7;
        return payFrequency.Trim().ToLowerInvariant() switch
        {
            "monthly" => 30,
            "half_monthly" => 15,
            _ => 7
        };
    }

    public static decimal EndOfServiceGratuityUninsured(decimal lastMonthlyWage, DateOnly hire, DateOnly end)
    {
        if (lastMonthlyWage < 0)
            throw new ArgumentOutOfRangeException(nameof(lastMonthlyWage), "الأجر لا يمكن أن يكون سالباً.");
        if (end < hire)
            throw new ArgumentException("تاريخ نهاية الخدمة لا يمكن أن يسبق تاريخ التعيين.", nameof(end));

        int days = end.DayNumber - hire.DayNumber;
        decimal years = Math.Round(days / 365m, 4, MidpointRounding.AwayFromZero);
        return Round2(lastMonthlyWage * years);
    }

    public static decimal MaxDisciplinaryDeduction(decimal basic)
    {
        if (basic < 0)
            throw new ArgumentOutOfRangeException(nameof(basic), "الراتب الأساسي لا يمكن أن يكون سالباً.");
        return Round2(basic * 0.20m);
    }

    public static bool IsDisciplineWithinDeadline(DateOnly discovered, DateOnly action)
    {
        int diff = action.DayNumber - discovered.DayNumber;
        return diff >= 0 && diff <= 15;
    }

    public static int MaxDailyMinutes(bool ramadan, bool juvenile, bool pregnantOrNursing)
    {
        int max = 480;
        if (juvenile) max = Math.Min(max, 420);
        if (pregnantOrNursing) max = Math.Min(max, 300);
        if (ramadan) max = Math.Min(max, 360);
        return max;
    }

    public static bool OvertimeAllowed(bool juvenile, bool pregnantFromSixthMonthOrPostnatal) =>
        !juvenile && !pregnantFromSixthMonthOrPostnatal;

    public static bool ProbationValid(int months) => months >= 0 && months <= 6;

    private static int FullYearsBetween(DateOnly start, DateOnly end)
    {
        if (end < start) return 0;
        int years = end.Year - start.Year;
        if (end < start.AddYears(years)) years--;
        return years < 0 ? 0 : years;
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
=== END FILE ===

=== FILE: tests/Hrms.Tests/PayrollCalculatorTests.cs ===
using Hrms.Domain.Payroll;
using Xunit;

namespace Hrms.Tests;

public class PayrollCalculatorTests
{
    private static SalaryItem BasicItem(decimal amount = 300000m) =>
        new("BASIC", "الراتب الأساسي", ItemType.Earning, CalcMethod.Fixed, amount, null, true, true);

    private static AttendanceTotals EmptyAttendance() =>
        new(0m, 0, 0, 0, 0, 0, 0m);

    private static PayrollInput BuildInput(
        IReadOnlyList<SalaryItem>? fixedItems = null,
        IReadOnlyList<SalaryItem>? variableItems = null,
        AttendanceTotals? attendance = null,
        decimal advance = 0m,
        decimal custody = 0m,
        PayrollSettings? settings = null,
        decimal dailyHours = 8m)
    {
        return new PayrollInput(
            EmployeeId: 1,
            DailyHours: dailyHours,
            FixedItems: fixedItems ?? new List<SalaryItem> { BasicItem() },
            VariableItems: variableItems ?? new List<SalaryItem>(),
            Attendance: attendance ?? EmptyAttendance(),
            AdvanceInstallment: advance,
            CustodyDeduction: custody,
            Settings: settings ?? new PayrollSettings());
    }

    private static decimal ItemAmount(PayrollResult result, string code) =>
        result.Items.Single(i => i.Code == code).Amount;

    [Fact]
    public void ReferenceExample_ProducesExpectedFigures()
    {
        var attendance = new AttendanceTotals(
            AbsentDays: 1m,
            AbsentMinutesPartial: 0,
            LateMinutes: 60,
            EarlyLeaveMinutes: 0,
            OvertimeNormalMinutes: 120,
            OvertimeSpecialMinutes: 0,
            UnpaidLeaveDays: 0m);

        var input = BuildInput(attendance: attendance, advance: 20000m);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(300000m, result.BasicSalary);
        Assert.Equal(10000m, result.DayRate);
        Assert.Equal(1250m, result.HourRate);
        Assert.Equal(300000m, result.InsurableWage);
        Assert.Equal(3750m, ItemAmount(result, "OVERTIME"));
        Assert.Equal(303750m, result.TotalEarnings);
        Assert.Equal(10000m, ItemAmount(result, "ABSENCE"));
        Assert.Equal(1250m, ItemAmount(result, "LATE"));
        Assert.Equal(18000m, ItemAmount(result, "INSURANCE"));
        Assert.Equal(20000m, ItemAmount(result, "ADVANCE"));
        Assert.Equal(49250m, result.TotalDeductions);
        Assert.Equal(254500m, result.NetPay);
        Assert.Equal(27000m, result.EmployerInsurance);
    }

    [Fact]
    public void HousingAllowance_PctBasic20_Is60000_AndNotInsurable()
    {
        var fixedItems = new List<SalaryItem>
        {
            BasicItem(),
            new("HOUSING", "بدل سكن", ItemType.Earning, CalcMethod.PctBasic, 0m, 20m, false, false)
        };

        var result = PayrollCalculator.Calculate(BuildInput(fixedItems: fixedItems));

        Assert.Equal(60000m, ItemAmount(result, "HOUSING"));
        Assert.Equal(300000m, result.InsurableWage);
        Assert.Equal(360000m, result.TotalEarnings);
        Assert.Equal(18000m, ItemAmount(result, "INSURANCE"));
        Assert.Equal(342000m, result.NetPay);
    }

    [Fact]
    public void Custody_IsCappedAt25PercentOfBasic()
    {
        var result = PayrollCalculator.Calculate(BuildInput(custody: 100000m));

        Assert.Equal(75000m, ItemAmount(result, "CUSTODY"));
    }

    [Fact]
    public void Custody_BelowCap_IsDeductedInFull()
    {
        var result = PayrollCalculator.Calculate(BuildInput(custody: 50000m));

        Assert.Equal(50000m, ItemAmount(result, "CUSTODY"));
    }

    [Fact]
    public void PctGross_IsComputedOnFixedAndPctBasicEarnings()
    {
        var fixedItems = new List<SalaryItem>
        {
            BasicItem(),
            new("HOUSING", "بدل سكن", ItemType.Earning, CalcMethod.PctBasic, 0m, 20m, false, false),
            new("BONUS", "مكافأة", ItemType.Earning, CalcMethod.PctGross, 0m, 10m, false, false)
        };

        var result = PayrollCalculator.Calculate(BuildInput(fixedItems: fixedItems));

        Assert.Equal(36000m, ItemAmount(result, "BONUS"));
    }

    [Fact]
    public void NoBasicItem_ThrowsArgumentException()
    {
        var fixedItems = new List<SalaryItem>
        {
            new("HOUSING", "بدل سكن", ItemType.Earning, CalcMethod.Fixed, 50000m, null, false, false)
        };

        Assert.Throws<ArgumentException>(() => PayrollCalculator.Calculate(BuildInput(fixedItems: fixedItems)));
    }

    [Fact]
    public void ZeroDailyHours_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PayrollCalculator.Calculate(BuildInput(dailyHours: 0m)));
    }

    [Fact]
    public void NegativeNet_ThrowsInvalidOperationException()
    {
        var input = BuildInput(advance: 500000m);

        Assert.Throws<InvalidOperationException>(() => PayrollCalculator.Calculate(input));
    }

    [Fact]
    public void DeductInsuranceFalse_NoInsuranceLine_ButEmployerInsuranceStillComputed()
    {
        var settings = new PayrollSettings(DeductInsurance: false);
        var result = PayrollCalculator.Calculate(BuildInput(settings: settings));

        Assert.DoesNotContain(result.Items, i => i.Code == "INSURANCE");
        Assert.Equal(27000m, result.EmployerInsurance);
        Assert.Equal(300000m, result.NetPay);
    }

    [Fact]
    public void ZeroStatutoryAmounts_AreNotAddedAsItems()
    {
        var result = PayrollCalculator.Calculate(BuildInput());

        Assert.DoesNotContain(result.Items, i => i.Code == "OVERTIME");
        Assert.DoesNotContain(result.Items, i => i.Code == "ABSENCE");
        Assert.DoesNotContain(result.Items, i => i.Code == "LATE");
        Assert.DoesNotContain(result.Items, i => i.Code == "UNPAID_LEAVE");
        Assert.DoesNotContain(result.Items, i => i.Code == "ADVANCE");
        Assert.DoesNotContain(result.Items, i => i.Code == "CUSTODY");
    }

    [Fact]
    public void SpecialOvertime_UsesSpecialRate()
    {
        var attendance = new AttendanceTotals(0m, 0, 0, 0, 0, 60, 0m);
        var result = PayrollCalculator.Calculate(BuildInput(attendance: attendance));

        Assert.Equal(2500m, ItemAmount(result, "OVERTIME"));
    }

    [Fact]
    public void UnpaidLeave_DeductsDayRatePerDay()
    {
        var attendance = new AttendanceTotals(0m, 0, 0, 0, 0, 0, 2m);
        var result = PayrollCalculator.Calculate(BuildInput(attendance: attendance));

        Assert.Equal(20000m, ItemAmount(result, "UNPAID_LEAVE"));
    }
}
=== END FILE ===

=== FILE: tests/Hrms.Tests/YemenLaborRulesTests.cs ===
using Hrms.Domain.Labor;
using Xunit;

namespace Hrms.Tests;

public class YemenLaborRulesTests
{
    [Fact]
    public void AnnualLeaveAccrued_12Months_Is30Days()
    {
        Assert.Equal(30m, YemenLaborRules.AnnualLeaveAccrued(12));
    }

    [Fact]
    public void MaxCarryOver_IsHalfEntitlement()
    {
        Assert.Equal(15m, YemenLaborRules.MaxCarryOver(30m));
    }

    [Fact]
    public void SickLeavePayAmount_Prior50_20Days_Is185000()
    {
        Assert.Equal(185000m, YemenLaborRules.SickLeavePayAmount(10000m, 50, 20));
    }

    [Fact]
    public void SickLeavePay_SpansAllTiers()
    {
        var tiers = YemenLaborRules.SickLeavePay(1, 250);

        Assert.Equal(5, tiers.Count);
        Assert.Equal((60, 100m), tiers[0]);
        Assert.Equal((60, 85m), tiers[1]);
        Assert.Equal((60, 75m), tiers[2]);
        Assert.Equal((60, 50m), tiers[3]);
        Assert.Equal((10, 0m), tiers[4]);
    }

    [Fact]
    public void SickLeavePay_ZeroDays_ReturnsEmpty()
    {
        Assert.Empty(YemenLaborRules.SickLeavePay(1, 0));
    }

    [Fact]
    public void SickLeavePay_StartBeyond240_IsUnpaid()
    {
        var tiers = YemenLaborRules.SickLeavePay(241, 5);

        Assert.Single(tiers);
        Assert.Equal((5, 0m), tiers[0]);
    }

    [Fact]
    public void HajjEligible_ThreePointNineYears_IsFalse()
    {
        var hire = new DateOnly(2020, 1, 1);
        var onDate = new DateOnly(2023, 11, 25);

        Assert.False(YemenLaborRules.HajjEligible(hire, onDate, false));
    }

    [Fact]
    public void HajjEligible_FourYears_IsTrue()
    {
        var hire = new DateOnly(2020, 1, 1);
        var onDate = new DateOnly(2024, 1, 1);

        Assert.True(YemenLaborRules.HajjEligible(hire, onDate, false));
    }

    [Fact]
    public void HajjEligible_UsedBefore_IsFalse()
    {
        var hire = new DateOnly(2010, 1, 1);
        var onDate = new DateOnly(2024, 1, 1);

        Assert.False(YemenLaborRules.HajjEligible(hire, onDate, true));
    }

    [Theory]
    [InlineData(false, 60)]
    [InlineData(true, 80)]
    public void MaternityDays_ReturnsExpected(bool difficult, int expected)
    {
        Assert.Equal(expected, YemenLaborRules.MaternityDays(difficult));
    }

    [Theory]
    [InlineData("monthly", 30)]
    [InlineData("MONTHLY", 30)]
    [InlineData("half_monthly", 15)]
    [InlineData("weekly", 7)]
    [InlineData("daily", 7)]
    public void NoticeDays_ReturnsExpected(string frequency, int expected)
    {
        Assert.Equal(expected, YemenLaborRules.NoticeDays(frequency));
    }

    [Fact]
    public void EndOfServiceGratuityUninsured_TwoYears_200000_Is400000()
    {
        var hire = new DateOnly(2021, 1, 1);
        var end = new DateOnly(2023, 1, 1);

        Assert.Equal(400000m, YemenLaborRules.EndOfServiceGratuityUninsured(200000m, hire, end));
    }

    [Fact]
    public void MaxDisciplinaryDeduction_Is20Percent()
    {
        Assert.Equal(60000m, YemenLaborRules.MaxDisciplinaryDeduction(300000m));
    }

    [Fact]
    public void IsDisciplineWithinDeadline_15Days_True_16Days_False()
    {
        var discovered = new DateOnly(2024, 3, 1);

        Assert.True(YemenLaborRules.IsDisciplineWithinDeadline(discovered, new DateOnly(2024, 3, 16)));
        Assert.False(YemenLaborRules.IsDisciplineWithinDeadline(discovered, new DateOnly(2024, 3, 17)));
    }

    [Theory]
    [InlineData(false, false, false, 480)]
    [InlineData(true, false, false, 360)]
    [InlineData(false, true, false, 420)]
    [InlineData(false, false, true, 300)]
    [InlineData(true, true, false, 360)]
    [InlineData(true, true, true, 300)]
    public void MaxDailyMinutes_ReturnsMinimumApplicable(bool ramadan, bool juvenile, bool pregnant, int expected)
    {
        Assert.Equal(expected, YemenLaborRules.MaxDailyMinutes(ramadan, juvenile, pregnant));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void OvertimeAllowed_ReturnsExpected(bool juvenile, bool pregnant, bool expected)
    {
        Assert.Equal(expected, YemenLaborRules.OvertimeAllowed(juvenile, pregnant));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void ProbationValid_ReturnsExpected(int months, bool expected)
    {
        Assert.Equal(expected, YemenLaborRules.ProbationValid(months));
    }
}
=== END FILE ===