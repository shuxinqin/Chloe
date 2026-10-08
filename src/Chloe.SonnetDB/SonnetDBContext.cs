using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;

namespace Chloe.SonnetDB
{
    public class SonnetDBContext : DbContext
    {
        public SonnetDBContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public SonnetDBContext(IDbConnectionFactory dbConnectionFactory) : this(new SonnetDBOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public SonnetDBContext(SonnetDBOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new SonnetDBOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            SonnetDBContext dbContext = new SonnetDBContext(this.Options.Clone());
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            SonnetDBContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            SonnetDBContextProvider.SetMethodHandler(methodName, handler);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        SonnetDBOptions _options;

        public DbContextProviderFactory(SonnetDBOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider(IDbContext dbContext)
        {
            return new SonnetDBContextProvider(this._options, dbContext as DbContext);
        }
    }
}
