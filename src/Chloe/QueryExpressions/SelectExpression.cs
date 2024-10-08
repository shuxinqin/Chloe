﻿using System.Linq.Expressions;

namespace Chloe.QueryExpressions
{
    public class SelectExpression : QueryExpression
    {
        LambdaExpression _selector;
        public SelectExpression(Type elementType, QueryExpression prevExpression, LambdaExpression selector) : base(QueryExpressionType.Select, elementType, prevExpression)
        {
            this._selector = selector;
        }
        public LambdaExpression Selector
        {
            get { return this._selector; }
        }
        public override T Accept<T>(IQueryExpressionVisitor<T> visitor)
        {
            return visitor.VisitSelect(this);
        }
    }
}
