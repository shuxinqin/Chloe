namespace Chloe
{
    public class DataSourceDbContextProviderPair
    {
        public DataSourceDbContextProviderPair(IPhysicDataSource dataSource, IDbContextProvider dbContextProvider)
        {
            this.DataSource = dataSource;
            this.DbContextProvider = dbContextProvider;
        }

        public IPhysicDataSource DataSource { get; set; }
        public IDbContextProvider DbContextProvider { get; set; }
    }
}
