using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;

namespace Chloe.SQLite
{
    public class SQLiteContext : DbContext
    {
        public SQLiteContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public SQLiteContext(IDbConnectionFactory dbConnectionFactory) : this(new SQLiteOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public SQLiteContext(SQLiteOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new SQLiteOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            SQLiteContext dbContext = new SQLiteContext(this.Options.Clone());
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            SQLiteContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            SQLiteContextProvider.SetMethodHandler(methodName, handler);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        SQLiteOptions _options;

        public DbContextProviderFactory(SQLiteOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider(IDbContext dbContext)
        {
            return new SQLiteContextProvider(this._options, dbContext as DbContext);
        }
    }
}
