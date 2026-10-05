namespace SaPayroll.Core;

/// <summary>SARS source codes used on payslip lines and the IRP5.</summary>
public static class IrpCodes
{
    public const string Income = "3601";
    public const string Commission = "3606";
    public const string Overtime = "3607";
    public const string TravelAllowance = "3701";
    public const string MedicalFringeBenefit = "3810";
    public const string AnnualBonus = "3605";
    public const string GrossRemuneration = "3699";

    public const string RetirementContribution = "4001";
    public const string MedicalContribution = "4005";
    public const string Paye = "4102";
    public const string MedicalCredit = "4116";
    public const string Uif = "4141";
    public const string Sdl = "4142";
}
