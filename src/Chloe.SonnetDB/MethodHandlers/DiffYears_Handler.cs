using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class DiffYears_Handler : DiffYears_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.Append_DateDiff(generator, exp.Arguments[0], exp.Arguments[1], "year");
        }
    }
}