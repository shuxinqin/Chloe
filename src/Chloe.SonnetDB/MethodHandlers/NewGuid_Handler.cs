using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class NewGuid_Handler : NewGuid_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            throw new NotSupportedException("SonnetDB does not provide a server side UUID function for Guid.NewGuid(). See https://github.com/IoTSharp/SonnetDB/issues/258.");
        }
    }
}
