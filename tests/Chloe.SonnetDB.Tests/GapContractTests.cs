using Chloe.Annotations;
using Chloe.Infrastructure;
using System.Data;
using Xunit;

namespace Chloe.SonnetDB.Tests;

/// <summary>
/// These tests document database limitations and provider rejection contracts.
/// Passing these tests is evidence of a tracked gap, not implementation of that capability.
/// </summary>
[Trait("Category", "GapContract")]
public class GapContractTests : TestDatabase
{
    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/231")]
    public void Sequence_mapping_is_explicitly_rejected()
    {
        var error = Assert.Throws<NotSupportedException>(() => CreateTable<SequenceRow>());
        Assert.Contains("sequence", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/232")]
    public void Schema_mapping_is_explicitly_rejected()
    {
        var error = Assert.Throws<NotSupportedException>(() => CreateTable<SchemaRow>());
        Assert.Contains("schema", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/233")]
    public void Update_lock_is_explicitly_rejected()
    {
        SeedRows();
        var error = Assert.Throws<NotSupportedException>(() => Context.Query<Row>(LockType.UpdLock).ToList());
        Assert.Contains("lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/225")]
    public void Serializable_isolation_is_not_silently_downgraded()
    {
        // Chloe opens transactions lazily. Materialize this data source first so
        // BeginTransaction reaches the connection's isolation validation.
        CreateTable<Group>();
        Assert.Throws<NotSupportedException>(() => Context.Session.BeginTransaction(IsolationLevel.Serializable));
        Assert.False(Context.Session.IsInTransaction);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/236")]
    public void Published_database_datetime_codec_loses_submillisecond_ticks()
    {
        // Test the engine directly, outside the ORM translation and parameter conversion.
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "PreciseDates", "Id INT AUTO_INCREMENT, Value DATETIME, PRIMARY KEY(Id)");
        DateTime input = new DateTime(2024, 2, 29, 12, 34, 56, 456).AddTicks(7890);
        command.CommandText = "INSERT INTO PreciseDates (Value) VALUES (@value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.DateTime;
        parameter.Value = input;
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "SELECT Value FROM PreciseDates";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        DateTime actual = reader.GetDateTime(0);
        Assert.NotEqual(input.Ticks, actual.Ticks);
        Assert.Equal(input.Ticks - input.Ticks % TimeSpan.TicksPerMillisecond, actual.Ticks);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/236")]
    public void Orm_rejects_submillisecond_datetime_parameters_instead_of_truncating()
    {
        CreateTable<ScalarRow<DateTime>>();
        DateTime input = new DateTime(2024, 2, 29, 12, 34, 56, 456).AddTicks(7890);
        Assert.Throws<NotSupportedException>(() => Context.Insert(new ScalarRow<DateTime> { Value = input }));
        Assert.Throws<NotSupportedException>(() => Context.Insert(() => new ScalarRow<DateTime> { Value = input }));
        Assert.Throws<NotSupportedException>(() => Context.InsertRange(new List<ScalarRow<DateTime>> { new() { Value = input } }));
        Assert.Throws<NotSupportedException>(() => Context.Query<ScalarRow<DateTime>>().Where(a => a.Value == input).Count());
        Assert.Equal(0, Context.Query<ScalarRow<DateTime>>().Count());
        Assert.False(Context.Session.IsInTransaction);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/235")]
    public void Published_database_decimal_parameter_loses_precision()
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
        Assert.NotEqual(input, reader.GetDecimal(0));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/237")]
    public void Published_database_column_comment_statement_is_rejected()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "CommentedRows", "Value INT");
        command.CommandText = "COMMENT ON COLUMN CommentedRows.Value IS 'column description'";
        Assert.ThrowsAny<Exception>(() => command.ExecuteNonQuery());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/251")]
    public void DateTimeOffset_mapping_is_rejected_and_native_reader_returns_DateTime()
    {
        Assert.Throws<NotSupportedException>(() => CreateTable<ScalarRow<DateTimeOffset>>());
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "OffsetRows", "Id INT AUTO_INCREMENT, Value DATETIME, PRIMARY KEY(Id)");
        command.CommandText = "INSERT INTO OffsetRows (Value) VALUES (@value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.DateTimeOffset;
        parameter.Value = new DateTimeOffset(2024, 2, 29, 12, 34, 56, TimeSpan.FromHours(8));
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "SELECT Value FROM OffsetRows";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        var actual = Assert.IsType<DateTime>(reader.GetValue(0));
        Assert.Throws<InvalidCastException>(() => new DateTimeOffset_ValueConverter().Convert(actual));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/252")]
    public void TimeSpan_mapping_is_rejected_and_native_reader_returns_TimeOnly()
    {
        Assert.Throws<NotSupportedException>(() => CreateTable<ScalarRow<TimeSpan>>());
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "TimeRows", "Id INT AUTO_INCREMENT, Value TIME, PRIMARY KEY(Id)");
        command.CommandText = "INSERT INTO TimeRows (Value) VALUES (@value)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.Time;
        parameter.Value = TimeSpan.FromHours(2);
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "SELECT Value FROM TimeRows";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        var actual = Assert.IsType<TimeOnly>(reader.GetValue(0));
        Assert.Throws<InvalidCastException>(() => new TimeSpan_ValueConverter().Convert(actual));
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/253")]
    public void Keyless_table_mapping_is_explicitly_rejected()
    {
        Assert.Throws<NotSupportedException>(() => CreateTable<KeylessRow>());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/240")]
    public void Explicit_column_length_constraint_is_not_silently_ignored()
    {
        Assert.Throws<NotSupportedException>(() => CreateTable<LengthConstrainedRow>());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/264")]
    public void Explicit_decimal_constraints_are_rejected_and_native_database_does_not_enforce_them()
    {
        Assert.Throws<NotSupportedException>(() => CreateTable<DecimalConstrainedRow>());
        Assert.Throws<NotSupportedException>(() => CreateTable<DecimalPrecisionRow>());
        Assert.Throws<NotSupportedException>(() => CreateTable<DecimalScaleRow>());
        Assert.Throws<NotSupportedException>(() => CreateTable<CustomDecimalConstrainedRow>());
        Assert.Throws<NotSupportedException>(() => CreateTable<CustomNumericConstrainedRow>());

        // Bind strings to avoid the independent native decimal parameter defect (#235).
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "DeclaredDecimals", "Id INT AUTO_INCREMENT, Value DECIMAL(5,2), PRIMARY KEY(Id)");
        foreach (string input in new[] { "1.234", "123456.789" })
        {
            command.CommandText = "INSERT INTO DeclaredDecimals (Value) VALUES (@value)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@value";
            parameter.DbType = DbType.String;
            parameter.Value = input;
            command.Parameters.Add(parameter);
            Assert.Equal(1, command.ExecuteNonQuery());
            command.Parameters.Clear();
        }
        command.CommandText = "SELECT Value FROM DeclaredDecimals ORDER BY Id";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1.234m, reader.GetDecimal(0));
        Assert.True(reader.Read());
        Assert.Equal(123456.789m, reader.GetDecimal(0));
        Assert.False(reader.Read());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/254")]
    public void Explicit_outer_cast_of_aggregate_is_rejected()
    {
        SeedRows();
        Assert.Throws<NotSupportedException>(() => Context.Query<Row>().Select(a => (double)Sql.Sum(a.Value)).ToList());
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SUM(Value) AS FLOAT) FROM Rows";
        var error = Assert.ThrowsAny<Exception>(() => command.ExecuteScalar());
        Assert.Contains("聚合", error.Message);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/258")]
    public void Server_NewGuid_is_rejected_while_client_created_Guid_round_trips()
    {
        CreateTable<ScalarRow<Guid>>();
        Guid clientValue = Guid.NewGuid();
        var entity = Context.Insert(new ScalarRow<Guid> { Value = clientValue });
        Assert.Equal(clientValue, Context.Query<ScalarRow<Guid>>().First().Value);
        Assert.Throws<NotSupportedException>(() => Context.Query<ScalarRow<Guid>>().Select(a => Guid.NewGuid()).ToList());
        Assert.Throws<NotSupportedException>(() => Context.Update<ScalarRow<Guid>>(a => a.Id == entity.Id, a => new ScalarRow<Guid> { Value = Guid.NewGuid() }));
        Assert.Equal(clientValue, Context.Query<ScalarRow<Guid>>().First().Value);
    }

    [Theory]
    [InlineData("and")]
    [InlineData("or")]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/259")]
    public void Full_range_UInt64_bitwise_operations_are_explicitly_rejected(string operation)
    {
        CreateTable<ScalarRow<ulong>>();
        Context.Insert(new ScalarRow<ulong> { Value = 7UL });
        if (operation == "and")
        {
            Assert.Throws<NotSupportedException>(() => Context.Query<ScalarRow<ulong>>().Select(a => a.Value & 3UL).ToList());
            Assert.Throws<NotSupportedException>(() => Context.Update<ScalarRow<ulong>>(a => true, a => new ScalarRow<ulong> { Value = a.Value & 3UL }));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => Context.Query<ScalarRow<ulong>>().Select(a => a.Value | 8UL).ToList());
            Assert.Throws<NotSupportedException>(() => Context.Update<ScalarRow<ulong>>(a => true, a => new ScalarRow<ulong> { Value = a.Value | 8UL }));
        }
        Assert.Equal(7UL, Context.Query<ScalarRow<ulong>>().First().Value);
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/252")]
    public void DateTime_subtraction_returning_TimeSpan_is_explicitly_rejected()
    {
        SeedRows();
        DateTime start = new(2024, 1, 1);
        Assert.Throws<NotSupportedException>(() => Context.Query<Row>().Select(a => a.Created.Subtract(start)).ToList());
        Assert.Throws<NotSupportedException>(() => Context.Query<Row>().Select(a => a.Created - start).ToList());
    }

    [Fact]
    [Trait("Issue", "https://github.com/IoTSharp/SonnetDB/issues/260")]
    public void Server_UInt64_Parse_is_rejected_while_client_parsed_parameter_is_supported()
    {
        CreateTable<ScalarRow<string>>();
        Context.Insert(new ScalarRow<string> { Value = "2.9" });
        Assert.Throws<NotSupportedException>(() => Context.Query<ScalarRow<string>>().Select(a => ulong.Parse(a.Value)).ToList());
        CreateTable<ScalarRow<ulong>>();
        ulong parsed = ulong.Parse("18446744073709551615");
        Context.Insert(new ScalarRow<ulong> { Value = parsed });
        Assert.Equal(parsed, Context.Query<ScalarRow<ulong>>().First().Value);
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
