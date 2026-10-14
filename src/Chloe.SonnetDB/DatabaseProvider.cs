using Chloe.Infrastructure;
using System.Data;

namespace Chloe.SonnetDB
{
    class DatabaseProvider : IDatabaseProvider
    {
        SonnetDBContextProvider _contextProvider;

        public string DatabaseType { get { return "SonnetDB"; } }

        public DatabaseProvider(SonnetDBContextProvider contextProvider)
        {
            this._contextProvider = contextProvider;
        }

        public IDbConnection CreateConnection()
        {
            return new SonnetDBConnection(this._contextProvider.Options.DbConnectionFactory.CreateConnection());
        }

        public IDbExpressionTranslator CreateDbExpressionTranslator()
        {
            return new DbExpressionTranslator(this._contextProvider);
        }

        public string CreateParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            if (name[0] == UtilConstants.ParameterNamePlaceholder[0])
            {
                return name;
            }

            return UtilConstants.ParameterNamePlaceholder + name;
        }
    }
}
