using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class EndsWith_Handler : EndsWith_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("ENDS_WITH(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(",");
            exp.Arguments.First().Accept(generator);
            generator.SqlBuilder.Append(")");
        }
    }

}
