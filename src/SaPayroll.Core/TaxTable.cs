using System.Reflection;
using System.Text.Json;

namespace SaPayroll.Core;

public sealed record TaxBracket(decimal From, decimal BaseTax, decimal Rate);
public sealed record Rebates(decimal Primary, decimal Secondary, decimal Tertiary);
public sealed record MedicalCredits(decimal MainAndFirstDependant, decimal AdditionalDependant);
public sealed record UifSettings(decimal Rate, decimal MonthlyCeiling);
public sealed record SdlSettings(decimal Rate, decimal AnnualPayrollThreshold);
public sealed record RetirementSettings(decimal DeductiblePercent, decimal AnnualCap);
public sealed record TravelInclusion(decimal Standard, decimal MostlyBusiness);

/// <summary>SARS rates for one tax year (1 March to end of February), loaded from TaxTables/{year}.json.</summary>
public sealed record TaxTable(
    int TaxYear,
    string Description,
    IReadOnlyList<TaxBracket> Brackets,
    Rebates Rebates,
    MedicalCredits MedicalCredits,
    UifSettings Uif,
    SdlSettings Sdl,
    RetirementSettings Retirement,
    TravelInclusion TravelAllowanceInclusion)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static TaxTable ForYear(int taxYear)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = $"SaPayroll.Core.TaxTables.{taxYear}.json";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new ArgumentException($"No tax table for the {taxYear} tax year", nameof(taxYear));
        return JsonSerializer.Deserialize<TaxTable>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Could not read {name}");
    }

    /// <summary>The tax year a date falls in. 15 June 2025 is in the 2026 year.</summary>
    public static int TaxYearOf(DateOnly date) => date.Month >= 3 ? date.Year + 1 : date.Year;

    public DateOnly YearEnd => new DateOnly(TaxYear, 3, 1).AddDays(-1);
}
