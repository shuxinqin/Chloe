using Chloe.Infrastructure;
using System.Data;

#if NET6 || NET8 || NET10
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif

namespace Chloe.SqlServer
{
    public class DefaultDbConnectionFactory : IDbConnectionFactory
    {
        string _connString;
        public DefaultDbConnectionFactory(string connString)
        {
            PublicHelper.CheckNull(connString, "connString");

            this._connString = connString;
        }
        public IDbConnection CreateConnection()
        {
            SqlConnection conn = new SqlConnection(this._connString);
            return conn;
        }
    }
}
