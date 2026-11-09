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
