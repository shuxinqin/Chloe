using Xunit;

namespace Chloe.SonnetDB.Tests;

public class QueryTests : TestDatabase
{
    [Fact]
    public async Task Filtering_sorting_projection_pagination_and_distinct()
    {
        SeedRows();
        var query = Context.Query<Row>();
        Assert.Equal(new[] { 20, 10 }, query.Where(a => a.GroupId == 1).OrderByDesc(a => a.Value).Select(a => a.Value).ToList());
        Assert.Equal(new[] { 20, 30 }, query.OrderBy(a => a.Value).Skip(1).Take(2).Select(a => a.Value).ToList());
        Assert.Equal(new[] { 20, 30, 40 }, query.OrderBy(a => a.Value).Skip(1).Select(a => a.Value).ToList());
        Assert.Equal(new[] { 30, 40 }, query.OrderBy(a => a.Value).TakePage(2, 2).Select(a => a.Value).ToList());
        var page = await query.OrderBy(a => a.Value).PagingAsync(2, 2);
        Assert.Equal(4, page.Totals);
        Assert.Equal(new[] { 30, 40 }, page.DataList.Select(a => a.Value));
        Assert.Equal(new[] { 1, 2, 3 }, query.Select(a => a.GroupId).Distinct().OrderBy(a => a).ToList());
        Assert.Empty(query.Take(0).ToList());
        Assert.Null(query.FirstOrDefault(a => a.Value < 0));
    }

    [Theory]
    [InlineData(JoinType.InnerJoin, 3)]
    [InlineData(JoinType.LeftJoin, 4)]
    [InlineData(JoinType.RightJoin, 4)]
    [InlineData(JoinType.FullJoin, 5)]
    public void All_framework_join_types_keep_unmatched_rows(JoinType join, int expectedRows)
    {
        SeedRows();
        SeedGroups();
        var result = Context.Query<Row>().Join<Group>(join, (row, group) => row.GroupId == group.Id)
            .Select((row, group) => new { RowId = (int?)row.Id, GroupId = (int?)group.Id }).ToList();
        Assert.Equal(expectedRows, result.Count);
        Assert.Equal(3, result.Count(a => a.RowId.HasValue && a.GroupId.HasValue));
        Assert.Equal(join is JoinType.LeftJoin or JoinType.FullJoin ? 1 : 0, result.Count(a => a.RowId.HasValue && !a.GroupId.HasValue));
        Assert.Equal(join is JoinType.RightJoin or JoinType.FullJoin ? 1 : 0, result.Count(a => !a.RowId.HasValue && a.GroupId.HasValue));
    }

    [Fact]
    public void Join_filtered_subquery_keeps_where_and_projection()
    {
        SeedRows();
        SeedGroups();
        var groups = Context.Query<Group>().Where(a => a.Id == 1);
        var result = Context.Query<Row>().InnerJoin(groups, (row, group) => row.GroupId == group.Id)
            .Select((row, group) => new { row.Value, GroupName = group.Name }).ToList();
        Assert.Equal(2, result.Count);
        Assert.All(result, row => Assert.Equal("First", row.GroupName));
    }

    [Fact]
    public void Group_by_having_and_aggregates_preserve_types_and_nulls()
    {
        SeedRows();
        var query = Context.Query<Row>();
        Assert.Equal(4, query.Count());
        Assert.Equal(4L, query.LongCount());
        Assert.Equal(100, query.Sum(a => a.Value));
        Assert.Equal(25d, query.Average(a => a.Value));
        Assert.Equal(40, query.Max(a => a.Value));
        Assert.Equal(10, query.Min(a => a.Value));
        Assert.Null(query.Where(a => a.Value < 0).Max(a => (int?)a.Value));
        var groups = query.GroupBy(a => a.GroupId).Having(a => Sql.Count() > 1)
            .Select(a => new { a.GroupId, Count = Sql.Count(), Sum = Sql.Sum(a.Value), Average = Sql.Average(a.Value), Max = Sql.Max(a.Value), Min = Sql.Min(a.Value) }).ToList();
        var result = Assert.Single(groups);
        Assert.Equal(1, result.GroupId);
        Assert.Equal(2, result.Count);
        Assert.Equal(30, result.Sum);
        Assert.Equal(15d, result.Average);
        Assert.Equal(20, result.Max);
        Assert.Equal(10, result.Min);
    }

    [Fact]
    public void Contains_exists_and_correlated_scalar_subqueries()
    {
        SeedRows();
        SeedGroups();
        var rows = Context.Query<Row>();
        var groups = Context.Query<Group>();
        var values = new List<int> { 10, 30 };
        Assert.Equal(2, rows.Where(a => values.Contains(a.Value)).Count());
        Assert.Equal(3, rows.Where(a => groups.Select(g => g.Id).ToList().Contains(a.GroupId)).Count());
        Assert.Equal(3, rows.Where(a => groups.Where(g => g.Id == a.GroupId).Any()).Count());
        var names = rows.Where(a => a.GroupId < 3).OrderBy(a => a.Value)
            .Select(a => groups.Where(g => g.Id == a.GroupId).First().Name).ToList();
        Assert.Equal(new[] { "First", "First", "Second" }, names);
        var counts = groups.OrderBy(a => a.Id).Select(a => rows.Where(row => row.GroupId == a.Id).Count()).ToList();
        Assert.Equal(new[] { 2, 1, 0 }, counts);
    }

    [Fact]
    public void In_expression_splits_large_collections_and_handles_empty_collection()
    {
        SeedRows();
        Context.Options.MaxInItems = 2;
        var values = new List<int> { 10, 20, 30, 40, 50 };
        Assert.Equal(4, Context.Query<Row>().Where(a => values.Contains(a.Value)).Count());
        var empty = new List<int>();
        Assert.Equal(0, Context.Query<Row>().Where(a => empty.Contains(a.Value)).Count());
    }

    [Fact]
    public void Context_filter_can_be_ignored_and_excluded_column_is_not_materialized()
    {
        CreateTable<FilteredRow>();
        Context.InsertRange(Enumerable.Range(1, 4).Select(a => new FilteredRow { Name = "filtered", Value = a * 10, Created = DateTime.Today }).ToList());
        Context.HasQueryFilter<FilteredRow>(a => a.Value > 10);
        Assert.Equal(3, Context.Query<FilteredRow>().Count());
        Assert.Equal(4, Context.Query<FilteredRow>().IgnoreAllFilters().Count());
        var rows = Context.Query<FilteredRow>().Exclude(a => a.Name).ToList();
        Assert.All(rows, a => Assert.Null(a.Name));
        Assert.Contains(rows, a => a.Value == 20);
    }

    [Fact]
    public async Task Raw_sql_supports_named_parameters_scalar_and_materialization()
    {
        SeedRows();
        var rows = Context.SqlQuery<Row>("SELECT * FROM Rows WHERE Value > @minimum", new { minimum = 20 });
        Assert.Equal(2, rows.Count);
        var names = await Context.SqlQueryAsync<string>("SELECT Name FROM Rows WHERE Value = @value", new { value = 10 });
        Assert.Equal("Alpha", Assert.Single(names));
        Assert.Equal(4, Convert.ToInt32(await Context.Session.ExecuteScalarAsync("SELECT COUNT(*) FROM Rows")));
        Assert.Equal(1, await Context.Session.ExecuteNonQueryAsync("UPDATE Rows SET Value=@value WHERE Value=@old", new { value = 11, old = 10 }));
        Assert.True(Context.Query<Row>().Any(a => a.Value == 11));
    }
}
