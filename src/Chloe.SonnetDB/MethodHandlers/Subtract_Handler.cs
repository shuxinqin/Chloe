using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Subtract_Handler : Subtract_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            throw new NotSupportedException("SonnetDB does not support CLR TimeSpan duration results from DateTime.Subtract(). See https://github.com/IoTSharp/SonnetDB/issues/252.");
        }
    }
}
