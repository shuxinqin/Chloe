﻿namespace Chloe.QueryExpressions
{
    public class BindTwoWayExpression : QueryExpression
    {
        public BindTwoWayExpression(Type elementType, QueryExpression prevExpression) : base(QueryExpressionType.BindTwoWay, elementType, prevExpression)
        {

        }

        public override T Accept<T>(QueryExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
