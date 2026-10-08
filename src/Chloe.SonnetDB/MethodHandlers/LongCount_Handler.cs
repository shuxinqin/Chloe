using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class LongCount_Handler : LongCount_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            //Sql.LongCount<TField>(TField field)
            if (exp.Arguments.Count == 1)
            {
                SqlGenerator.Aggregate_LongCount(generator, exp.Arguments[0]);
                return;
            }

            SqlGenerator.Aggregate_LongCount(generator);
        }
    }
}
