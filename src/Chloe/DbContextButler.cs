using Chloe.Exceptions;
using Chloe.Infrastructure.Interception;
using Chloe.Sharding;
using System.Data;
using System.Linq.Expressions;

namespace Chloe
{
    public class DbContextButler : IDisposable
    {
        bool _disposed = false;

        int _commandTimeout = 30;
        IDbContextProvider _defaultDbContextProvider;
        IDbContextProvider _shardingDbContextProvider;
        Dictionary<Type, IShardingConfig> _contextShardingConfigs;

        public DbContextButler(DbContext dbContext)
        {
            this.DbContext = dbContext;
        }

        public DbContext DbContext { get; private set; }

        public List<DataSourceDbContextProviderPair> PersistedDbContextProviders { get; set; } = new List<DataSourceDbContextProviderPair>();
        public List<IDbCommandInterceptor> DbCommandInterceptors { get; } = new List<IDbCommandInterceptor>();
        public List<IDbContextInterceptor> DbContextInterceptors { get; } = new List<IDbContextInterceptor>();
        public Dictionary<Type, List<LambdaExpression>> QueryFilters { get; } = new Dictionary<Type, List<LambdaExpression>>();

        public Dictionary<Type, IShardingConfig> ContextShardingConfigs
        {
            get
            {
                if (this._contextShardingConfigs == null)
                {
                    this._contextShardingConfigs = new Dictionary<Type, IShardingConfig>();
                }

                return this._contextShardingConfigs;
            }
        }

        public IsolationLevel? IL { get; private set; }
        public bool IsInTransaction { get; private set; }
        public int CommandTimeout
        {
            get
            {
                return this._commandTimeout;
            }
            set
            {
                this.SetCommandTimeout(value);
            }
        }

        public void Dispose()
        {
            if (this._disposed)
                return;

            if (this.IsInTransaction)
            {
                try
                {
                    this.RollbackTransactionImpl();
                }
                catch
                {
                }
            }

            for (int i = 0; i < this.PersistedDbContextProviders.Count; i++)
            {
                DataSourceDbContextProviderPair pair = this.PersistedDbContextProviders[i];
                pair.DbContextProvider.Dispose();
            }

            this.PersistedDbContextProviders.Clear();

            this._disposed = true;
        }

        void SetCommandTimeout(int commandTimeout)
        {
            this._commandTimeout = commandTimeout;

            foreach (var pair in this.PersistedDbContextProviders)
            {
                pair.DbContextProvider.Session.CommandTimeout = commandTimeout;
            }
        }

        public void AddDbCommandInterceptor(IDbCommandInterceptor interceptor)
        {
            PublicHelper.CheckNull(interceptor, nameof(interceptor));
            this.DbCommandInterceptors.Add(interceptor);
            foreach (var pair in this.PersistedDbContextProviders)
            {
                pair.DbContextProvider.Session.AddInterceptor(interceptor);
            }
        }
        public void RemoveDbCommandInterceptor(IDbCommandInterceptor interceptor)
        {
            PublicHelper.CheckNull(interceptor, nameof(interceptor));
            this.DbCommandInterceptors.Remove(interceptor);
            foreach (var pair in this.PersistedDbContextProviders)
            {
                pair.DbContextProvider.Session.RemoveInterceptor(interceptor);
            }
        }

        public void AddDbContextInterceptor(IDbContextInterceptor dbContextInterceptor)
        {
            PublicHelper.CheckNull(dbContextInterceptor, nameof(dbContextInterceptor));
            this.DbContextInterceptors.Add(dbContextInterceptor);
        }

        public void BeginTransaction(IsolationLevel? il)
        {
            if (this.IsInTransaction)
            {
                throw new ChloeException("The current session has opened a transaction.");
            }

            try
            {
                for (int i = 0; i < this.PersistedDbContextProviders.Count; i++)
                {
                    DataSourceDbContextProviderPair pair = this.PersistedDbContextProviders[i];
                    pair.DbContextProvider.Session.BeginTransaction(il);
                }
            }
            catch
            {
                try
                {
                    this.RollbackTransactionImpl();
                }
                catch
                {
                }

                throw;
            }

            this.IL = il;
            this.IsInTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!this.IsInTransaction)
            {
                throw new ChloeException("Current session does not open a transaction.");
            }

            for (int i = 0; i < this.PersistedDbContextProviders.Count; i++)
            {
                DataSourceDbContextProviderPair pair = this.PersistedDbContextProviders[i];
                var dbContextProvider = pair.DbContextProvider;

                if (!dbContextProvider.Session.IsInTransaction)
                {
                    continue;
                }

                dbContextProvider.Session.CommitTransaction();
            }

            this.IsInTransaction = false;
        }
        public void RollbackTransaction()
        {
            if (!this.IsInTransaction)
            {
                throw new ChloeException("Current session does not open a transaction.");
            }

            this.RollbackTransactionImpl();
        }
        void RollbackTransactionImpl()
        {
            List<Exception> exceptions = null;

            for (int i = 0; i < this.PersistedDbContextProviders.Count; i++)
            {
                DataSourceDbContextProviderPair pair = this.PersistedDbContextProviders[i];
                var dbContextProvider = pair.DbContextProvider;
                if (!dbContextProvider.Session.IsInTransaction)
                {
                    continue;
                }

                try
                {
                    dbContextProvider.Session.RollbackTransaction();
                }
                catch (Exception ex)
                {
                    if (this.PersistedDbContextProviders.Count == 1 && pair.DbContextProvider == this._defaultDbContextProvider)
                    {
                        throw;
                    }

                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            this.IsInTransaction = false;

            if (exceptions != null && exceptions.Count > 0)
            {
                AggregateException aggregateException = new AggregateException("One or more exceptions occurred when rolling back the transaction.", exceptions);
                throw aggregateException;
            }
        }

        public void UseTransaction(IDbTransaction dbTransaction)
        {
            IDbContextProvider defaultDbContextProvider = this.GetDefaultDbContextProvider();
            defaultDbContextProvider.Session.UseTransaction(dbTransaction);
        }


        public void HasQueryFilter<TEntity>(Expression<Func<TEntity, bool>> filter)
        {
            Type entityType = typeof(TEntity);
            this.HasQueryFilter(entityType, filter);
        }
        public void HasQueryFilter(Type entityType, LambdaExpression filter)
        {
            PublicHelper.CheckNull(filter, nameof(filter));
            List<LambdaExpression> filters;
            if (!this.QueryFilters.TryGetValue(entityType, out filters))
            {
                filters = new List<LambdaExpression>(1);
                this.QueryFilters.Add(entityType, filters);
            }

            filters.Add(filter);
            foreach (var pair in this.PersistedDbContextProviders)
            {
                pair.DbContextProvider.HasQueryFilter(entityType, filter);
            }
        }

        public void HasShardingConfig(Type entityType, IShardingConfig shardingConfig)
        {
            this.ContextShardingConfigs[entityType] = shardingConfig;
        }
        public IShardingConfig FindShardingConfig(Type entityType)
        {
            if (this._contextShardingConfigs == null)
            {
                return null;
            }

            return this._contextShardingConfigs.FindValue(entityType);
        }

        public IDbContextProvider GetDefaultDbContextProvider()
        {
            if (this._defaultDbContextProvider == null)
            {
                if (this.DbContext.DbContextProviderFactory == null)
                {
                    throw new InvalidOperationException("No provider specified.");
                }

                var defaultDbContextProvider = this.DbContext.DbContextProviderFactory.CreateDbContextProvider(this.DbContext);
                this.AppendFeatures(defaultDbContextProvider);

                var physicDataSource = new PhysicDataSource(DbContext.DefaultProviderDataSourceName, this.DbContext.DbContextProviderFactory);
                var pair = new DataSourceDbContextProviderPair(physicDataSource, defaultDbContextProvider);
                this.PersistedDbContextProviders.Add(pair);

                this._defaultDbContextProvider = defaultDbContextProvider;
            }

            this.StartTransactionIfNeed(this._defaultDbContextProvider);
            return this._defaultDbContextProvider;
        }
        public IDbContextProvider GetShardingDbContextProvider()
        {
            if (this._shardingDbContextProvider == null)
            {
                this._shardingDbContextProvider = new ShardingDbContextProvider(this.DbContext);
            }

            return this._shardingDbContextProvider;
        }

        public IDbContextProvider GetPersistedDbContextProvider(IPhysicDataSource dataSource)
        {
            DataSourceDbContextProviderPair pair = this.PersistedDbContextProviders.FirstOrDefault(a => a.DataSource.Name == dataSource.Name);
            if (pair == null)
            {
                IDbContextProvider dbContextProvider = dataSource.DbContextProviderFactory.CreateDbContextProvider(this.DbContext);
                this.AppendFeatures(dbContextProvider);

                pair = new DataSourceDbContextProviderPair(dataSource, dbContextProvider);
                this.PersistedDbContextProviders.Add(pair);
            }

            this.StartTransactionIfNeed(pair.DbContextProvider);
            return new PersistedDbContextProvider(pair.DbContextProvider);
        }
        public ISharedDbContextProviderPool GetDbContextProviderPool(IPhysicDataSource dataSource)
        {
            SharedDbContextProviderPool pool;
            if (this.DbContext.Butler.IsInTransaction)
            {
                pool = new SharedDbContextProviderPool(1, () => this.GetPersistedDbContextProvider(dataSource));
                return pool;
            }

            pool = new SharedDbContextProviderPool(this.DbContext.ShardingOptions.MaxConnectionsPerDataSource, () => dataSource.DbContextProviderFactory.CreateDbContextProvider(this.DbContext));
            return pool;
        }

        void StartTransactionIfNeed(IDbContextProvider dbContextProvider)
        {
            if (this.IsInTransaction)
            {
                if (!dbContextProvider.Session.IsInTransaction)
                {
                    dbContextProvider.Session.BeginTransaction(this.IL);
                }
            }
        }
        void AppendFeatures(IDbContextProvider dbContextProvider)
        {
            dbContextProvider.Session.CommandTimeout = this.CommandTimeout;
            this.AppendQueryFilters(dbContextProvider);
            this.AppendSessionInterceptors(dbContextProvider);
        }
        void AppendQueryFilters(IDbContextProvider dbContextProvider)
        {
            foreach (var kv in this.DbContext.Butler.QueryFilters)
            {
                foreach (var filter in kv.Value)
                {
                    dbContextProvider.HasQueryFilter(kv.Key, filter);
                }
            }
        }
        void AppendSessionInterceptors(IDbContextProvider dbContextProvider)
        {
            foreach (var interceptor in this.DbContext.Butler.DbCommandInterceptors)
            {
                dbContextProvider.Session.AddInterceptor(interceptor);
            }
        }

        class PersistedDbContextProvider : DbContextProviderDecorator, IDbContextProvider
        {
            public PersistedDbContextProvider(IDbContextProvider dbContextProvider) : base(dbContextProvider)
            {
            }

            protected override void Dispose(bool disposing)
            {

            }
        }
    }
}
