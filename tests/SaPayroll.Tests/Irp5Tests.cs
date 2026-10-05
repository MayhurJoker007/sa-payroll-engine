using SaPayroll.Core;

namespace SaPayroll.Tests;

public class Irp5Tests
{
    [Fact]
    public void Totals_a_year_of_payslips_by_source_code()
    {
        var table = TaxTable.ForYear(2026);
        var calc = new PayrollCalculator(table);
        var employee = new Employee("E002", "Lerato Dlamini", new DateOnly(1990, 1, 5), MedicalAidMembers: 2);

        var ytd = YearToDate.Empty;
        var slips = new List<Payslip>();
        for (var period = 1; period <= 12; period++)
        {
            var slip = calc.Calculate(employee, new PeriodInput
            {
                Period = period,
                BasicSalary = 40_000m,
                TravelAllowance = 3_000m,
                EmployerMedicalContribution = 2_000m,
            }, ytd);
            slips.Add(slip);
            ytd = slip.YearToDate;
        }

        var irp5 = Irp5.From(slips);

        Assert.Equal(480_000m, irp5.Codes[IrpCodes.Income]);
        Assert.Equal(36_000m, irp5.Codes[IrpCodes.TravelAllowance]);
        Assert.Equal(24_000m, irp5.Codes[IrpCodes.MedicalFringeBenefit]);
        Assert.Equal(540_000m, irp5.Codes[IrpCodes.GrossRemuneration]);
        Assert.Equal(ytd.Paye, irp5.Codes[IrpCodes.Paye]);
        Assert.Equal(177.12m * 12 * 2, irp5.Codes[IrpCodes.Uif]);
        Assert.Equal(728m * 12, irp5.Codes[IrpCodes.MedicalCredit]);
    }

    [Fact]
    public void Refuses_payslips_for_different_employees()
    {
        var calc = new PayrollCalculator(TaxTable.ForYear(2026));
        var a = calc.Calculate(new Employee("A", "A", new DateOnly(1990, 1, 1)),
            new PeriodInput { Period = 1, BasicSalary = 10_000m }, YearToDate.Empty);
        var b = calc.Calculate(new Employee("B", "B", new DateOnly(1990, 1, 1)),
            new PeriodInput { Period = 1, BasicSalary = 10_000m }, YearToDate.Empty);

        Assert.Throws<ArgumentException>(() => Irp5.From([a, b]));
    }
}
