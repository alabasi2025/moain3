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
