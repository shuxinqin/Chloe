using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class TrimStart_Handler : TrimStart_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            PublicHelper.EnsureTrimCharArgumentIsSpaces(exp.Arguments[0]);

            generator.SqlBuilder.Append("LTRIM(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(")");
        }
    }
}
