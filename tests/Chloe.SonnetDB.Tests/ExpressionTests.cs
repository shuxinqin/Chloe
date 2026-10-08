using Xunit;

namespace Chloe.SonnetDB.Tests;

public class ExpressionTests : TestDatabase
{
    [Fact]
    public void String_methods_run_on_column_values()
    {
        CreateTable<Row>();
        Context.Insert(new Row { Name = "  Abcd中文  ", Value = 1, Created = DateTime.Today });
        char[] spaces = { ' ' };
        var result = Context.Query<Row>().Select(a => new
        {
            Length = a.Name!.Length,
            Lower = a.Name.ToLower(), Upper = a.Name.ToUpper(), Trim = a.Name.Trim(),
            TrimStart = a.Name.TrimStart(spaces), TrimEnd = a.Name.TrimEnd(spaces),
            Substring = a.Name.Substring(2), Middle = a.Name.Substring(3, 2),
            Replace = a.Name.Replace("Ab", "xy"), Contains = a.Name.Contains("中文"),
            StartsWith = a.Name.StartsWith("  A"), EndsWith = a.Name.EndsWith("文  "),
            Empty = string.IsNullOrEmpty(a.Name)
        }).First();
        Assert.Equal(10, result.Length);
        Assert.Equal("  abcd中文  ", result.Lower);
        Assert.Equal("  ABCD中文  ", result.Upper);
        Assert.Equal("Abcd中文", result.Trim);
        Assert.Equal("Abcd中文  ", result.TrimStart);
        Assert.Equal("  Abcd中文", result.TrimEnd);
        Assert.Equal("Abcd中文  ", result.Substring);
        Assert.Equal("bc", result.Middle);
        Assert.Equal("  xycd中文  ", result.Replace);
        Assert.True(result.Contains && result.StartsWith && result.EndsWith);
        Assert.False(result.Empty);
    }

    [Fact]
    public void String_concatenation_preserves_csharp_null_semantics()
    {
        SeedRows();
        var result = Context.Query<Row>().OrderBy(a => a.Value).Select(a => a.Name + ":" + a.Value).ToList();
        Assert.Equal(new[] { "Alpha:10", "Beta:20", "Gamma:30", ":40" }, result);
        Assert.Equal("", Context.Query<Row>().Where(a => a.Name == null).Select(a => a.Name + a.AlternateName).First());
    }

    [Fact]
    public void Arithmetic_integer_division_modulo_bitwise_cast_and_parse()
    {
        CreateTable<Row>();
        Context.Insert(new Row { Name = "7", Value = 7, Created = DateTime.Today });
        var result = Context.Query<Row>().Select(a => new
        {
            Add = a.Value + 2, Subtract = a.Value - 2, Multiply = a.Value * 2,
            Divide = a.Value / 2, Modulo = a.Value % 2, Negate = -a.Value,
            And = a.Value & 3, Or = a.Value | 8,
            FloatingDivide = (double)a.Value / 2, Text = a.Value.ToString(), Parsed = int.Parse(a.Name!)
        }).First();
        Assert.Equal(9, result.Add);
        Assert.Equal(5, result.Subtract);
        Assert.Equal(14, result.Multiply);
        Assert.Equal(3, result.Divide);
        Assert.Equal(1, result.Modulo);
        Assert.Equal(-7, result.Negate);
        Assert.Equal(3, result.And);
        Assert.Equal(15, result.Or);
        Assert.Equal(3.5, result.FloatingDivide);
        Assert.Equal("7", result.Text);
        Assert.Equal(7, result.Parsed);
    }

    [Fact]
    public void Int64_division_keeps_precision_near_maximum_value()
    {
        CreateTable<ScalarRow<long>>();
        Context.Insert(new ScalarRow<long> { Value = long.MaxValue });
        Assert.Equal(long.MaxValue / 2, Context.Query<ScalarRow<long>>().Select(a => a.Value / 2).First());
        Context.Update<ScalarRow<long>>(a => true, a => new ScalarRow<long> { Value = 7 });
        var bitwise = Context.Query<ScalarRow<long>>().Select(a => new { And = a.Value & 3L, Or = a.Value | 8L }).First();
        Assert.Equal(3L, bitwise.And);
        Assert.Equal(15L, bitwise.Or);
    }

    [Fact]
    public void Null_comparison_coalesce_boolean_projection_and_case_when()
    {
        SeedRows();
        Assert.Equal(1, Context.Query<Row>().Where(a => a.Name == null).Count());
        Assert.Equal(3, Context.Query<Row>().Where(a => a.Name != null).Count());
        Assert.Equal(1, Context.Query<Row>().Where(a => a.Name == a.AlternateName).Count());
        Assert.Equal(3, Context.Query<Row>().Where(a => a.Name != a.AlternateName).Count());
        var result = Context.Query<Row>().OrderBy(a => a.Value).Select(a => new
        {
            Name = a.Name ?? "missing", Number = a.Optional ?? -1,
            Flag = a.Enabled && a.Value > 15, Not = !a.Enabled,
            Ternary = a.Optional == null ? 100 : a.Optional.Value,
            Case = Case.When(a.Value > 20).Then("high").Else("low")
        }).ToList();
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma", "missing" }, result.Select(a => a.Name));
        Assert.Equal(new[] { -1, 4, 5, -1 }, result.Select(a => a.Number));
        Assert.Equal(new[] { false, false, true, false }, result.Select(a => a.Flag));
        Assert.Equal(new[] { false, true, false, true }, result.Select(a => a.Not));
        Assert.Equal(new[] { 100, 4, 5, 100 }, result.Select(a => a.Ternary));
        Assert.Equal(new[] { "low", "low", "high", "high" }, result.Select(a => a.Case));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/262")]
    public void Date_properties_and_calendar_additions_run_on_columns()
    {
        CreateTable<Row>();
        DateTime start = new(2024, 2, 29, 12, 34, 56, 456);
        Context.Insert(new Row { Name = "date", Created = start });
        var result = Context.Query<Row>().Select(a => new
        {
            Date = a.Created.Date, Year = a.Created.Year, Month = a.Created.Month, Day = a.Created.Day,
            Hour = a.Created.Hour, Minute = a.Created.Minute, Second = a.Created.Second,
            Millisecond = a.Created.Millisecond, Weekday = a.Created.DayOfWeek,
            AddYears = a.Created.AddYears(1), AddMonths = a.Created.AddMonths(1),
            AddDays = a.Created.AddDays(1), AddHours = a.Created.AddHours(1),
            AddMinutes = a.Created.AddMinutes(1), AddSeconds = a.Created.AddSeconds(1),
            AddMilliseconds = a.Created.AddMilliseconds(1)
        }).First();
        Assert.Equal(start.Date, result.Date);
        Assert.Equal(2024, result.Year);
        Assert.Equal(2, result.Month);
        Assert.Equal(29, result.Day);
        Assert.Equal(12, result.Hour);
        Assert.Equal(34, result.Minute);
        Assert.Equal(56, result.Second);
        Assert.Equal(456, result.Millisecond);
        Assert.Equal(DayOfWeek.Thursday, result.Weekday);
        Assert.Equal(start.AddYears(1), result.AddYears);
        Assert.Equal(start.AddMonths(1), result.AddMonths);
        Assert.Equal(start.AddDays(1), result.AddDays);
        Assert.Equal(start.AddHours(1), result.AddHours);
        Assert.Equal(start.AddMinutes(1), result.AddMinutes);
        Assert.Equal(start.AddSeconds(1), result.AddSeconds);
        Assert.Equal(start.AddMilliseconds(1), result.AddMilliseconds);
    }

    [Fact]
    public void Date_diff_functions_preserve_direction_and_units()
    {
        CreateTable<Row>();
        DateTime start = new(2024, 1, 1);
        Context.Insert(new Row { Created = start });
        DateTime end = start.AddDays(1).AddMilliseconds(2);
        var result = Context.Query<Row>().Select(a => new
        {
            Years = Sql.DiffYears(a.Created, end), Months = Sql.DiffMonths(a.Created, end),
            Days = Sql.DiffDays(a.Created, end), Hours = Sql.DiffHours(a.Created, end),
            Minutes = Sql.DiffMinutes(a.Created, end), Seconds = Sql.DiffSeconds(a.Created, end),
            Milliseconds = Sql.DiffMilliseconds(a.Created, end),
            Reversed = Sql.DiffDays(end, a.Created)
        }).First();
        Assert.Equal(0, result.Years);
        Assert.Equal(0, result.Months);
        Assert.Equal(1, result.Days);
        Assert.Equal(24, result.Hours);
        Assert.Equal(1440, result.Minutes);
        Assert.Equal(86400, result.Seconds);
        Assert.Equal(86400002, result.Milliseconds);
        Assert.Equal(-1, result.Reversed);
    }

    [Fact]
    public void Date_functions_keep_datetime_type_in_nested_expressions_predicates_and_updates()
    {
        CreateTable<Row>();
        DateTime start = new(2024, 2, 29, 12, 34, 56, 456);
        DateTime day = start.Date;
        DateTime nextDay = day.AddDays(1);
        Context.Insert(new Row { Created = start });
        Assert.Equal(1, Context.Query<Row>().Where(a => a.Created.Date == day).Count());
        Assert.Equal(1, Context.Query<Row>().Where(a => a.Created.Date.AddDays(1) == nextDay).Count());
        var result = Context.Query<Row>().Select(a => new
        {
            Year = a.Created.Date.Year,
            Hour = a.Created.Date.AddHours(2).Hour,
            NextDay = a.Created.AddDays(1).Date,
            Difference = Sql.DiffHours(a.Created.Date, a.Created.Date.AddHours(2))
        }).First();
        Assert.Equal(2024, result.Year);
        Assert.Equal(2, result.Hour);
        Assert.Equal(nextDay, result.NextDay);
        Assert.Equal(2, result.Difference);
        Assert.Equal(1, Context.Update<Row>(a => a.Created.Date == day, a => new Row { Created = a.Created.Date.AddHours(3) }));
        Assert.Equal(day.AddHours(3), Context.Query<Row>().First().Created);
    }

    [Fact]
    public void Date_diff_microseconds_uses_subsecond_units()
    {
        CreateTable<Row>();
        DateTime start = new(2024, 1, 1);
        Context.Insert(new Row { Created = start });
        DateTime end = start.AddMilliseconds(2);
        Assert.Equal(2000, Context.Query<Row>().Select(a => Sql.DiffMicroseconds(a.Created, end)).First());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/262")]
    public void Server_current_time_today_and_utc_time_are_live_values()
    {
        SeedRows();
        DateTime before = DateTime.Now.AddSeconds(-5);
        DateTime utcBefore = DateTime.UtcNow.AddSeconds(-5);
        var result = Context.Query<Row>().Select(a => new { Now = DateTime.Now, Utc = DateTime.UtcNow, Today = DateTime.Today }).First();
        Assert.InRange(result.Now, before, DateTime.Now.AddSeconds(5));
        Assert.InRange(result.Utc, utcBefore, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(result.Now.Date, result.Today);
    }
}
