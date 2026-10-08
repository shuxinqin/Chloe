using Chloe.Annotations;
using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.Sharding;
using Chloe.Sharding.Routing;
using System.Reflection;
using Xunit;

namespace Chloe.SonnetDB.Tests;

public class CoreFeatureTests : TestDatabase
{
    [Fact]
    public async Task Navigation_include_split_query_and_cascade_save()
    {
        CreateTable<Parent>();
        CreateTable<Child>();
        var parent = new Parent { Name = "owner", Children = new List<Child> { new() { Name = "first" }, new() { Name = "second" } } };
        await Context.SaveAsync(parent);
        Assert.True(parent.Id > 0);
        Assert.All(parent.Children, child => Assert.Equal(parent.Id, child.ParentId));
        var joined = Context.Query<Parent>().IncludeMany(a => a.Children).First(a => a.Id == parent.Id);
        Assert.Equal(new[] { "first", "second" }, joined.Children.Select(a => a.Name).OrderBy(a => a));
        var split = await Context.Query<Parent>().IncludeMany(a => a.Children).SplitQuery().ToListAsync();
        Assert.Equal(2, Assert.Single(split).Children.Count);
        var childWithParent = Context.Query<Child>().Include(a => a.Parent).First();
        Assert.Equal("owner", childWithParent.Parent!.Name);
    }

    [Fact]
    public void Options_and_context_clone_preserve_configuration()
    {
        Context.Options.MaxInItems = 2;
        Context.Options.MaxNumberOfParameters = 50;
        Context.Options.DefaultBatchSizeForInsertRange = 3;
        Context.Options.InsertStrategy = InsertStrategy.IgnoreNull | InsertStrategy.IgnoreEmptyString;
        Context.Options.TreatEmptyStringAsNull = true;
        var options = Context.Options.Clone();
        Assert.NotSame(Context.Options, options);
        Assert.Same(Context.Options.DbConnectionFactory, options.DbConnectionFactory);
        Assert.Equal(Context.Options.MaxInItems, options.MaxInItems);
        Assert.Equal(Context.Options.MaxNumberOfParameters, options.MaxNumberOfParameters);
        Assert.Equal(Context.Options.DefaultBatchSizeForInsertRange, options.DefaultBatchSizeForInsertRange);
        Assert.Equal(Context.Options.InsertStrategy, options.InsertStrategy);
        Assert.Equal(Context.Options.TreatEmptyStringAsNull, options.TreatEmptyStringAsNull);
        using var clone = (SonnetDBContext)Context.Clone();
        Assert.NotSame(Context, clone);
        Assert.NotSame(Context.Options, clone.Options);
        Assert.Equal("SonnetDB", clone.DatabaseType);
        CreateTable<Group>();
        Context.Insert(new Group { Id = 1, Name = "shared database" });
        Assert.Equal("shared database", clone.Query<Group>().First().Name);
    }

    [Fact]
    public void Custom_method_and_property_handlers_execute_sql()
    {
        var methodHandler = new DoubleValueHandler();
        var propertyHandler = new LengthHandler();
        SonnetDBContext.SetMethodHandler(nameof(CustomSql.DoubleValue), methodHandler);
        SonnetDBContext.SetPropertyHandler(nameof(string.Length), propertyHandler);
        SeedRows();
        var result = Context.Query<Row>().Where(a => a.Name != null)
            .OrderBy(a => a.Value).Select(a => new { Doubled = CustomSql.DoubleValue(a.Value), Length = a.Name!.Length }).ToList();
        Assert.Equal(new[] { 20, 40, 60 }, result.Select(a => a.Doubled));
        Assert.Equal(new[] { 5, 4, 5 }, result.Select(a => a.Length));
        Assert.True(methodHandler.Processed);
        Assert.True(propertyHandler.Processed);
    }

    [Fact]
    public void Db_function_attribute_invokes_database_function()
    {
        SeedRows();
        Assert.Equal(5, Context.Query<Row>().Where(a => a.Value == 10).Select(a => CustomSql.NativeLength(a.Name!)).First());
    }

    [Fact]
    public void Ddl_create_if_not_exists_keeps_existing_data()
    {
        CreateTable<Group>();
        Context.Insert(new Group { Id = 1, Name = "persisted" });
        new DDL.SonnetDBTableGenerator(Context).CreateTable(typeof(Group));
        Assert.Equal("persisted", Context.Query<Group>().First().Name);
    }

    [Fact]
    public void Quoted_identifiers_with_spaces_and_quotes_round_trip()
    {
        CreateTable<QuotedRow>();
        var row = Context.Insert(new QuotedRow { Value = "O'Brien 中文" });
        Assert.Equal(row.Value, Context.Query<QuotedRow>().First(a => a.Id == row.Id).Value);
        Assert.Equal(1, Context.Update<QuotedRow>(a => a.Id == row.Id, a => new QuotedRow { Value = "changed" }));
        Assert.Equal("changed", Context.Query<QuotedRow>().First().Value);
        Assert.Equal(1, Context.Delete(row));
    }

    [Fact]
    public async Task Sharding_routes_writes_and_merges_reads_without_provider_special_case()
    {
        CreateTable<ShardRow>("ShardRows0");
        CreateTable<ShardRow>("ShardRows1");
        var route = new TwoTableRoute(Context.DbContextProviderFactory);
        var config = new ShardingConfigBuilder<ShardRow>().HasShardingKey(a => a.Partition).HasRoute(route).Build();
        Context.HasShardingConfig(typeof(ShardRow), config);
        Context.ShardingOptions.MaxConnectionsPerDataSource = 1;
        await Context.InsertRangeAsync(new List<ShardRow>
        {
            new() { Id = 1, Partition = 0, Value = 10 }, new() { Id = 2, Partition = 1, Value = 20 },
            new() { Id = 3, Partition = 0, Value = 30 }, new() { Id = 4, Partition = 1, Value = 40 }
        });
        Assert.Equal(2, Context.Query<ShardRow>().Where(a => a.Partition == 0).Count());
        Assert.Equal(4, await Context.Query<ShardRow>().CountAsync());
        Assert.Equal(new[] { 20, 30 }, (await Context.Query<ShardRow>().OrderBy(a => a.Value).Skip(1).Take(2).ToListAsync()).Select(a => a.Value));
        Assert.Equal(100, Context.Query<ShardRow>().Sum(a => a.Value));
        Assert.Equal(2, Context.Update<ShardRow>(a => a.Partition == 1, a => new ShardRow { Value = a.Value + 1 }));
        Assert.Equal(2, Context.Delete<ShardRow>(a => a.Partition == 0));
        Assert.Equal(new[] { 21, 41 }, Context.Query<ShardRow>().OrderBy(a => a.Value).Select(a => a.Value).ToList());
    }
}

public static class CustomSql
{
    public static int DoubleValue(int value) => throw new NotSupportedException();
    [DbFunction("length")]
    public static int NativeLength(string value) => throw new NotSupportedException();
}

public sealed class DoubleValueHandler : IMethodHandler
{
    public bool Processed { get; private set; }
    public bool CanProcess(DbMethodCallExpression expression) => expression.Method.DeclaringType == typeof(CustomSql);
    public void Process(DbMethodCallExpression expression, SqlGeneratorBase generator)
    {
        Processed = true;
        generator.SqlBuilder.Append("(");
        expression.Arguments[0].Accept(generator);
        generator.SqlBuilder.Append(" * 2)");
    }
}

public sealed class LengthHandler : IPropertyHandler
{
    public bool Processed { get; private set; }
    public bool CanProcess(DbMemberAccessExpression expression) => expression.Member.DeclaringType == typeof(string);
    public void Process(DbMemberAccessExpression expression, SqlGeneratorBase generator)
    {
        Processed = true;
        generator.SqlBuilder.Append("LENGTH(");
        expression.Expression.Accept(generator);
        generator.SqlBuilder.Append(")");
    }
}

[Table("ShardRows")]
public class ShardRow
{
    [Column(IsPrimaryKey = true), NonAutoIncrement]
    public int Id { get; set; }
    public int Partition { get; set; }
    public int Value { get; set; }
}

[Table("Quoted \" Rows")]
public class QuotedRow
{
    [Column("Row \" Id", IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [Column("Value \" Label")]
    public string? Value { get; set; }
}

public sealed class TwoTableRoute : IShardingRoute
{
    private readonly List<RouteTable> _tables;
    public TwoTableRoute(IDbContextProviderFactory factory)
    {
        var source = new RouteDataSource { Name = "SonnetDBTest", DbContextProviderFactory = factory };
        _tables = new List<RouteTable>
        {
            new() { Name = "ShardRows0", DataSource = source }, new() { Name = "ShardRows1", DataSource = source }
        };
    }

    public IEnumerable<RouteTable> GetTables() => _tables;
    public IRoutingStrategy? GetStrategy(MemberInfo member) => member.Name == nameof(ShardRow.Partition) ? new PartitionStrategy(this) : null;
    public SortResult SortTables(List<RouteTable> tables, List<Ordering> orderings) => new() { Tables = tables, IsOrdered = false };

    private sealed class PartitionStrategy : RoutingStrategy<int>
    {
        public PartitionStrategy(IShardingRoute route) : base(route) { }
        public override IEnumerable<RouteTable> ForEqual(int value) => Route.GetTables().Where(table => table.Name == "ShardRows" + value);
    }
}
