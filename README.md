# SA Payroll Engine (C# / .NET 9)

A South African payroll calculation library: PAYE using the cumulative method, UIF, SDL, medical tax credits,
retirement fund deductions, travel allowance inclusion and IRP5 source code totals.

I've built payroll logic like this at work on a different stack (NiFi + PostgreSQL + Groovy). This is a clean
rewrite in C# from the public SARS rules, so I could structure it properly as a library with tests around every
rule. No code or data from that system is in here.

## How PAYE is worked out

Each period:

1. Add up remuneration: salary, overtime, bonus, commission, the taxable part of the travel allowance
   (80%, or 20% if the employee's travel is mostly business) and the employer's medical aid contribution as a
   fringe benefit.
2. Subtract retirement fund contributions, capped at 27.5% of remuneration and R350 000 a year.
3. Annualise the year-to-date taxable income: `ytd * periods_in_year / periods_worked`.
4. Run that through the tax brackets and subtract the rebates for the employee's age at the end of the tax year.
5. Scale back to the periods worked, take off medical credits accrued so far, and subtract the PAYE already
   deducted. What's left is this period's PAYE.

Because each period recalculates from the year-to-date figures, variable pay doesn't leave anyone over- or
under-taxed by February. There's a test that runs twelve months with a bonus in month nine and checks the total
PAYE equals the tax on the annual income to the cent.

All money is `decimal` and only the final PAYE figure is rounded, so rounding never adds up across periods.

## Tax tables

Rates live in `src/SaPayroll.Core/TaxTables/{year}.json` as embedded resources. The 2026 tax year
(1 March 2025 - 28 February 2026) is included. Adding a year is a new JSON file, with no code changes.

## Usage

```csharp
var table = TaxTable.ForYear(2026);
var calculator = new PayrollCalculator(table);
var employee = new Employee("E001", "Thabo Nkosi", new DateOnly(1995, 4, 12), MedicalAidMembers: 2);

var slip = calculator.Calculate(employee,
    new PeriodInput { Period = 1, BasicSalary = 32_000m, EmployeeRetirementContribution = 2_400m },
    YearToDate.Empty);

// carry slip.YearToDate into next month's call
```

There's also a small CLI that runs a JSON file of employees and periods and prints payslips plus IRP5 totals:

```
dotnet run --project src/SaPayroll.Cli -- samples/run.json
```

```
E001 Thabo Nkosi  |  2026 period 1
  Basic salary                     32 000,00
  Gross                            32 000,00
  PAYE                             -3 951,08
  UIF                                -177,12
  Pension fund                     -2 400,00
  Medical aid                      -2 100,00
  Net pay                          23 371,80
```

## Tests

```
dotnet test
```

The tests cover the brackets at their edges, the age rebates, the UIF ceiling, medical credits, retirement fund
caps, travel allowance inclusion, weekly vs monthly annualisation, the bonus case above and IRP5 totals.

## What's simplified

This is a calculation engine, not a full payroll system. It doesn't do:

- directive-based tax (lump sums, fixed-rate directives)
- the annual bonus "once-off" method. It uses straight cumulative averaging, which gives the same year-end total
  but a different month-by-month split
- employment that starts or ends mid-year (periods worked are assumed to start at period 1)
- ETI, fringe benefits other than medical aid, or the full list of UIF exclusions (only commission is excluded)

Check figures against the SARS tables or e-Filing before using it for real payslips.
