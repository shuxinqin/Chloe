
namespace Chloe.SonnetDB
{
    public class SonnetDBOptions : DbOptions
    {
        public SonnetDBOptions()
        {
            this.MaxNumberOfParameters = 999;
            this.MaxInItems = 999;
        }

        public SonnetDBOptions Clone()
        {
            SonnetDBOptions options = new SonnetDBOptions()
            {
                DbConnectionFactory = this.DbConnectionFactory,
                InsertStrategy = this.InsertStrategy,
                MaxNumberOfParameters = this.MaxNumberOfParameters,
                MaxInItems = this.MaxInItems,
                DefaultBatchSizeForInsertRange = this.DefaultBatchSizeForInsertRange,
                TreatEmptyStringAsNull = this.TreatEmptyStringAsNull
            };

            return options;
        }
    }
}
