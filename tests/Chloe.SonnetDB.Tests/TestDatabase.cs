using Chloe.Annotations;
using Chloe.Infrastructure;
using Chloe.SonnetDB;
using Chloe.SonnetDB.DDL;
using SonnetDB.Data;
using System.Data;
using Xunit;

// Mapping configuration and custom handlers are shared by Chloe process-wide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Chloe.SonnetDB.Tests;

public abstract class TestDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Chloe.SonnetDB.Tests", Guid.NewGuid().ToString("N"));

    protected TestDatabase()
    {
        Directory.CreateDirectory(_directory);
        string connectionString = Environment.GetEnvironmentVariable("CHLOE_SONNETDB_CONNECTION_STRING")
            ?? $"Data Source={_directory}";
        ConnectionFactory = new DbConnectionFactory(() => new SndbConnection(connectionString));
        Context = new SonnetDBContext(ConnectionFactory);
    }

    protected IDbConnectionFactory ConnectionFactory { get; }
    protected SonnetDBContext Context { get; }
    protected string DatabaseDirectory => _directory;

    protected void CreateTable<T>(string? table = null)
    {
        new SonnetDBTableGenerator(Context).CreateTable(typeof(T), table, Chloe.RDBMS.DDL.TableCreateMode.CreateNew);
    }

    protected static void CreateNativeTable(IDbCommand command, string name, string columns)
    {
        string quoted = "\"" + name.Replace("\"", "\"\"") + "\"";
        command.CommandText = $"DROP TABLE IF EXISTS {quoted}";
        command.ExecuteNonQuery();
        command.CommandText = $"CREATE TABLE {quoted} ({columns})";
        command.ExecuteNonQuery();
    }

    protected void SeedRows()
    {
        CreateTable<Row>();
        Context.InsertRange(new List<Row>
        {
            new() { Name = "Alpha", GroupId = 1, Value = 10, Enabled = true, Created = new DateTime(2024, 2, 29, 12, 34, 56) },
            new() { Name = "Beta", GroupId = 1, Value = 20, Optional = 4, Enabled = false, Created = new DateTime(2024, 3, 1, 13, 35, 57) },
            new() { Name = "Gamma", GroupId = 2, Value = 30, Optional = 5, Enabled = true, Created = new DateTime(2024, 3, 2, 14, 36, 58) },
            new() { Name = null, GroupId = 3, Value = 40, Enabled = false, Created = new DateTime(2024, 3, 3, 15, 37, 59) }
        });
    }

    protected void SeedGroups()
    {
        CreateTable<Group>();
        Context.InsertRange(new List<Group>
        {
            new() { Id = 1, Name = "First" }, new() { Id = 2, Name = "Second" }, new() { Id = 4, Name = "Unmatched" }
        });
    }

    public void Dispose()
    {
        Context.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

[Table("Rows")]
public class Row
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? AlternateName { get; set; }
    public int GroupId { get; set; }
    public int Value { get; set; }
    public int? Optional { get; set; }
    public bool Enabled { get; set; }
    public DateTime Created { get; set; }
}

[Table("Groups")]
public class Group
{
    [Column(IsPrimaryKey = true), NonAutoIncrement]
    public int Id { get; set; }
    public string? Name { get; set; }
}

[Table("FilteredRows")]
public class FilteredRow : Row { }

[Table("IdentityOnly")]
public class IdentityOnly
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public long Id { get; set; }
}

[Table("ScalarRows")]
public class ScalarRow<T>
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public T Value { get; set; } = default!;
}

[Table("CompositeRows")]
public class CompositeRow
{
    [Column(IsPrimaryKey = true), NonAutoIncrement]
    public int TenantId { get; set; }
    [Column(IsPrimaryKey = true), NonAutoIncrement]
    public int LocalId { get; set; }
    public string? Name { get; set; }
}

[Table("VersionedRows")]
public class VersionedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public string? Name { get; set; }
    [Column(IsRowVersion = true)]
    public int Version { get; set; }
}

[Table("Parents")]
public class Parent
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public string? Name { get; set; }
    [Navigation]
    public List<Child> Children { get; set; } = new();
}

[Table("Children")]
public class Child
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string? Name { get; set; }
    [Navigation(nameof(ParentId))]
    public Parent? Parent { get; set; }
}

public enum State : short { Disabled = -1, Enabled = 2 }
