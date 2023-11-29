﻿using Chloe.Visitors;
using Chloe.DbExpressions;
using System.Reflection;
using Chloe.RDBMS;

namespace Chloe.PostgreSQL
{
    class EvaluableDbExpressionTransformer : EvaluableDbExpressionTransformerBase
    {
        static EvaluableDbExpressionTransformer _transformer = new EvaluableDbExpressionTransformer();

        static EvaluableDbExpressionTransformer()
        {

        }

        public EvaluableDbExpressionTransformer()
        {

        }

        public static DbExpression Transform(DbExpression exp)
        {
            return exp.Accept(_transformer);
        }

        protected override Dictionary<string, IPropertyHandler[]> GetPropertyHandlers()
        {
            return SqlGenerator.PropertyHandlerDic;
        }

        protected override Dictionary<string, IMethodHandler[]> GetMethodHandlers()
        {
            return SqlGenerator.MethodHandlerDic;
        }
    }
}
