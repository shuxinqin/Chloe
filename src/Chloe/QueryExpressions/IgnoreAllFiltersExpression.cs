﻿namespace Chloe.QueryExpressions
{
    public class IgnoreAllFiltersExpression : QueryExpression
    {
        public IgnoreAllFiltersExpression(Type elementType, QueryExpression prevExpression) : base(QueryExpressionType.IgnoreAllFilters, elementType, prevExpression)
        {

        }

        public override T Accept<T>(QueryExpressionVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
