﻿namespace Chloe.QueryExpressions
{
    public class TakeExpression : QueryExpression
    {
        int _count;
        public TakeExpression(Type elementType, QueryExpression prevExpression, int count) : base(QueryExpressionType.Take, elementType, prevExpression)
        {
            this.CheckInputCount(count);
            this._count = count;
        }

        public int Count
        {
            get { return this._count; }
        }
        void CheckInputCount(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("count 小于 0");
            }
        }

        public override T Accept<T>(IQueryExpressionVisitor<T> visitor)
        {
            return visitor.VisitTake(this);
        }
    }
}
