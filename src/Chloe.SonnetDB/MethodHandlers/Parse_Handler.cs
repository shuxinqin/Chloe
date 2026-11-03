using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class Parse_Handler : Parse_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            if (exp.Method.ReturnType == typeof(ulong))
            {
                // Candidate for SonnetDB's documented bounded ASCII grammar, not all CLR Parse overloads/cultures.
                generator.SqlBuilder.Append("parse_uint64(");
                exp.Arguments[0].Accept(generator);
                generator.SqlBuilder.Append(")");
                return;
            }

            base.Process(exp, generator);
        }
    }
}
