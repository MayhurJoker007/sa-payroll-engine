namespace SaPayroll.Core;

public static class IncomeTax
{
    /// <summary>Annual tax on taxable income after rebates, before medical credits.</summary>
    public static decimal Annual(decimal taxableIncome, int ageAtYearEnd, TaxTable table)
    {
        if (taxableIncome <= 0) return 0m;

        var bracket = table.Brackets.Last(b => taxableIncome > b.From || b.From == 0);
        var tax = bracket.BaseTax + (taxableIncome - bracket.From) * bracket.Rate;

        var rebate = table.Rebates.Primary;
        if (ageAtYearEnd >= 65) rebate += table.Rebates.Secondary;
        if (ageAtYearEnd >= 75) rebate += table.Rebates.Tertiary;

        return Math.Max(0m, tax - rebate);
    }

    /// <summary>Monthly section 6A medical scheme fees tax credit.</summary>
    public static decimal MonthlyMedicalCredit(int membersOnScheme, TaxTable table)
    {
        if (membersOnScheme <= 0) return 0m;
        var credits = table.MedicalCredits;
        var firstTwo = Math.Min(membersOnScheme, 2) * credits.MainAndFirstDependant;
        var rest = Math.Max(0, membersOnScheme - 2) * credits.AdditionalDependant;
        return firstTwo + rest;
    }

    public static int AgeAt(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (dateOfBirth > on.AddYears(-age)) age--;
        return age;
    }
}
