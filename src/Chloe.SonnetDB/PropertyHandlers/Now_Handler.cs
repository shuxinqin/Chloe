using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SonnetDB.PropertyHandlers
{
    class Now_Handler : Now_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            Utils.EnsureSupportedMappingType(exp.Type);
            generator.SqlBuilder.Append("TO_DATETIME(CURRENT_DATETIME())");
        }
    }
}
