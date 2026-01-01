using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;
using System.Threading.Tasks;

namespace Chloe.SqlServer
{
    public class MsSqlContext : DbContext
    {
        public MsSqlContext(string connString) : this(new DefaultDbConnectionFactory(connString))
        {

        }

        public MsSqlContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public MsSqlContext(IDbConnectionFactory dbConnectionFactory) : this(new MsSqlOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public MsSqlContext(MsSqlOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new MsSqlOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            MsSqlContext dbContext = new MsSqlContext(this.Options.Clone());
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            MsSqlContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            MsSqlContextProvider.SetMethodHandler(methodName, handler);
        }

        /// <summary>
        /// 利用 SqlBulkCopy 批量插入数据。
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="table"></param>
        /// <param name="batchSize">设置 SqlBulkCopy.BatchSize 的值</param>
        /// <param name="bulkCopyTimeout">设置 SqlBulkCopy.BulkCopyTimeout 的值</param>
        /// <param name="keepIdentity">是否保留源自增值。false 由数据库分配自增值</param>
        public virtual void BulkInsert<TEntity>(List<TEntity> entities, string table = null, int? batchSize = null, int? bulkCopyTimeout = null, bool keepIdentity = false)
        {
            var dbContextProvider = (MsSqlContextProvider)this.DefaultDbContextProvider;
            dbContextProvider.BulkInsert(entities, table, batchSize, bulkCopyTimeout, keepIdentity);
        }
        public virtual async Task BulkInsertAsync<TEntity>(List<TEntity> entities, string table = null, int? batchSize = null, int? bulkCopyTimeout = null, bool keepIdentity = false)
        {
            var dbContextProvider = (MsSqlContextProvider)this.DefaultDbContextProvider;
            await dbContextProvider.BulkInsertAsync(entities, table, batchSize, bulkCopyTimeout, keepIdentity);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        MsSqlOptions _options;

        public DbContextProviderFactory(MsSqlOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider(IDbContext dbContext)
        {
            return new MsSqlContextProvider(this._options, dbContext as DbContext);
        }
    }
}
