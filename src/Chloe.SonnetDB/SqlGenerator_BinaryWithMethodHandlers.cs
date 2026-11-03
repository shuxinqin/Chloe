using Chloe.DbExpressions;
using Chloe.RDBMS;
using System.Reflection;

namespace Chloe.SonnetDB
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        static Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> InitBinaryWithMethodHandlers()
        {
            var binaryWithMethodHandlers = new Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>>();
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_String_String, StringConcat);
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_Object_Object, StringConcat);

            var ret = PublicHelper.Clone(binaryWithMethodHandlers);
            return ret;
        }

        static void StringConcat(DbBinaryExpression exp, SqlGeneratorBase generator)
        {
            List<DbExpression> operands = new List<DbExpression>();
            operands.Add(exp.Right);

            DbExpression left = exp.Left;
            DbAddExpression e = null;
            while ((e = (left as DbAddExpression)) != null && (e.Method == PublicConstants.MethodInfo_String_Concat_String_String || e.Method == PublicConstants.MethodInfo_String_Concat_Object_Object))
            {
                operands.Add(e.Right);
                left = e.Left;
            }

            operands.Add(left);

            generator.SqlBuilder.Append("CONCAT(");
            for (int i = operands.Count - 1; i >= 0; i--)
            {
                if (i < operands.Count - 1)
                    generator.SqlBuilder.Append(",");
                DbExpression operand = operands[i];
                if (operand.Type != PublicConstants.TypeOfString)
                    operand = new DbConvertExpression(PublicConstants.TypeOfString, operand);
                operand.Accept(generator);
            }
            generator.SqlBuilder.Append(")");
        }
    }
}
