using Chloe.Data;
using System.Data;

namespace Chloe.SonnetDB
{
    class SonnetDBConnection : DbConnectionDecorator
    {
        public SonnetDBConnection(IDbConnection connection) : base(connection)
        {
        }

        public override IDbTransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            if (isolationLevel != IsolationLevel.ReadCommitted && isolationLevel != IsolationLevel.Serializable && isolationLevel != IsolationLevel.Unspecified)
                throw new NotSupportedException("SonnetDB supports ReadCommitted and Serializable transactions; other isolation levels require an explicit supported mapping. See https://github.com/IoTSharp/SonnetDB/issues/225.");

            return base.BeginTransaction(isolationLevel);
        }
    }
}
