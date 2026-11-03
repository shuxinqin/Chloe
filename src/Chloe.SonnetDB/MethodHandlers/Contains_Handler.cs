using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Contains_Handler : Contains_HandlerBase
    {
        protected override void Method_String_Contains(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("CONTAINS(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(",");
            exp.Arguments.First().Accept(generator);
            generator.SqlBuilder.Append(")");
        }
    }
}
