using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;

namespace Chloe.Dameng
{
    //hongyl 加入dbContext参数
    public class DamengContext : DbContext
    {
        public DamengContext(Func<IDbConnection> dbConnectionFactory) : this(new DbConnectionFactory(dbConnectionFactory))
        {

        }

        public DamengContext(IDbConnectionFactory dbConnectionFactory) : this(new DamengOptions() { DbConnectionFactory = dbConnectionFactory })
        {

        }

        public DamengContext(DamengOptions options) : base(options, new DbContextProviderFactory(options))
        {
            this.Options = options;
        }

        public new DamengOptions Options { get; private set; }

        protected override DbContext CloneImpl()
        {
            DamengContext dbContext = new DamengContext(this.Options.Clone());
            return dbContext;
        }

        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            DamengContextProvider.SetPropertyHandler(propertyName, handler);
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            DamengContextProvider.SetMethodHandler(methodName, handler);
        }
    }

    class DbContextProviderFactory : IDbContextProviderFactory
    {
        DamengOptions _options;

        public DbContextProviderFactory(DamengOptions options)
        {
            PublicHelper.CheckNull(options);
            this._options = options;
        }

        public IDbContextProvider CreateDbContextProvider(IDbContext dbContext)
        {
            return new DamengContextProvider(this._options, dbContext as DbContext);
        }
    }
}
