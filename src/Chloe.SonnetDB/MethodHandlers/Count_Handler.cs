using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Count_Handler : Count_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            //Sql.Count<TField>(TField field)
            if (exp.Arguments.Count == 1)
            {
                SqlGenerator.Aggregate_Count(generator, exp.Arguments[0]);
                return;
            }

            SqlGenerator.Aggregate_Count(generator);
        }
    }
}
