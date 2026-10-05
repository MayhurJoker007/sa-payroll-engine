namespace SaPayroll.Core;

public enum PayFrequency
{
    Monthly = 12,
    Weekly = 52,
}

public sealed record Employee(
    string EmployeeNumber,
    string FullName,
    DateOnly DateOfBirth,
    PayFrequency Frequency = PayFrequency.Monthly,
    int MedicalAidMembers = 0,
    bool TravelMostlyBusiness = false);

/// <summary>Everything that was earned or deducted in one pay period.</summary>
public sealed record PeriodInput
{
    public required int Period { get; init; }          // 1-based period in the tax year
    public decimal BasicSalary { get; init; }
    public decimal Overtime { get; init; }
    public decimal Bonus { get; init; }
    public decimal Commission { get; init; }
    public decimal TravelAllowance { get; init; }
    public decimal EmployerMedicalContribution { get; init; }   // taxable fringe benefit
    public decimal EmployeeMedicalContribution { get; init; }
    public decimal EmployeeRetirementContribution { get; init; }
}

/// <summary>Running totals carried from one period to the next.</summary>
public sealed record YearToDate(
    decimal Taxable = 0m,
    decimal Paye = 0m,
    decimal Uif = 0m,
    decimal RetirementDeducted = 0m,
    decimal MedicalCredits = 0m)
{
    public static readonly YearToDate Empty = new();
}

public sealed record PayslipLine(string Code, string Description, decimal Amount);

public sealed record Payslip(
    Employee Employee,
    int TaxYear,
    int Period,
    IReadOnlyList<PayslipLine> Earnings,
    IReadOnlyList<PayslipLine> Deductions,
    IReadOnlyList<PayslipLine> EmployerContributions,
    decimal Gross,
    decimal TaxableThisPeriod,
    decimal NetPay,
    YearToDate YearToDate)
{
    public decimal Deduction(string code) => Deductions.Where(d => d.Code == code).Sum(d => d.Amount);
    public decimal EmployerContribution(string code) =>
        EmployerContributions.Where(d => d.Code == code).Sum(d => d.Amount);
}
