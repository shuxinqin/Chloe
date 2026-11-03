using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SonnetDB.PropertyHandlers
{
    class Year_Handler : Year_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.DbFunction_DATEPART(generator, "year", exp.Expression);
        }
    }
}