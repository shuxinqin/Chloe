using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Subtract_Handler : Subtract_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(" - ");
            exp.Arguments[0].Accept(generator);
            generator.SqlBuilder.Append(")");
        }
    }
}
