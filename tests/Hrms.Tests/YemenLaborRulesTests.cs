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
