using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SonnetDB.PropertyHandlers
{
    class Day_Handler : Day_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.DbFunction_DATEPART(generator, "day", exp.Expression);
        }
    }
}