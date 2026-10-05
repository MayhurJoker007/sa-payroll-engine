using System.Globalization;
using System.Text.Json;
using SaPayroll.Core;

// Usage: dotnet run --project src/SaPayroll.Cli -- samples/run.json
//
// Runs every period in the file in order for each employee, prints the payslips,
// and prints the IRP5 totals at the end.

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: SaPayroll.Cli <run.json>");
    return 1;
}

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var run = JsonSerializer.Deserialize<RunFile>(File.ReadAllText(args[0]), options)
          ?? throw new InvalidOperationException("empty run file");

var table = TaxTable.ForYear(run.TaxYear);
var calculator = new PayrollCalculator(table, run.EmployerPaysSdl);
var za = CultureInfo.GetCultureInfo("en-ZA");

foreach (var entry in run.Employees)
{
    var ytd = YearToDate.Empty;
    var slips = new List<Payslip>();
    foreach (var period in entry.Periods.OrderBy(p => p.Period))
    {
        var slip = calculator.Calculate(entry.Employee, period, ytd);
        slips.Add(slip);
        ytd = slip.YearToDate;
        Print(slip);
    }

    var irp5 = Irp5.From(slips);
    Console.WriteLine($"IRP5 {irp5.TaxYear} - {entry.Employee.EmployeeNumber} {entry.Employee.FullName}");
    foreach (var (code, amount) in irp5.Codes)
        Console.WriteLine($"  {code}  {amount.ToString("N2", za),14}");
    Console.WriteLine();
}

return 0;

void Print(Payslip slip)
{
    Console.WriteLine($"{slip.Employee.EmployeeNumber} {slip.Employee.FullName}  |  {slip.TaxYear} period {slip.Period}");
    foreach (var line in slip.Earnings)
        Console.WriteLine($"  {line.Description,-28}{line.Amount.ToString("N2", za),14}");
    Console.WriteLine($"  {"Gross",-28}{slip.Gross.ToString("N2", za),14}");
    foreach (var line in slip.Deductions)
        Console.WriteLine($"  {line.Description,-28}{(-line.Amount).ToString("N2", za),14}");
    Console.WriteLine($"  {"Net pay",-28}{slip.NetPay.ToString("N2", za),14}");
    Console.WriteLine();
}

record RunFile(int TaxYear, bool EmployerPaysSdl, List<EmployeeRun> Employees);
record EmployeeRun(Employee Employee, List<PeriodInput> Periods);
