using Chloe.Annotations;
using Chloe.Infrastructure;
using System.Data;
using Xunit;

namespace Chloe.SonnetDB.Tests;

/// <summary>
/// These tests validate the repaired 5.0 database contracts through the complete provider.
/// Explicit legacy DATETIME and unsupported lock boundaries remain separate assertions.
/// </summary>
[Trait("Category", "ReleaseContract")]
public class GapContractTests : TestDatabase
{
    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/231")]
    public void Sequence_mapping_allocates_native_unique_values()
    {
        CreateTable<SequenceRow>();
        var first = Context.Insert(new SequenceRow());
        var second = Context.Insert(new SequenceRow());
        Assert.True(first.Id > 0);
        Assert.True(second.Id > first.Id);
        Context.InsertRange(new List<SequenceRow> { new(), new() });
        Assert.Equal(4, Context.Query<SequenceRow>().Count());
        Assert.Equal(4, Context.Query<SequenceRow>().Select(a => a.Id).ToList().Distinct().Count());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/232")]
    public void Schema_mapping_uses_qualified_relational_identity()
    {
        CreateTable<SchemaRow>();
        var inserted = Context.Insert(new SchemaRow());
        Assert.True(inserted.Id > 0);
        Assert.Equal(inserted.Id, Context.Query<SchemaRow>().First().Id);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/233")]
    public void Update_lock_is_supported_and_NoLock_remains_explicitly_rejected()
    {
        SeedRows();
        Context.Session.BeginTransaction(IsolationLevel.ReadCommitted);
        try { Assert.Equal(4, Context.Query<Row>(LockType.UpdLock).ToList().Count); }
        finally { Context.Session.RollbackTransaction(); }
        Assert.Throws<NotSupportedException>(() => Context.Query<Row>(LockType.NoLock).ToList());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/225")]
    public void Serializable_isolation_uses_native_transaction_contract()
    {
        CreateTable<Group>();
        Context.Session.BeginTransaction(IsolationLevel.Serializable);
        Assert.True(Context.Session.IsInTransaction);
        Assert.Equal(0, Context.Query<Group>().Count());
        Context.Session.RollbackTransaction();
        Assert.False(Context.Session.IsInTransaction);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/236")]
    public void Legacy_DbType_DateTime_rejects_submillisecond_values()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "PreciseDates", "Id INT AUTO_INCREMENT, Value DATETIME, PRIMARY KEY(Id)");
        command.CommandText = "INSERT INTO PreciseDates (Value) VALUES (@value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.DateTime;
        parameter.Value = new DateTime(2024, 2, 29).AddTicks(1);
        command.Parameters.Add(parameter);
        Assert.Throws<ArgumentException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/236")]
    public void Orm_DateTime2_preserves_submillisecond_ticks()
    {
        CreateTable<ScalarRow<DateTime>>();
        DateTime input = new DateTime(2024, 2, 29, 12, 34, 56, 456).AddTicks(7890);
        Context.Insert(new ScalarRow<DateTime> { Value = input });
        Context.Insert(() => new ScalarRow<DateTime> { Value = input });
        Context.InsertRange(new List<ScalarRow<DateTime>> { new() { Value = input } });
        Assert.Equal(3, Context.Query<ScalarRow<DateTime>>().Where(a => a.Value == input).Count());
        Assert.All(Context.Query<ScalarRow<DateTime>>().ToList(), a => Assert.Equal(input.Ticks, a.Value.Ticks));
        Assert.False(Context.Session.IsInTransaction);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/235")]
    public void Native_decimal_parameter_preserves_precision_5_0()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "PreciseDecimals", "Id INT AUTO_INCREMENT, Value DECIMAL(38,28), PRIMARY KEY(Id)");
        decimal input = 123456789.1234567890123456789m;
        command.CommandText = "INSERT INTO PreciseDecimals (Value) VALUES (@value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.Decimal;
        parameter.Precision = 38;
        parameter.Scale = 28;
        parameter.Value = input;
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "SELECT Value FROM PreciseDecimals";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(input, reader.GetDecimal(0));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/237")]
    public void Native_column_comments_are_available_in_ADO_metadata()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "CommentedRows", "Value INT");
        command.CommandText = "COMMENT ON COLUMN CommentedRows.Value IS 'column description'";
        command.ExecuteNonQuery();
        var metadata = ((System.Data.Common.DbConnection)connection).GetSchema("Columns");
        Assert.Contains(metadata.Rows.Cast<DataRow>(), a => a.ItemArray.Contains("column description"));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/251")]
    public void DateTimeOffset_mapping_preserves_UTC_ticks_and_zero_offset()
    {
        CreateTable<ScalarRow<DateTimeOffset>>();
        var input = new DateTimeOffset(2024, 2, 29, 12, 34, 56, TimeSpan.FromHours(8)).AddTicks(7890);
        Context.Insert(new ScalarRow<DateTimeOffset> { Value = input });
        var actual = Context.Query<ScalarRow<DateTimeOffset>>().First().Value;
        Assert.Equal(input.UtcTicks, actual.UtcTicks);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/252")]
    public void TimeSpan_mapping_preserves_negative_and_maximum_duration()
    {
        CreateTable<ScalarRow<TimeSpan>>();
        var input = TimeSpan.FromDays(-2).Add(TimeSpan.FromTicks(17));
        Context.Insert(new ScalarRow<TimeSpan> { Value = input });
        Context.InsertRange(new List<ScalarRow<TimeSpan>> { new() { Value = TimeSpan.MaxValue } });
        Assert.Equal(new[] { input, TimeSpan.MaxValue }, Context.Query<ScalarRow<TimeSpan>>().OrderBy(a => a.Id).Select(a => a.Value).ToList());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/253")]
    public void Keyless_table_mapping_supports_duplicates_and_nulls()
    {
        CreateTable<KeylessRow>();
        Context.Insert(new KeylessRow { Value = "same" });
        Context.InsertRange(new List<KeylessRow> { new() { Value = "same" }, new() { Value = null } });
        Assert.Equal(3, Context.Query<KeylessRow>().Count());
        Assert.Equal(2, Context.Query<KeylessRow>().Where(a => a.Value == "same").Count());
        Assert.Equal(1, Context.Query<KeylessRow>().Where(a => a.Value == null).Count());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/240")]
    public void Explicit_column_length_constraint_rejects_overflow()
    {
        CreateTable<LengthConstrainedRow>();
        Context.Insert(new LengthConstrainedRow { Value = "abc" });
        Assert.ThrowsAny<Exception>(() => Context.Insert(new LengthConstrainedRow { Value = "abcd" }));
        Assert.Equal("abc", Context.Query<LengthConstrainedRow>().First().Value);
        Assert.Equal(1, Context.Query<LengthConstrainedRow>().Count());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/264")]
    public void Explicit_decimal_constraints_are_enforced_by_database()
    {
        CreateTable<DecimalConstrainedRow>();
        CreateTable<CustomDecimalConstrainedRow>();
        CreateTable<CustomNumericConstrainedRow>();
        Assert.Throws<NotSupportedException>(() => CreateTable<DecimalPrecisionRow>());
        Assert.Throws<NotSupportedException>(() => CreateTable<DecimalScaleRow>());
        Context.Insert(new DecimalConstrainedRow { Value = 999.99m });
        Assert.ThrowsAny<Exception>(() => Context.Insert(new DecimalConstrainedRow { Value = 1.234m }));
        Assert.ThrowsAny<Exception>(() => Context.Insert(new DecimalConstrainedRow { Value = 1000m }));
        Assert.Equal(999.99m, Context.Query<DecimalConstrainedRow>().First().Value);
        Assert.Equal(1, Context.Query<DecimalConstrainedRow>().Count());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/254")]
    public void Outer_cast_of_aggregate_executes_on_database()
    {
        SeedRows();
        Assert.Equal(100d, Context.Query<Row>().Select(a => (double)Sql.Sum(a.Value)).First());
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SUM(Value) AS FLOAT) FROM Rows";
        Assert.Equal(100d, Convert.ToDouble(command.ExecuteScalar()));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/258")]
    public void Server_NewGuid_generates_independent_values_per_row()
    {
        CreateTable<ScalarRow<Guid>>();
        var clientValue = Guid.NewGuid();
        Context.Insert(new ScalarRow<Guid> { Value = clientValue });
        Context.Insert(new ScalarRow<Guid> { Value = clientValue });
        var generated = Context.Query<ScalarRow<Guid>>().Select(a => Guid.NewGuid()).ToList();
        Assert.Equal(2, generated.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, generated);
        Assert.Equal(2, Context.Update<ScalarRow<Guid>>(a => true, a => new ScalarRow<Guid> { Value = Guid.NewGuid() }));
        Assert.Equal(2, Context.Query<ScalarRow<Guid>>().Select(a => a.Value).ToList().Distinct().Count());
    }

    [Theory]
    [InlineData("and")]
    [InlineData("or")]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/259")]
    public void UInt64_bitwise_operations_preserve_full_unsigned_range(string operation)
    {
        CreateTable<ScalarRow<ulong>>();
        Context.Insert(new ScalarRow<ulong> { Value = ulong.MaxValue });
        if (operation == "and")
        {
            Assert.Equal(1UL << 63, Context.Query<ScalarRow<ulong>>().Select(a => a.Value & (1UL << 63)).First());
            Assert.Equal(1, Context.Update<ScalarRow<ulong>>(a => true, a => new ScalarRow<ulong> { Value = a.Value & 3UL }));
            Assert.Equal(3UL, Context.Query<ScalarRow<ulong>>().First().Value);
        }
        else
        {
            Assert.Equal(ulong.MaxValue, Context.Query<ScalarRow<ulong>>().Select(a => a.Value | 8UL).First());
            Assert.Equal(1, Context.Update<ScalarRow<ulong>>(a => true, a => new ScalarRow<ulong> { Value = a.Value | 8UL }));
            Assert.Equal(ulong.MaxValue, Context.Query<ScalarRow<ulong>>().First().Value);
        }
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/252")]
    public void DateTime_subtraction_returns_native_TimeSpan()
    {
        SeedRows();
        DateTime start = new(2024, 1, 1);
        var expected = Context.Query<Row>().OrderBy(a => a.Id).Select(a => a.Created).ToList().Select(a => a - start).ToList();
        Assert.Equal(expected, Context.Query<Row>().OrderBy(a => a.Id).Select(a => a.Created.Subtract(start)).ToList());
        Assert.Equal(expected, Context.Query<Row>().OrderBy(a => a.Id).Select(a => a.Created - start).ToList());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/260")]
    public void Server_UInt64_Parse_accepts_maximum_and_rejects_fraction()
    {
        CreateTable<ScalarRow<string>>();
        Context.Insert(new ScalarRow<string> { Value = "18446744073709551615" });
        Assert.Equal(ulong.MaxValue, Context.Query<ScalarRow<string>>().Select(a => ulong.Parse(a.Value)).First());
        Context.Update<ScalarRow<string>>(a => true, a => new ScalarRow<string> { Value = "2.9" });
        Assert.ThrowsAny<Exception>(() => Context.Query<ScalarRow<string>>().Select(a => ulong.Parse(a.Value)).ToList());
    }
}

[Table("SequenceRows")]
public class SequenceRow
{
    [Column(IsPrimaryKey = true), Sequence("SequenceRows_Id")]
    public int Id { get; set; }
}

[Table("SchemaRows", Schema = "analytics")]
public class SchemaRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
}

[Table("KeylessRows")]
public class KeylessRow
{
    public string? Value { get; set; }
}

[Table("LengthConstrainedRows")]
public class LengthConstrainedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [Column(Size = 3)]
    public string? Value { get; set; }
}

[Table("DecimalConstrainedRows")]
public class DecimalConstrainedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [Column(Precision = 5, Scale = 2)]
    public decimal Value { get; set; }
}

[Table("DecimalPrecisionRows")]
public class DecimalPrecisionRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [Column(Precision = 5)]
    public decimal Value { get; set; }
}

[Table("DecimalScaleRows")]
public class DecimalScaleRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [Column(Scale = 2)]
    public decimal Value { get; set; }
}

[Table("CustomDecimalConstrainedRows")]
public class CustomDecimalConstrainedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [DataType(" decimal (5,2) ")]
    public decimal Value { get; set; }
}

[Table("CustomNumericConstrainedRows")]
public class CustomNumericConstrainedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [DataType("NUMERIC(5,2)")]
    public decimal Value { get; set; }
}
