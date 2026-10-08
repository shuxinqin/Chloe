using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class NextValueForSequence_Handler : NextValueForSequence_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            throw new NotSupportedException("SonnetDB does not support database sequences. See https://github.com/IoTSharp/SonnetDB/issues/231.");
        }
    }
}
