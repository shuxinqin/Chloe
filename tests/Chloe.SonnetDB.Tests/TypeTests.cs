using Xunit;

namespace Chloe.SonnetDB.Tests;

public class TypeTests : TestDatabase
{
    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("decimal")]
    [InlineData("bool")]
    [InlineData("string")]
    [InlineData("guid")]
    [InlineData("datetime")]
    [InlineData("datetime-local")]
    [InlineData("datetime-utc")]
    [InlineData("enum")]
    [InlineData("nullable-enum")]
    [InlineData("nullable-null")]
    public void Framework_mapping_types_round_trip(string type)
    {
        switch (type)
        {
            case "byte": RoundTrip(byte.MaxValue); break;
            case "sbyte": RoundTrip(sbyte.MinValue); break;
            case "short": RoundTrip(short.MinValue); break;
            case "ushort": RoundTrip(ushort.MaxValue); break;
            case "int": RoundTrip(int.MinValue); break;
            case "uint": RoundTrip(uint.MaxValue); break;
            case "long": RoundTrip(long.MaxValue); break;
            case "ulong": RoundTrip(ulong.MaxValue); break;
            case "float": RoundTrip(12.5f); break;
            case "double": RoundTrip(1.2345678901234567d); break;
            case "decimal": RoundTrip(12345.125m); break;
            case "bool": RoundTrip(true); break;
            case "string": RoundTrip("O'Brien 中文 😀"); break;
            case "guid": RoundTrip(Guid.Parse("b9d63b0b-c6a2-4f25-978c-6ba4e088c9e1")); break;
            case "datetime": RoundTrip(new DateTime(2024, 2, 29, 12, 34, 56, 456)); break;
            case "datetime-local": RoundTrip(new DateTime(2024, 2, 29, 12, 34, 56, 456, DateTimeKind.Local)); break;
            case "datetime-utc": RoundTrip(new DateTime(2024, 2, 29, 12, 34, 56, 456, DateTimeKind.Utc)); break;
            case "enum": RoundTrip(State.Disabled); break;
            case "nullable-enum": RoundTrip<State?>(State.Enabled); break;
            case "nullable-null": RoundTrip<int?>(null); break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private void RoundTrip<T>(T value)
    {
        CreateTable<ScalarRow<T>>();
        var entity = Context.Insert(new ScalarRow<T> { Value = value });
        Assert.True(entity.Id > 0);
        Assert.Equal(value, Context.Query<ScalarRow<T>>().First().Value);
        using var reopened = new SonnetDBContext(ConnectionFactory);
        Assert.Equal(value, reopened.Query<ScalarRow<T>>().First().Value);
        if (value is DateTime dateTime && dateTime.Kind == DateTimeKind.Local)
        {
            // SQL DATETIME preserves the clock value; reader Kind is a separate
            // driver/protocol contract. Verify the same normalization in each path.
            Assert.Equal(1, Context.Query<ScalarRow<DateTime>>().Where(a => a.Value == dateTime).Count());
            Context.InsertRange(new List<ScalarRow<DateTime>>
            {
                new() { Value = dateTime }, new() { Value = dateTime.AddHours(1) }
            });
            Assert.Equal(new[] { dateTime, dateTime, dateTime.AddHours(1) }, Context.Query<ScalarRow<DateTime>>().OrderBy(a => a.Id).Select(a => a.Value).ToList());
            DateTime updated = dateTime.AddHours(2);
            Assert.Equal(1, Context.Update<ScalarRow<DateTime>>(a => a.Id == entity.Id, a => new ScalarRow<DateTime> { Value = updated }));
            Assert.Equal(updated, Context.Query<ScalarRow<DateTime>>().First(a => a.Id == entity.Id).Value);
        }
    }

    [Fact]
    public void Binary_values_and_null_are_preserved()
    {
        CreateTable<ScalarRow<byte[]?>>();
        byte[] bytes = { 0, 1, 127, 128, 255 };
        Context.Insert(new ScalarRow<byte[]?> { Value = bytes });
        Context.Insert(new ScalarRow<byte[]?> { Value = null });
        var rows = Context.Query<ScalarRow<byte[]?>>().OrderBy(a => a.Id).ToList();
        Assert.Equal(bytes, rows[0].Value);
        Assert.Null(rows[1].Value);
    }

    [Fact]
    public void Nullable_primitive_values_preserve_null_and_value()
    {
        CreateTable<ScalarRow<bool?>>();
        Context.Insert(new ScalarRow<bool?> { Value = null });
        Context.Insert(new ScalarRow<bool?> { Value = false });
        Context.Insert(new ScalarRow<bool?> { Value = true });
        Assert.Equal(new bool?[] { null, false, true }, Context.Query<ScalarRow<bool?>>().OrderBy(a => a.Id).Select(a => a.Value).ToList());
    }

    [Fact]
    public void Decimal_parameters_round_trip_without_precision_loss()
    {
        // SonnetDB's native decimal parameter defect is tracked in #235. The provider
        // must preserve the value through an equivalent SQL conversion, if available.
        RoundTrip(123456789.1234567890123456789m);
    }

    [Fact]
    public void UInt64_full_range_works_in_parameters_updates_and_bulk_insert()
    {
        CreateTable<ScalarRow<ulong>>();
        ulong maximum = ulong.MaxValue;
        var entity = Context.Insert(new ScalarRow<ulong> { Value = maximum });
        Assert.Equal(1, Context.Query<ScalarRow<ulong>>().Where(a => a.Value == maximum).Count());
        Assert.Equal(1, Context.Update<ScalarRow<ulong>>(a => a.Id == entity.Id, a => new ScalarRow<ulong> { Value = a.Value - 1UL }));
        Assert.Equal(maximum - 1, Context.Query<ScalarRow<ulong>>().First().Value);
        Context.InsertRange(new List<ScalarRow<ulong>> { new() { Value = maximum }, new() { Value = maximum - 2 } });
        Assert.Equal(new[] { maximum - 1, maximum, maximum - 2 }, Context.Query<ScalarRow<ulong>>().OrderBy(a => a.Id).Select(a => a.Value).ToList());
    }

    [Fact]
    public void Nullable_decimal_parameters_preserve_precision_and_null()
    {
        CreateTable<ScalarRow<decimal?>>();
        decimal? precise = 123456789.1234567890123456789m;
        Context.Insert(new ScalarRow<decimal?> { Value = precise });
        Context.Insert(new ScalarRow<decimal?> { Value = null });
        Assert.Equal(1, Context.Query<ScalarRow<decimal?>>().Where(a => a.Value == precise).Count());
        Assert.Equal(new[] { precise, null }, Context.Query<ScalarRow<decimal?>>().OrderBy(a => a.Id).Select(a => a.Value).ToList());
    }

    [Theory]
    [InlineData("double", 1.9, 1UL)]
    [InlineData("double", 2.9, 2UL)]
    [InlineData("double", -0.9, 0UL)]
    [InlineData("float", 2.9, 2UL)]
    [InlineData("decimal", 2.9, 2UL)]
    [InlineData("decimal", -0.9, 0UL)]
    public void Numeric_to_UInt64_cast_truncates_fraction_instead_of_rounding(string type, double value, ulong expected)
    {
        switch (type)
        {
            case "double":
                CreateTable<ScalarRow<double>>();
                Context.Insert(new ScalarRow<double> { Value = value });
                Assert.Equal(expected, Context.Query<ScalarRow<double>>().Select(a => (ulong)a.Value).First());
                break;
            case "float":
                CreateTable<ScalarRow<float>>();
                Context.Insert(new ScalarRow<float> { Value = (float)value });
                Assert.Equal(expected, Context.Query<ScalarRow<float>>().Select(a => (ulong)a.Value).First());
                break;
            case "decimal":
                CreateTable<ScalarRow<decimal>>();
                Context.Insert(new ScalarRow<decimal> { Value = (decimal)value });
                Assert.Equal(expected, Context.Query<ScalarRow<decimal>>().Select(a => (ulong)a.Value).First());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
