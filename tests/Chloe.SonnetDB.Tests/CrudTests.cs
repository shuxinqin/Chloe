using Chloe.Exceptions;
using Xunit;

namespace Chloe.SonnetDB.Tests;

public class CrudTests : TestDatabase
{
    [Fact]
    public void Entity_crud_returns_identity_and_affected_rows()
    {
        CreateTable<Row>();
        var entity = new Row { Name = "O'Brien 中文", Value = 7, Enabled = true, Created = new DateTime(2024, 6, 1) };
        Assert.Same(entity, Context.Insert(entity));
        Assert.True(entity.Id > 0);
        var loaded = Context.Query<Row>().First(a => a.Id == entity.Id);
        Assert.Equal(entity.Name, loaded.Name);
        Assert.True(loaded.Enabled);
        loaded.Name = "changed";
        Assert.Equal(1, Context.Update(loaded));
        Assert.Equal("changed", Context.Query<Row>().First(a => a.Id == entity.Id).Name);
        Assert.Equal(1, Context.Delete(loaded));
        Assert.False(Context.Query<Row>().Any());
    }

    [Fact]
    public void Expression_crud_returns_key_and_computes_update_on_server()
    {
        CreateTable<Row>();
        string name = "expression";
        DateTime created = new(2024, 6, 1);
        int id = Convert.ToInt32(Context.Insert(() => new Row { Name = name, Value = 7, GroupId = 0, Enabled = false, Created = created }));
        Assert.True(id > 0);
        Assert.Equal(1, Context.Update<Row>(a => a.Id == id, a => new Row { Value = a.Value + 3, Name = a.Name + "!" }));
        var loaded = Context.Query<Row>().First(a => a.Id == id);
        Assert.Equal(10, loaded.Value);
        Assert.Equal("expression!", loaded.Name);
        Assert.Equal(1, Context.Delete<Row>(a => a.Id == id));
    }

    [Fact]
    public async Task Async_entity_and_expression_crud_preserve_results()
    {
        CreateTable<Row>();
        var entity = await Context.InsertAsync(new Row { Name = "async", Value = 3, Created = DateTime.Today });
        Assert.True(entity.Id > 0);
        entity.Value = 5;
        Assert.Equal(1, await Context.UpdateAsync(entity));
        Assert.Equal(5, (await Context.Query<Row>().FirstAsync(a => a.Id == entity.Id)).Value);
        Assert.Equal(1, await Context.DeleteAsync(entity));
        var key = Convert.ToInt32(await Context.InsertAsync(() => new Row { Name = "async expression", Value = 2, GroupId = 0, Enabled = false, Created = DateTime.Today }));
        Assert.True(key > 0);
        Assert.Equal(1, await Context.UpdateAsync<Row>(a => a.Id == key, a => new Row { Value = a.Value * 4 }));
        Assert.Equal(8, (await Context.Query<Row>().ToListAsync()).Single().Value);
        Assert.Equal(1, await Context.DeleteAsync<Row>(a => a.Id == key));
        Assert.Equal(0, await Context.Query<Row>().CountAsync());
    }

    [Fact]
    public async Task Identity_only_entities_use_default_values()
    {
        CreateTable<IdentityOnly>();
        var first = Context.Insert(new IdentityOnly());
        var second = await Context.InsertAsync(new IdentityOnly());
        long third = Convert.ToInt64(Context.Insert(() => new IdentityOnly { }));
        long fourth = Convert.ToInt64(await Context.InsertAsync(() => new IdentityOnly { }));
        Assert.Equal(4, new[] { first.Id, second.Id, third, fourth }.Distinct().Count());
        Assert.All(new[] { first.Id, second.Id, third, fourth }, id => Assert.True(id > 0));
        Assert.Equal(4, Context.Query<IdentityOnly>().Count());
    }

    [Fact]
    public async Task Insert_range_batches_respect_parameter_limit_and_outer_transaction()
    {
        CreateTable<Row>();
        Context.Options.MaxNumberOfParameters = 20;
        Context.Options.DefaultBatchSizeForInsertRange = 2;
        var rows = Enumerable.Range(1, 7).Select(i => new Row { Name = "batch-" + i, Value = i, Created = DateTime.Today }).ToList();
        Context.InsertRange(rows);
        Assert.Equal(7, Context.Query<Row>().Count());
        Context.Session.BeginTransaction();
        await Context.InsertRangeAsync(rows, batchSize: 2, table: null);
        Assert.True(Context.Session.IsInTransaction);
        Assert.Equal(14, Context.Query<Row>().Count());
        Context.Session.RollbackTransaction();
        Assert.Equal(7, Context.Query<Row>().Count());
    }

    [Fact]
    public void Failed_later_batch_rolls_back_entire_insert_range()
    {
        CreateTable<Group>();
        var entities = new List<Group> { new() { Id = 1, Name = "one" }, new() { Id = 2 }, new() { Id = 1, Name = "duplicate" } };
        Assert.ThrowsAny<Exception>(() => Context.InsertRange(entities, batchSize: 1, table: null));
        Assert.False(Context.Session.IsInTransaction);
        Assert.Equal(0, Context.Query<Group>().Count());
    }

    [Fact]
    public void Transaction_commit_and_rollback_are_observable()
    {
        CreateTable<Group>();
        Context.Session.BeginTransaction();
        Context.Insert(new Group { Id = 1, Name = "committed" });
        Context.Session.CommitTransaction();
        Context.Session.BeginTransaction();
        Context.Update<Group>(a => a.Id == 1, a => new Group { Name = "rolled back" });
        Context.Insert(new Group { Id = 2, Name = "rolled back" });
        Context.Session.RollbackTransaction();
        Assert.Equal("committed", Context.Query<Group>().First().Name);
        Assert.Equal(1, Context.Query<Group>().Count());
    }

    [Fact]
    public void Commit_and_rollback_failure_release_session_and_allow_reuse()
    {
        // SonnetDB 4.0.0 terminates a lightweight transaction when its deferred
        // batch commit fails. Its following Rollback throws, so Chloe must still
        // release both the ADO transaction and the context transaction state.
        CreateTable<Group>();
        Context.Session.BeginTransaction();
        Context.Insert(new Group { Id = 1, Name = "first" });
        Context.Insert(new Group { Id = 2, Name = "second" });
        Context.Insert(new Group { Id = 1, Name = "duplicate" });
        Assert.ThrowsAny<Exception>(() => Context.Session.CommitTransaction());
        Assert.ThrowsAny<Exception>(() => Context.Session.RollbackTransaction());
        Assert.False(Context.Session.IsInTransaction);
        Assert.Null(Context.Session.CurrentTransaction);
        Assert.Equal(0, Context.Query<Group>().Count());
        Context.Session.BeginTransaction();
        Context.Insert(new Group { Id = 1, Name = "reused" });
        Context.Session.CommitTransaction();
        Assert.False(Context.Session.IsInTransaction);
        Assert.Null(Context.Session.CurrentTransaction);
        Assert.Equal("reused", Context.Query<Group>().First().Name);
    }

    [Fact]
    public void Composite_primary_key_crud_targets_both_columns()
    {
        CreateTable<CompositeRow>();
        Context.Insert(new CompositeRow { TenantId = 1, LocalId = 1, Name = "one" });
        Context.Insert(new CompositeRow { TenantId = 1, LocalId = 2, Name = "two" });
        Context.Insert(new CompositeRow { TenantId = 2, LocalId = 1, Name = "other tenant" });
        var target = new CompositeRow { TenantId = 1, LocalId = 1, Name = "changed" };
        Assert.Equal(1, Context.Update(target));
        Assert.Equal("two", Context.Query<CompositeRow>().First(a => a.TenantId == 1 && a.LocalId == 2).Name);
        Assert.Equal("other tenant", Context.Query<CompositeRow>().First(a => a.TenantId == 2 && a.LocalId == 1).Name);
        Assert.Equal(1, Context.Delete(target));
        Assert.Equal(2, Context.Query<CompositeRow>().Count());
    }

    [Fact]
    public void Row_version_rejects_stale_update_and_delete()
    {
        CreateTable<VersionedRow>();
        var original = Context.Insert(new VersionedRow { Name = "one", Version = 1 });
        var stale = Context.Query<VersionedRow>().First(a => a.Id == original.Id);
        original.Name = "updated";
        Assert.Equal(1, Context.Update(original));
        Assert.Equal(2, original.Version);
        Assert.Throws<OptimisticConcurrencyException>(() => Context.Update(stale));
        Assert.Throws<OptimisticConcurrencyException>(() => Context.Delete(stale));
        Assert.Equal("updated", Context.Query<VersionedRow>().First().Name);
    }

    [Fact]
    public void Tracking_updates_only_changed_fields()
    {
        SeedRows();
        var tracked = Context.Query<Row>().AsTracking().First(a => a.Value == 10);
        Context.Update<Row>(a => a.Id == tracked.Id, a => new Row { Value = 99 });
        tracked.Name = "tracked change";
        Assert.Equal(1, Context.Update(tracked));
        var result = Context.Query<Row>().First(a => a.Id == tracked.Id);
        Assert.Equal(99, result.Value);
        Assert.Equal("tracked change", result.Name);
    }
}
