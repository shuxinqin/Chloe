﻿using Chloe.DbExpressions;
using System.Reflection;

namespace Chloe.RDBMS.MethodHandlers
{
    public class IsEqual_HandlerBase : MethodHandlerBase
    {
        public override bool CanProcess(DbMethodCallExpression exp)
        {
            MethodInfo method = exp.Method;
            return PublicHelper.Is_Sql_IsEqual_Method(method);
        }
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            DbExpression left = exp.Arguments[0];
            DbExpression right = exp.Arguments[1];

            left = DbExpressionExtension.StripInvalidConvert(left);
            right = DbExpressionExtension.StripInvalidConvert(right);

            //明确 left right 其中一边一定为 null
            if (DbExpressionExtension.AffirmExpressionRetValueIsNull(right, generator.Options.TreatEmptyStringAsNull))
            {
                left.Accept(generator);
                generator.SqlBuilder.Append(" IS NULL");
                return;
            }

            if (DbExpressionExtension.AffirmExpressionRetValueIsNull(left, generator.Options.TreatEmptyStringAsNull))
            {
                right.Accept(generator);
                generator.SqlBuilder.Append(" IS NULL");
                return;
            }

            left.Accept(generator);
            generator.SqlBuilder.Append(" = ");
            right.Accept(generator);
        }
    }
}
