﻿using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.QueryExpressions
{
    public class AggregateQueryExpression : QueryExpression
    {
        MethodInfo _method;
        ReadOnlyCollection<Expression> _arguments;

        public AggregateQueryExpression(QueryExpression prevExpression, MethodInfo method, IList<Expression> arguments) : base(QueryExpressionType.Aggregate, method.ReturnType, prevExpression)
        {
            this._method = method;
            this._arguments = new ReadOnlyCollection<Expression>(arguments);
        }

        public MethodInfo Method { get { return this._method; } }
        public ReadOnlyCollection<Expression> Arguments { get { return this._arguments; } }


        public override T Accept<T>(IQueryExpressionVisitor<T> visitor)
        {
            return visitor.VisitAggregateQuery(this);
        }
    }
}
