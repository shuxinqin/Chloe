using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.SonnetDB.MethodHandlers
{
    class NextValueForSequence_Handler : NextValueForSequence_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            string sequenceName = (string)exp.Arguments[0].Evaluate();
            string schema = (string)exp.Arguments[1].Evaluate();
            if (string.IsNullOrEmpty(sequenceName)) throw new ArgumentException("The sequence name cannot be empty.");
            if (!string.IsNullOrEmpty(schema)) throw new NotSupportedException("SonnetDB sequences have one database-wide namespace.");
            generator.SqlBuilder.Append("nextval('", Utils.QuoteName(sequenceName).Replace("'", "''"), "')");
        }
    }
}
