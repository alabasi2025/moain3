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
