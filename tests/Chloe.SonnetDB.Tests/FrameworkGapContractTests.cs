using Chloe.Annotations;
using Chloe.SQLite;
using Chloe.SQLite.DDL;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Chloe.SonnetDB.Tests;

/// <summary>
/// An inherited Chloe cache defect: RootQueryExpression hash/equality omit context
/// filters in src/Chloe/Query/QueryExpressionEqualityComparer.cs (lines 32 and 225).
/// SQLite provides an independent database baseline for the same behavior.
/// Passing these defect reproduction tests is not support for changing cached filters.
/// </summary>
[Trait("Category", "FrameworkGapContract")]
[Trait("Issue", "https://github.com/shuxinqin/Chloe/issues/434")]
public class FrameworkGapContractTests : TestDatabase
{
    [Fact]
    public void Cached_unfiltered_query_ignores_new_filter_on_sonnetdb()
    {
        CreateTable<CachedFilterRow>();
        Context.Insert(new CachedFilterRow { Value = 10 });
        Context.Insert(new CachedFilterRow { Value = 20 });
        Assert.Equal(2, Context.Query<CachedFilterRow>().Count());
        Context.HasQueryFilter<CachedFilterRow>(a => a.Value > 10);
        // The intended result is 1, but Chloe reuses the earlier unfiltered query plan.
        Assert.Equal(2, Context.Query<CachedFilterRow>().Count());
        // A new expression shape verifies that the filter was actually registered.
        Assert.Single(Context.Query<CachedFilterRow>().Where(a => a.Id > 0).ToList());
    }

    [Fact]
    public void Cached_unfiltered_query_ignores_new_filter_on_sqlite_baseline()
    {
        using var sqlite = new SQLiteContext(() => new SqliteConnection($"Data Source={Path.Combine(DatabaseDirectory, "baseline.sqlite")};Pooling=False"));
        new SQLiteTableGenerator(sqlite).CreateTable(typeof(SqliteCachedFilterRow));
        sqlite.Insert(new SqliteCachedFilterRow { Value = 10 });
        sqlite.Insert(new SqliteCachedFilterRow { Value = 20 });
        Assert.Equal(2, sqlite.Query<SqliteCachedFilterRow>().Count());
        sqlite.HasQueryFilter<SqliteCachedFilterRow>(a => a.Value > 10);
        Assert.Equal(2, sqlite.Query<SqliteCachedFilterRow>().Count());
        Assert.Single(sqlite.Query<SqliteCachedFilterRow>().Where(a => a.Id > 0).ToList());
    }
}

[Table("CachedFilterRows")]
public class CachedFilterRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public int Value { get; set; }
}

[Table("SqliteCachedFilterRows")]
public class SqliteCachedFilterRow : CachedFilterRow { }
