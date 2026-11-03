using System.Data;
using Xunit;

namespace Chloe.SonnetDB.Tests;

/// <summary>Direct SQL evidence used to distinguish engine defects from provider translation defects.</summary>
[Trait("Category", "RawSqlProbe")]
public class RawSqlProbes : TestDatabase
{
    [Theory]
    [InlineData("SELECT CAST(7 / 2 AS INT)", 3)]
    [InlineData("SELECT 7 % 2", 1)]
    [InlineData("SELECT LENGTH('中文')", 2)]
    [InlineData("SELECT COALESCE(NULL, 7)", 7)]
    [InlineData("SELECT CASE WHEN 1=1 THEN 7 ELSE 0 END", 7)]
    public void Native_scalar_functions_have_expected_semantics(string sql, int expected)
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(expected, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Native_returning_reports_inserted_identity()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "ReturnedRows", "Id INT AUTO_INCREMENT, Name STRING, PRIMARY KEY(Id)");
        command.CommandText = "INSERT INTO ReturnedRows (Name) VALUES ('probe') RETURNING Id";
        Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 0);
    }

    [Fact]
    public void Native_blob_parameter_is_exact()
    {
        using var connection = ConnectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        CreateNativeTable(command, "BlobRows", "Id INT AUTO_INCREMENT, Value BLOB, PRIMARY KEY(Id)");
        byte[] bytes = { 0, 1, 128, 255 };
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@value";
        parameter.DbType = DbType.Binary;
        parameter.Value = bytes;
        command.Parameters.Add(parameter);
        command.CommandText = "INSERT INTO BlobRows (Value) VALUES (@value)";
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = "SELECT Value FROM BlobRows";
        Assert.Equal(bytes, Assert.IsType<byte[]>(command.ExecuteScalar()));
    }
}
