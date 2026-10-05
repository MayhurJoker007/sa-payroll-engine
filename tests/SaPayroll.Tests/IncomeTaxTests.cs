using SaPayroll.Core;

namespace SaPayroll.Tests;

public class IncomeTaxTests
{
    private static readonly TaxTable Table = TaxTable.ForYear(2026);

    [Theory]
    [InlineData(95_750, 0)]                 // tax threshold under 65
    [InlineData(237_100, 25_443)]           // top of the first bracket
    [InlineData(500_000, 100_272)]          // 77 362 + 31% of 129 500 - 17 235
    [InlineData(2_000_000, 709_604)]        // 644 489 + 45% of 183 000 - 17 235
    public void Annual_tax_for_under_65s(decimal income, decimal expected)
    {
        Assert.Equal(expected, IncomeTax.Annual(income, 40, Table));
    }

    [Fact]
    public void Tertiary_rebate_applies_from_75()
    {
        var at74 = IncomeTax.Annual(300_000m, 74, Table);
        var at75 = IncomeTax.Annual(300_000m, 75, Table);
        Assert.Equal(3_145m, at74 - at75);
    }

    [Theory]
    [InlineData("2025-03-01", 2026)]
    [InlineData("2026-02-28", 2026)]
    [InlineData("2026-03-01", 2027)]
    public void Tax_year_runs_march_to_february(string date, int expected)
    {
        Assert.Equal(expected, TaxTable.TaxYearOf(DateOnly.Parse(date)));
    }

    [Fact]
    public void Age_is_taken_on_the_last_day_of_the_tax_year()
    {
        Assert.Equal(64, IncomeTax.AgeAt(new DateOnly(1961, 3, 1), Table.YearEnd));
        Assert.Equal(65, IncomeTax.AgeAt(new DateOnly(1961, 2, 28), Table.YearEnd));
    }

    [Fact]
    public void Unknown_tax_year_throws()
    {
        Assert.Throws<ArgumentException>(() => TaxTable.ForYear(1999));
    }
}
