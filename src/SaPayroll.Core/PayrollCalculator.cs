namespace SaPayroll.Core;

/// <summary>
/// Calculates one pay period using the cumulative (averaging) PAYE method: year-to-date taxable income is
/// annualised, tax is worked out on the annual figure, scaled back to the periods worked so far, and the
/// PAYE already deducted this year is subtracted. Because each period corrects for what came before,
/// variable pay (overtime, commission, a bonus) doesn't leave the employee over- or under-taxed at year end.
/// </summary>
public sealed class PayrollCalculator(TaxTable table, bool employerPaysSdl = true)
{
    public Payslip Calculate(Employee employee, PeriodInput input, YearToDate ytd)
    {
        var periods = (int)employee.Frequency;
        if (input.Period < 1 || input.Period > periods)
            throw new ArgumentOutOfRangeException(nameof(input), $"Period must be between 1 and {periods}");

        var earnings = new List<PayslipLine>();
        void Earn(string code, string description, decimal amount)
        {
            if (amount != 0) earnings.Add(new PayslipLine(code, description, amount));
        }

        Earn(IrpCodes.Income, "Basic salary", input.BasicSalary);
        Earn(IrpCodes.Overtime, "Overtime", input.Overtime);
        Earn(IrpCodes.AnnualBonus, "Bonus", input.Bonus);
        Earn(IrpCodes.Commission, "Commission", input.Commission);
        Earn(IrpCodes.TravelAllowance, "Travel allowance", input.TravelAllowance);

        var cashGross = earnings.Sum(e => e.Amount);

        var travelInclusion = employee.TravelMostlyBusiness
            ? table.TravelAllowanceInclusion.MostlyBusiness
            : table.TravelAllowanceInclusion.Standard;
        var remuneration = cashGross - input.TravelAllowance
                           + input.TravelAllowance * travelInclusion
                           + input.EmployerMedicalContribution;

        var retirementDeductible = RetirementDeduction(input, remuneration, ytd, periods);
        var taxable = remuneration - retirementDeductible;

        var paye = CumulativePaye(employee, input.Period, periods, ytd, taxable, out var medicalCredit);
        var uif = Money.Round(Math.Min(remuneration - input.Commission, table.Uif.MonthlyCeiling * 12m / periods)
                              * table.Uif.Rate);
        uif = Math.Max(0m, uif);
        var sdl = employerPaysSdl ? Money.Round(remuneration * table.Sdl.Rate) : 0m;

        var deductions = new List<PayslipLine>
        {
            new(IrpCodes.Paye, "PAYE", paye),
            new(IrpCodes.Uif, "UIF", uif),
        };
        if (input.EmployeeRetirementContribution > 0)
            deductions.Add(new(IrpCodes.RetirementContribution, "Pension fund", input.EmployeeRetirementContribution));
        if (input.EmployeeMedicalContribution > 0)
            deductions.Add(new(IrpCodes.MedicalContribution, "Medical aid", input.EmployeeMedicalContribution));

        var employer = new List<PayslipLine> { new(IrpCodes.Uif, "UIF (employer)", uif) };
        if (sdl > 0) employer.Add(new(IrpCodes.Sdl, "SDL", sdl));
        if (input.EmployerMedicalContribution > 0)
            employer.Add(new(IrpCodes.MedicalFringeBenefit, "Medical aid (employer)", input.EmployerMedicalContribution));

        var net = cashGross - deductions.Sum(d => d.Amount);

        var newYtd = new YearToDate(
            Taxable: ytd.Taxable + taxable,
            Paye: ytd.Paye + paye,
            Uif: ytd.Uif + uif,
            RetirementDeducted: ytd.RetirementDeducted + retirementDeductible,
            MedicalCredits: ytd.MedicalCredits + medicalCredit);

        return new Payslip(employee, table.TaxYear, input.Period, earnings, deductions, employer,
            cashGross, taxable, net, newYtd);
    }

    private decimal RetirementDeduction(PeriodInput input, decimal remuneration, YearToDate ytd, int periods)
    {
        if (input.EmployeeRetirementContribution <= 0) return 0m;
        var percentCap = remuneration * table.Retirement.DeductiblePercent;
        var annualRoom = Math.Max(0m, table.Retirement.AnnualCap - ytd.RetirementDeducted);
        return Math.Min(input.EmployeeRetirementContribution, Math.Min(percentCap, annualRoom));
    }

    private decimal CumulativePaye(Employee employee, int period, int periods, YearToDate ytd,
        decimal taxableThisPeriod, out decimal medicalCreditThisPeriod)
    {
        var age = IncomeTax.AgeAt(employee.DateOfBirth, table.YearEnd);
        var ytdTaxable = ytd.Taxable + taxableThisPeriod;
        var annualised = ytdTaxable * periods / period;

        var annualTax = IncomeTax.Annual(annualised, age, table);
        var ytdLiability = annualTax * period / periods;

        // Medical credits accrue per period and can only reduce tax, never create a refund.
        var periodCredit = IncomeTax.MonthlyMedicalCredit(employee.MedicalAidMembers, table) * 12m / periods;
        var creditsAvailable = ytd.MedicalCredits + periodCredit;
        var creditsUsed = Math.Min(creditsAvailable, ytdLiability);
        medicalCreditThisPeriod = creditsUsed - ytd.MedicalCredits;

        var paye = Money.Round(ytdLiability - creditsUsed) - ytd.Paye;

        // Over-deductions earlier in the year are refunded through payroll, but only up to what was deducted.
        return Math.Max(-ytd.Paye, paye);
    }
}
