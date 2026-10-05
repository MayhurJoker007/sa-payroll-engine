namespace SaPayroll.Core;

/// <summary>Year-end certificate totals per SARS source code, built from a year's payslips.</summary>
public sealed record Irp5(string EmployeeNumber, int TaxYear, IReadOnlyDictionary<string, decimal> Codes)
{
    public static Irp5 From(IReadOnlyCollection<Payslip> payslips)
    {
        if (payslips.Count == 0) throw new ArgumentException("No payslips", nameof(payslips));
        var first = payslips.First();
        if (payslips.Any(p => p.Employee.EmployeeNumber != first.Employee.EmployeeNumber || p.TaxYear != first.TaxYear))
            throw new ArgumentException("Payslips must be for one employee and one tax year", nameof(payslips));

        var codes = new SortedDictionary<string, decimal>();
        void Add(string code, decimal amount)
        {
            if (amount == 0) return;
            codes[code] = codes.GetValueOrDefault(code) + amount;
        }

        foreach (var p in payslips)
        {
            foreach (var e in p.Earnings) Add(e.Code, e.Amount);
            foreach (var d in p.Deductions) Add(d.Code, d.Amount);
            Add(IrpCodes.MedicalFringeBenefit, p.EmployerContribution(IrpCodes.MedicalFringeBenefit));
            Add(IrpCodes.Sdl, p.EmployerContribution(IrpCodes.Sdl));
        }

        // UIF on the IRP5 is the employee and employer share together.
        if (codes.ContainsKey(IrpCodes.Uif)) codes[IrpCodes.Uif] *= 2;

        var credits = payslips.MaxBy(p => p.Period)!.YearToDate.MedicalCredits;
        Add(IrpCodes.MedicalCredit, Money.Round(credits));

        var incomeCodes = new[]
        {
            IrpCodes.Income, IrpCodes.Overtime, IrpCodes.AnnualBonus, IrpCodes.Commission,
            IrpCodes.TravelAllowance, IrpCodes.MedicalFringeBenefit,
        };
        codes[IrpCodes.GrossRemuneration] = incomeCodes.Sum(c => codes.GetValueOrDefault(c));

        return new Irp5(first.Employee.EmployeeNumber, first.TaxYear, codes);
    }
}
