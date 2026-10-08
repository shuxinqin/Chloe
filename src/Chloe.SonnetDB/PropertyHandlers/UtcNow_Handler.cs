using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SonnetDB.PropertyHandlers
{
    class UtcNow_Handler : UtcNow_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            Utils.EnsureSupportedMappingType(exp.Type);
            generator.SqlBuilder.Append("TO_DATETIME(CURRENT_UTC_DATETIME())");
        }
    }
}
