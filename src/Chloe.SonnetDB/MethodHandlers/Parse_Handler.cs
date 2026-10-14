using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Parse_Handler : Parse_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            if (exp.Method.ReturnType == typeof(ulong))
                throw new NotSupportedException("SonnetDB's DECIMAL conversion does not enforce the CLR UInt64.Parse integer contract. See https://github.com/IoTSharp/SonnetDB/issues/260.");

            base.Process(exp, generator);
        }
    }
}
