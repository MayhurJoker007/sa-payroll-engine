using SaPayroll.Core;

namespace SaPayroll.Tests;

public class PayrollCalculatorTests
{
    private static readonly TaxTable Table = TaxTable.ForYear(2026);
    private static readonly Employee Thabo = new("E001", "Thabo Nkosi", new DateOnly(1995, 4, 12));

    private static Payslip Run(Employee employee, PeriodInput input, YearToDate? ytd = null) =>
        new PayrollCalculator(Table).Calculate(employee, input, ytd ?? YearToDate.Empty);

    [Fact]
    public void Monthly_paye_matches_the_annual_tax_divided_by_twelve()
    {
        // R360 000 a year: 42 678 + 26% of (360 000 - 237 100) - 17 235 rebate = 57 397
        var slip = Run(Thabo, new PeriodInput { Period = 1, BasicSalary = 30_000m });

        Assert.Equal(4_783.08m, slip.Deduction(IrpCodes.Paye));
    }

    [Fact]
    public void Income_under_the_tax_threshold_pays_no_paye()
    {
        var slip = Run(Thabo, new PeriodInput { Period = 1, BasicSalary = 7_000m });

        Assert.Equal(0m, slip.Deduction(IrpCodes.Paye));
        Assert.Equal(70m, slip.Deduction(IrpCodes.Uif));
    }

    [Fact]
    public void Over_65s_get_the_secondary_rebate()
    {
        var pensioner = Thabo with { DateOfBirth = new DateOnly(1958, 6, 1) };
        var slip = Run(pensioner, new PeriodInput { Period = 1, BasicSalary = 12_000m });

        // 144 000 * 18% = 25 920, below primary + secondary rebate of 26 679
        Assert.Equal(0m, slip.Deduction(IrpCodes.Paye));
    }

    [Fact]
    public void Uif_is_capped_at_the_monthly_ceiling()
    {
        var slip = Run(Thabo, new PeriodInput { Period = 1, BasicSalary = 80_000m });

        Assert.Equal(177.12m, slip.Deduction(IrpCodes.Uif));
        Assert.Equal(177.12m, slip.EmployerContribution(IrpCodes.Uif));
        Assert.Equal(800m, slip.EmployerContribution(IrpCodes.Sdl));
    }

    [Fact]
    public void Medical_credits_reduce_paye()
    {
        var withFamily = Thabo with { MedicalAidMembers = 3 };
        var slip = Run(withFamily, new PeriodInput { Period = 1, BasicSalary = 30_000m });

        // 364 + 364 + 246 = 974
        Assert.Equal(4_783.08m - 974m, slip.Deduction(IrpCodes.Paye));
    }

    [Fact]
    public void Pension_contributions_are_deducted_before_tax()
    {
        var slip = Run(Thabo, new PeriodInput
        {
            Period = 1,
            BasicSalary = 30_000m,
            EmployeeRetirementContribution = 3_000m,
        });

        Assert.Equal(27_000m, slip.TaxableThisPeriod);
        Assert.Equal(4_003.08m, slip.Deduction(IrpCodes.Paye));
    }

    [Fact]
    public void Pension_deduction_is_limited_to_27_and_a_half_percent()
    {
        var slip = Run(Thabo, new PeriodInput
        {
            Period = 1,
            BasicSalary = 20_000m,
            EmployeeRetirementContribution = 8_000m,
        });

        Assert.Equal(20_000m - 5_500m, slip.TaxableThisPeriod);
    }

    [Theory]
    [InlineData(false, 4_000)]
    [InlineData(true, 1_000)]
    public void Travel_allowance_is_partly_taxable(bool mostlyBusiness, decimal expectedTaxablePortion)
    {
        var employee = Thabo with { TravelMostlyBusiness = mostlyBusiness };
        var slip = Run(employee, new PeriodInput { Period = 1, BasicSalary = 20_000m, TravelAllowance = 5_000m });

        Assert.Equal(20_000m + expectedTaxablePortion, slip.TaxableThisPeriod);
        Assert.Equal(25_000m, slip.Gross);
    }

    [Fact]
    public void A_bonus_is_spread_so_the_year_total_matches_the_annual_tax()
    {
        var calc = new PayrollCalculator(Table);
        var ytd = YearToDate.Empty;
        var totalPaye = 0m;
        for (var period = 1; period <= 12; period++)
        {
            var input = new PeriodInput
            {
                Period = period,
                BasicSalary = 30_000m,
                Bonus = period == 9 ? 30_000m : 0m,
            };
            var slip = calc.Calculate(Thabo, input, ytd);
            totalPaye += slip.Deduction(IrpCodes.Paye);
            ytd = slip.YearToDate;
        }

        // R390 000 for the year: 77 362 + 31% of 19 500 - 17 235 = 66 172
        Assert.Equal(66_172m, totalPaye);
        Assert.Equal(66_172m, ytd.Paye);
    }

    [Fact]
    public void Net_pay_is_gross_less_deductions()
    {
        var slip = Run(Thabo with { MedicalAidMembers = 1 }, new PeriodInput
        {
            Period = 1,
            BasicSalary = 25_000m,
            EmployeeMedicalContribution = 1_800m,
        });

        Assert.Equal(slip.Gross - slip.Deductions.Sum(d => d.Amount), slip.NetPay);
    }

    [Fact]
    public void Weekly_staff_are_annualised_over_52_weeks()
    {
        var weekly = Thabo with { Frequency = PayFrequency.Weekly };
        var slip = Run(weekly, new PeriodInput { Period = 1, BasicSalary = 7_000m });

        var expected = Math.Round(IncomeTax.Annual(7_000m * 52, 30, Table) / 52, 2);
        Assert.Equal(expected, slip.Deduction(IrpCodes.Paye));
    }

    [Fact]
    public void Rejects_a_period_outside_the_year()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Run(Thabo, new PeriodInput { Period = 13, BasicSalary = 1m }));
    }
}
