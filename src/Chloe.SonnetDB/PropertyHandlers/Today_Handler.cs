using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SonnetDB.PropertyHandlers
{
    class Today_Handler : Today_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("TO_DATETIME(DATE_ONLY(CURRENT_DATETIME()))");
        }
    }
}
