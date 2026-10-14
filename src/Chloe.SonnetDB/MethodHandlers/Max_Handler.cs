using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Max_Handler : Max_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.Aggregate_Max(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
    }
}
