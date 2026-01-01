using Chloe.Infrastructure.Interception;
using Chloe.Sharding;
using System.Data;
using System.Linq.Expressions;

namespace Chloe
{
    public class DbContext : DbContextBase, IDbContext
    {
        public const string DefaultProviderDataSourceName = "_default_";

        IDbSession _session;

        public DbContext() : this(new DbOptions())
        {

        }

        public DbContext(DbOptions options) : this(options, null)
        {

        }

        public DbContext(DbOptions options, IDbContextProviderFactory dbContextProviderFactory)
        {
            PublicHelper.CheckNull(options, nameof(options));

            this.Options = options;
            this.DbContextProviderFactory = dbContextProviderFactory;
            this.Butler = new DbContextButler(this);
            this._session = new DbSession(this);
        }

        public DbOptions Options { get; private set; }

        /// <summary>
        /// 是否启用分片功能。
        /// </summary>
        public bool ShardingEnabled { get; set; } = true;
        public ShardingOptions ShardingOptions { get; private set; } = new ShardingOptions();

        public IDbContextProviderFactory DbContextProviderFactory { get; private set; }
        public DbContextButler Butler { get; private set; }
        public override IDbSession Session { get { return this._session; } }
        public IDbContextProvider DefaultDbContextProvider { get { return this.Butler.GetDefaultDbContextProvider(); } }
        internal IDbContextProvider ShardingDbContextProvider { get { return this.Butler.GetShardingDbContextProvider(); } }

        public virtual string DatabaseType
        {
            get
            {
                return (this.DefaultDbContextProvider as DbContextProvider).DatabaseProvider.DatabaseType;
            }
        }

        protected bool IsShardingType(Type entityType)
        {
            IShardingConfig shardingConfig = this.GetShardingConfig(entityType);
            return shardingConfig != null;
        }
        protected internal IShardingConfig GetShardingConfig(Type entityType)
        {
            IShardingConfig shardingConfig = this.Butler.FindShardingConfig(entityType);
            if (shardingConfig == null)
                shardingConfig = ShardingConfigContainer.Find(entityType);

            return shardingConfig;
        }

        protected IDbContextProvider GetDbContextProvider(Type entityType)
        {
            if (!this.ShardingEnabled)
            {
                return this.DefaultDbContextProvider;
            }

            bool isShardingType = this.IsShardingType(entityType);
            if (isShardingType)
            {
                return this.ShardingDbContextProvider;
            }

            return this.DefaultDbContextProvider;
        }
        protected IDbContextProvider GetDbContextProvider<TEntity>()
        {
            return this.GetDbContextProvider(typeof(TEntity));
        }


        protected override void Dispose(bool disposing)
        {
            this.Butler.Dispose();
        }

        public override object Clone()
        {
            DbContext dbContext = this.CloneImpl();

            dbContext.ShardingEnabled = this.ShardingEnabled;
            dbContext.ShardingOptions.MaxConnectionsPerDataSource = this.ShardingOptions.MaxConnectionsPerDataSource;

            foreach (var entityQueryFilterKV in this.Butler.QueryFilters)
            {
                foreach (LambdaExpression queryFilter in entityQueryFilterKV.Value)
                {
                    dbContext.HasQueryFilter(entityQueryFilterKV.Key, queryFilter);
                }
            }

            foreach (var entityShardingConfigKV in this.Butler.ContextShardingConfigs)
            {
                dbContext.HasShardingConfig(entityShardingConfigKV.Key, entityShardingConfigKV.Value);
            }

            dbContext.Session.CommandTimeout = this.Session.CommandTimeout;
            foreach (IDbCommandInterceptor interceptor in this.Butler.DbCommandInterceptors)
            {
                dbContext.Session.AddInterceptor(interceptor);
            }

            foreach (IDbContextInterceptor interceptor in this.Butler.DbContextInterceptors)
            {
                dbContext.AddInterceptor(interceptor);
            }

            return dbContext;
        }

        protected virtual DbContext CloneImpl()
        {
            DbContext dbContext = new DbContext(this.Options, this.DbContextProviderFactory);
            return dbContext;
        }

        public override void TrackEntity(object entity)
        {
            PublicHelper.CheckNull(entity);
            var entityType = entity.GetType();
            this.GetDbContextProvider(entityType).TrackEntity(entity);
        }

        public override void AddInterceptor(IDbContextInterceptor interceptor)
        {
            this.Butler.AddDbContextInterceptor(interceptor);
        }

        public override void HasQueryFilter(Type entityType, LambdaExpression filter)
        {
            this.Butler.HasQueryFilter(entityType, filter);
        }

        public override void HasShardingConfig(Type entityType, IShardingConfig shardingConfig)
        {
            this.Butler.HasShardingConfig(entityType, shardingConfig);
        }

        public override IQuery<TEntity> Query<TEntity>(string table, LockType @lock)
        {
            return this.GetDbContextProvider<TEntity>().Query<TEntity>(table, @lock);
        }

        public override List<T> SqlQuery<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            return this.DefaultDbContextProvider.SqlQuery<T>(sql, cmdType, parameters);
        }
        public override List<T> SqlQuery<T>(string sql, CommandType cmdType, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", CommandType.Text, new { Id = 1 });
             */

            return this.DefaultDbContextProvider.SqlQuery<T>(sql, cmdType, parameter);
        }
        public override Task<List<T>> SqlQueryAsync<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            return this.DefaultDbContextProvider.SqlQueryAsync<T>(sql, cmdType, parameters);
        }
        public override Task<List<T>> SqlQueryAsync<T>(string sql, CommandType cmdType, object parameter)
        {
            return this.DefaultDbContextProvider.SqlQueryAsync<T>(sql, cmdType, parameter);
        }

        public override TEntity Save<TEntity>(TEntity entity)
        {
            return this.GetDbContextProvider<TEntity>().Save(entity);
        }
        public override Task<TEntity> SaveAsync<TEntity>(TEntity entity)
        {
            return this.GetDbContextProvider<TEntity>().SaveAsync(entity);
        }

        protected override Task<TEntity> Insert<TEntity>(TEntity entity, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.InsertAsync(entity, table);
            }

            return Task.FromResult(dbContextProvider.Insert(entity, table));
        }
        protected override Task<object> Insert<TEntity>(Expression<Func<TEntity>> content, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.InsertAsync(content, table);
            }

            return Task.FromResult(dbContextProvider.Insert(content, table));
        }
        protected override Task InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.InsertRangeAsync(entities, batchSize, table);
            }

            dbContextProvider.InsertRange(entities, batchSize, table);
            return Task.CompletedTask;
        }

        protected override Task<int> Update<TEntity>(TEntity entity, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.UpdateAsync(entity, table);
            }

            return Task.FromResult(dbContextProvider.Update(entity, table));
        }
        protected override Task<int> Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.UpdateAsync(condition, content, table);
            }

            return Task.FromResult(dbContextProvider.Update(condition, content, table));
        }

        protected override Task<int> Delete<TEntity>(TEntity entity, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.DeleteAsync(entity, table);
            }

            return Task.FromResult(dbContextProvider.Delete(entity, table));
        }
        protected override Task<int> Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table, bool @async)
        {
            var dbContextProvider = this.GetDbContextProvider<TEntity>();
            if (@async)
            {
                return dbContextProvider.DeleteAsync(condition, table);
            }

            return Task.FromResult(dbContextProvider.Delete(condition, table));
        }
    }
}
