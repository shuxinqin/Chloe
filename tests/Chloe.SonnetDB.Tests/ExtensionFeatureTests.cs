using Chloe.Annotations;
using Chloe.DbExpressions;
using Chloe.Entity;
using Chloe.Infrastructure;
using Chloe.Infrastructure.Interception;
using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using Xunit;

namespace Chloe.SonnetDB.Tests;

public class ExtensionFeatureTests : TestDatabase
{
    [Fact]
    public async Task Context_and_command_interceptors_observe_and_change_real_operations()
    {
        CreateTable<InterceptedRow>();
        var recorder = new RecordingInterceptors();
        Context.AddInterceptor(recorder);
        Context.Session.AddInterceptor(recorder);

        var entity = Context.Insert(new InterceptedRow { Name = "entity input", Value = 1 });
        object expressionId = await Context.InsertAsync(() => new InterceptedRow { Name = "expression input", Value = 2 });
        await Context.InsertRangeAsync(new List<InterceptedRow>
        {
            new() { Name = "visible", Value = 3 }, new() { Name = "hidden", Value = 4 }
        });
        int minimum = 1;
        var rows = await Context.Query<InterceptedRow>().Where(a => a.Value >= minimum).OrderBy(a => a.Id).ToListAsync();

        Assert.True(entity.Id > 0);
        Assert.True(Convert.ToInt32(expressionId) > entity.Id);
        Assert.Equal(new[] { "entity intercepted", "expression intercepted", "bulk:visible" }, rows.Select(a => a.Name));
        Assert.Equal(1, recorder.EntityInsertCalls);
        Assert.Equal(1, recorder.ExpressionInsertCalls);
        Assert.Equal(1, recorder.BulkInsertCalls);
        Assert.Equal(1, recorder.QueryCalls);

        entity.Name = "entity update input";
        entity.Value = 10;
        Assert.Equal(1, await Context.UpdateAsync(entity));
        int expressionKey = Convert.ToInt32(expressionId);
        Assert.Equal(1, Context.Update<InterceptedRow>(a => a.Id == expressionKey,
            a => new InterceptedRow { Name = "expression update input", Value = 20 }));
        var updated = await Context.Query<InterceptedRow>().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(new[] { "entity update intercepted", "expression update intercepted", "bulk:visible" }, updated.Select(a => a.Name));
        Assert.Equal(new[] { 10, 20, 3 }, updated.Select(a => a.Value));
        Assert.Equal(1, recorder.EntityUpdateCalls);
        Assert.Equal(1, recorder.ExpressionUpdateCalls);

        Assert.Equal(1, Context.Delete(entity));
        Assert.Equal(1, await Context.DeleteAsync<InterceptedRow>(a => a.Id == expressionKey));
        Assert.Equal(1, recorder.EntityDeleteCalls);
        Assert.Equal(1, recorder.ExpressionDeleteCalls);
        var remaining = Assert.Single(await Context.Query<InterceptedRow>().ToListAsync());
        Assert.Equal("bulk:visible", remaining.Name);
        Assert.Equal(3, remaining.Value);
        using (var plainContext = new SonnetDBContext(ConnectionFactory))
            Assert.Equal(2, await plainContext.Query<InterceptedRow>().CountAsync());

        Assert.All(recorder.Commands, command =>
        {
            Assert.True(command.Completed);
            Assert.Null(command.Exception);
            Assert.True(command.HasResult);
        });
        Assert.Contains(recorder.Commands, a => a.Kind == "scalar" && a.Parameters.Contains("entity intercepted"));
        Assert.Contains(recorder.Commands, a => a.Kind == "scalar" && a.Parameters.Contains("expression intercepted"));
        Assert.Contains(recorder.Commands, a => a.Kind == "nonquery" && a.Parameters.Contains("bulk:visible") && a.Parameters.Contains("bulk:hidden"));
        Assert.Contains(recorder.Commands, a => a.Kind == "reader" && a.Sql.Contains("bulk:hidden", StringComparison.Ordinal) && a.Parameters.Contains(minimum));
        Assert.Contains(recorder.Commands, a => a.Kind == "nonquery" && a.Parameters.Contains("entity update intercepted"));
        Assert.Contains(recorder.Commands, a => a.Kind == "nonquery" && a.Parameters.Contains("expression update intercepted"));
        Assert.Contains(recorder.Commands, a => a.Sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) && a.Parameters.Contains(10));
        Assert.Contains(recorder.Commands, a => a.Sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) && a.Parameters.Contains(20));
        Assert.All(recorder.Commands, command => Assert.False(string.IsNullOrWhiteSpace(command.Sql)));
    }

    [Fact]
    public void Native_json_type_attribute_and_fluent_mapping_create_and_round_trip()
    {
        var mapping = new EntityTypeBuilder<FluentJsonRow>(initializeWithAnnotationMapping: true);
        mapping.Property(a => a.Value).HasSonnetDBDataType("JSON");
        DbConfiguration.UseTypeBuilders(mapping);
        var recorder = new RecordingInterceptors();
        Context.Session.AddInterceptor(recorder);
        CreateTable<AttributedJsonRow>();
        CreateTable<FluentJsonRow>();
        const string json = "{\"name\":\"中文\",\"enabled\":true}";
        Context.Insert(new AttributedJsonRow { Value = json });
        Context.Insert(new FluentJsonRow { Value = json });
        foreach (string actual in new[] { Context.Query<AttributedJsonRow>().First().Value, Context.Query<FluentJsonRow>().First().Value })
        {
            Assert.Equal(json, actual);
            using var document = JsonDocument.Parse(actual);
            Assert.Equal("中文", document.RootElement.GetProperty("name").GetString());
            Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        }
        var createStatements = recorder.Commands.Where(a => a.Sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, createStatements.Count);
        Assert.All(createStatements, a => Assert.Contains("\"Value\" JSON", a.Sql, StringComparison.Ordinal));
    }
}

[Table("InterceptedRows")]
public class InterceptedRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

[Table("AttributedJsonRows")]
public class AttributedJsonRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    [DataType("JSON")]
    public string Value { get; set; } = "";
}

[Table("FluentJsonRows")]
public class FluentJsonRow
{
    [Column(IsPrimaryKey = true), AutoIncrement]
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

public sealed class RecordedCommand
{
    public required string Kind { get; init; }
    public required string Sql { get; init; }
    public required List<object?> Parameters { get; init; }
    public bool Completed { get; set; }
    public bool HasResult { get; set; }
    public Exception? Exception { get; set; }
}

public sealed class RecordingInterceptors : IDbContextInterceptor, IDbCommandInterceptor
{
    public int EntityInsertCalls { get; private set; }
    public int ExpressionInsertCalls { get; private set; }
    public int BulkInsertCalls { get; private set; }
    public int QueryCalls { get; private set; }
    public int EntityUpdateCalls { get; private set; }
    public int ExpressionUpdateCalls { get; private set; }
    public int EntityDeleteCalls { get; private set; }
    public int ExpressionDeleteCalls { get; private set; }
    public List<RecordedCommand> Commands { get; } = new();

    public IQuery<TEntity> QueryCreated<TEntity>(IDbContext dbContext, IQuery<TEntity> query)
    {
        QueryCalls++;
        if (typeof(TEntity) == typeof(InterceptedRow))
            return (IQuery<TEntity>)((IQuery<InterceptedRow>)query).Where(a => a.Name != "bulk:hidden");
        return query;
    }

    public DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbInsertExpression expression)
    {
        EntityInsertCalls++;
        return ReplaceName(expression, "entity intercepted");
    }

    public DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity>> content, DbInsertExpression expression)
    {
        ExpressionInsertCalls++;
        return ReplaceName(expression, "expression intercepted");
    }

    private static DbInsertExpression ReplaceName(DbInsertExpression expression, string name)
    {
        for (int i = 0; i < expression.InsertColumns.Count; i++)
        {
            var pair = expression.InsertColumns[i];
            if (pair.Column.Name == nameof(InterceptedRow.Name))
                expression.InsertColumns[i] = new DbColumnValuePair(pair.Column, new DbParameterExpression(name));
        }
        return expression;
    }

    public void InsertRangeExecuting<TEntity>(IDbContext dbContext, List<TEntity> entities)
    {
        BulkInsertCalls++;
        foreach (var row in entities.OfType<InterceptedRow>())
            row.Name = "bulk:" + row.Name;
    }

    public DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbUpdateExpression expression)
    {
        EntityUpdateCalls++;
        return ReplaceName(expression, "entity update intercepted");
    }

    public DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, DbUpdateExpression expression)
    {
        ExpressionUpdateCalls++;
        return ReplaceName(expression, "expression update intercepted");
    }

    private static DbUpdateExpression ReplaceName(DbUpdateExpression expression, string name)
    {
        for (int i = 0; i < expression.UpdateColumns.Count; i++)
        {
            var pair = expression.UpdateColumns[i];
            if (pair.Column.Name == nameof(InterceptedRow.Name))
                expression.UpdateColumns[i] = new DbColumnValuePair(pair.Column, new DbParameterExpression(name));
        }
        return expression;
    }

    public DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbDeleteExpression expression)
    {
        EntityDeleteCalls++;
        return RequireValue(expression, 10);
    }

    public DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, DbDeleteExpression expression)
    {
        ExpressionDeleteCalls++;
        return RequireValue(expression, 20);
    }

    private static DbDeleteExpression RequireValue(DbDeleteExpression expression, int expectedValue)
    {
        var column = new DbColumnAccessExpression(expression.Table, new DbColumn(nameof(InterceptedRow.Value), typeof(int)));
        var valueCondition = new DbEqualExpression(column, new DbParameterExpression(expectedValue));
        return new DbDeleteExpression(expression.Table, expression.Condition.And(valueCondition));
    }

    private void Before<TResult>(string kind, IDbCommand command, DbCommandInterceptionContext<TResult> context)
    {
        var observation = new RecordedCommand
        {
            Kind = kind,
            Sql = command.CommandText,
            Parameters = command.Parameters.Cast<IDataParameter>().Select(a => a.Value).ToList()
        };
        Commands.Add(observation);
        context.DataBag[nameof(RecordingInterceptors)] = observation;
    }

    private static void After<TResult>(DbCommandInterceptionContext<TResult> context)
    {
        var observation = (RecordedCommand)context.DataBag[nameof(RecordingInterceptors)];
        observation.Completed = true;
        observation.HasResult = context.Result != null;
        observation.Exception = context.Exception;
    }

    public void ReaderExecuting(IDbCommand command, DbCommandInterceptionContext<IDataReader> context) => Before("reader", command, context);
    public void ReaderExecuted(IDbCommand command, DbCommandInterceptionContext<IDataReader> context) => After(context);
    public void NonQueryExecuting(IDbCommand command, DbCommandInterceptionContext<int> context) => Before("nonquery", command, context);
    public void NonQueryExecuted(IDbCommand command, DbCommandInterceptionContext<int> context) => After(context);
    public void ScalarExecuting(IDbCommand command, DbCommandInterceptionContext<object> context) => Before("scalar", command, context);
    public void ScalarExecuted(IDbCommand command, DbCommandInterceptionContext<object> context) => After(context);
}
