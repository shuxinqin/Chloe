﻿using Chloe.DbExpressions;
using System.Reflection;

namespace Chloe.RDBMS.MethodHandlers
{
    public class Equals_HandlerBase : MethodHandlerBase
    {
        public override bool CanProcess(DbMethodCallExpression exp)
        {
            MethodInfo method = exp.Method;

            if (method.ReturnType != PublicConstants.TypeOfBoolean || method.IsStatic || exp.Arguments.Count != 1)
                return false;

            return true;
        }
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            DbExpression right = exp.Arguments[0];
            if (right.Type != exp.Object.Type)
            {
                right = DbExpression.Convert(right, exp.Object.Type);
            }

            DbExpression.Equal(exp.Object, right).Accept(generator);
        }
    }
}
