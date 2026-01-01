using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;

namespace Chloe.PostgreSQL
{
    public class PostgreSQLContext : DbContext
    {
        public PostgreSQLContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public PostgreSQLContext(IDbConnectionFactory dbConnectionFactory) : this(new PostgreSQLOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public PostgreSQLContext(PostgreSQLOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new PostgreSQLOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            PostgreSQLContext dbContext = new PostgreSQLContext(this.Options.Clone());
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            PostgreSQLContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            PostgreSQLContextProvider.SetMethodHandler(methodName, handler);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        PostgreSQLOptions _options;

        public DbContextProviderFactory(PostgreSQLOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider(IDbContext dbContext)
        {
            return new PostgreSQLContextProvider(this._options, dbContext as DbContext);
        }
    }
}
