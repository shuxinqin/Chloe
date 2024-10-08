﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.MethodHandlers;

namespace Chloe.Oracle.MethodHandlers
{
    class Substring_Handler : Substring_HandlerBase
    {
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("SUBSTR(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(",");

            DbExpression arg1 = exp.Arguments[0];
            if (arg1.NodeType == DbExpressionType.Constant)
            {
                int startIndex = (int)(((DbConstantExpression)arg1).Value) + 1;
                generator.SqlBuilder.Append(startIndex.ToString());
            }
            else
            {
                arg1.Accept(generator);
                generator.SqlBuilder.Append(" + 1");
            }

            generator.SqlBuilder.Append(",");
            if (exp.Method == PublicConstants.MethodInfo_String_Substring_Int32)
            {
                var string_LengthExp = new DbMemberAccessExpression(PublicConstants.PropertyInfo_String_Length, exp.Object);
                string_LengthExp.Accept(generator);
            }
            else if (exp.Method == PublicConstants.MethodInfo_String_Substring_Int32_Int32)
            {
                exp.Arguments[1].Accept(generator);
            }
            else
                throw PublicHelper.MakeNotSupportedMethodException(exp.Method);

            generator.SqlBuilder.Append(")");
        }
    }
}
