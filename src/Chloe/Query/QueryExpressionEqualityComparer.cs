using Chloe.QueryExpressions;
using System.Linq.Expressions;

namespace Chloe.Query
{
    public class QueryExpressionEqualityComparer : IEqualityComparer<QueryExpression?>
    {
        public static QueryExpressionEqualityComparer Instance { get; } = new QueryExpressionEqualityComparer();

        public bool Equals(QueryExpression x, QueryExpression y)
        {
            return new QueryExpressionComparer().Compare(x, y);
        }

        public int GetHashCode(QueryExpression obj)
        {
            if (obj == null)
            {
                return 0;
            }

            HashCode hash = new HashCode();

            hash.Add(obj.NodeType);
            hash.Add(obj.ElementType);
            AddQeuryExpressionToHashIfNotNull(obj.PrevExpression);

            unchecked
            {
                switch (obj)
                {
                    case RootQueryExpression rootQueryExpression:
                        hash.Add(rootQueryExpression.ExplicitTable);
                        hash.Add(rootQueryExpression.Lock);
                        for (int i = 0; i < rootQueryExpression.GlobalFilters.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(rootQueryExpression.GlobalFilters[i]);
                        }
                        for (int i = 0; i < rootQueryExpression.ContextFilters.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(rootQueryExpression.ContextFilters[i]);
                        }
                        break;
                    case WhereExpression whereExpression:
                        AddExpressionToHashIfNotNull(whereExpression.Predicate);
                        break;
                    case OrderExpression orderExpression:
                        AddExpressionToHashIfNotNull(orderExpression.KeySelector);
                        break;
                    case SelectExpression selectExpression:
                        AddExpressionToHashIfNotNull(selectExpression.Selector);
                        break;
                    case SkipExpression skipExpression:
                        hash.Add(skipExpression.Count);
                        break;
                    case TakeExpression takeExpression:
                        hash.Add(takeExpression.Count);
                        break;
                    case PagingExpression pagingExpression:
                        hash.Add(pagingExpression.PageNumber);
                        hash.Add(pagingExpression.PageSize);
                        break;
                    case AggregateQueryExpression aggregateQueryExpression:
                        hash.Add(aggregateQueryExpression.Method);
                        for (int i = 0; i < aggregateQueryExpression.Arguments.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(aggregateQueryExpression.Arguments[i]);
                        }
                        break;
                    case BindTwoWayExpression bindTwoWayExpression:
                        hash.Add(typeof(BindTwoWayExpression));
                        break;
                    case DistinctExpression distinctExpression:
                        hash.Add(typeof(DistinctExpression));
                        break;
                    case ExcludeExpression excludeExpression:
                        AddExpressionToHashIfNotNull(excludeExpression.Field);
                        break;
                    case GroupingQueryExpression groupingQueryExpression:
                        AddExpressionToHashIfNotNull(groupingQueryExpression.Selector);
                        for (int i = 0; i < groupingQueryExpression.GroupKeySelectors.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(groupingQueryExpression.GroupKeySelectors[i]);
                        }
                        for (int i = 0; i < groupingQueryExpression.HavingPredicates.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(groupingQueryExpression.HavingPredicates[i]);
                        }
                        for (int i = 0; i < groupingQueryExpression.Orderings.Count; i++)
                        {
                            AddExpressionToHashIfNotNull(groupingQueryExpression.Orderings[i].KeySelector);
                            hash.Add(groupingQueryExpression.Orderings[i].OrderType);
                        }
                        break;
                    case IgnoreAllFiltersExpression ignoreAllFiltersExpression:
                        hash.Add(typeof(IgnoreAllFiltersExpression));
                        break;
                    case IncludeExpression includeExpression:
                        AddNavigationNodeToHashIfNotNull(includeExpression.NavigationNode);
                        break;
                    case JoinQueryExpression joinQueryExpression:
                        AddExpressionToHashIfNotNull(joinQueryExpression.Selector);
                        for (int i = 0; i < joinQueryExpression.JoinedQueries.Count; i++)
                        {
                            hash.Add(joinQueryExpression.JoinedQueries[i].JoinType);
                            AddExpressionToHashIfNotNull(joinQueryExpression.JoinedQueries[i].Condition);
                            hash.Add(joinQueryExpression.JoinedQueries[i].Query, this);
                        }
                        break;
                    case TrackingExpression trackingExpression:
                        hash.Add(typeof(TrackingExpression));
                        break;
                    case SplitQueryExpression splitQueryExpression:
                        hash.Add(typeof(SplitQueryExpression));
                        break;
                }
            }

            return hash.ToHashCode();

            void AddQeuryExpressionToHashIfNotNull(QueryExpression? t)
            {
                if (t != null)
                {
                    hash.Add(t, this);
                }
            }
            void AddExpressionToHashIfNotNull(Expression? t)
            {
                if (t != null)
                {
                    hash.Add(ExpressionEqualityComparer.Instance.GetHashCode(t));
                }
            }

            void AddNavigationNodeToHashIfNotNull(NavigationNode navigationNode)
            {
                if (navigationNode == null)
                    return;

                hash.Add(navigationNode.Property);
                AddExpressionToHashIfNotNull(navigationNode.Condition);
                for (int i = 0; i < navigationNode.ExcludedFields.Count; i++)
                {
                    AddExpressionToHashIfNotNull(navigationNode.ExcludedFields[i]);
                }

                for (int i = 0; i < navigationNode.GlobalFilters.Count; i++)
                {
                    AddExpressionToHashIfNotNull(navigationNode.GlobalFilters[i]);
                }

                for (int i = 0; i < navigationNode.ContextFilters.Count; i++)
                {
                    AddExpressionToHashIfNotNull(navigationNode.ContextFilters[i]);
                }

                AddNavigationNodeToHashIfNotNull(navigationNode.Next);
            }

        }

        private struct QueryExpressionComparer
        {
            public bool Compare(QueryExpression? left, QueryExpression? right)
            {
                if (left == right)
                {
                    return true;
                }

                if (left == null || right == null)
                {
                    return false;
                }

                if (left.NodeType != right.NodeType)
                {
                    return false;
                }

                if (left.ElementType != right.ElementType)
                {
                    return false;
                }

                if (!this.Compare(left.PrevExpression, right.PrevExpression))
                {
                    return false;
                }

                switch (left)
                {
                    case RootQueryExpression leftQueryExpression:
                        return CompareRootQuery(leftQueryExpression, (RootQueryExpression)right);
                    case WhereExpression leftQueryExpression:
                        return CompareWhere(leftQueryExpression, (WhereExpression)right);
                    case OrderExpression leftQueryExpression:
                        return CompareOrder(leftQueryExpression, (OrderExpression)right);
                    case SelectExpression leftQueryExpression:
                        return CompareSelect(leftQueryExpression, (SelectExpression)right);
                    case SkipExpression leftQueryExpression:
                        return CompareSkip(leftQueryExpression, (SkipExpression)right);
                    case TakeExpression leftQueryExpression:
                        return CompareTake(leftQueryExpression, (TakeExpression)right);
                    case PagingExpression leftQueryExpression:
                        return ComparePaging(leftQueryExpression, (PagingExpression)right);
                    case AggregateQueryExpression leftQueryExpression:
                        return CompareAggregateQuery(leftQueryExpression, (AggregateQueryExpression)right);
                    case BindTwoWayExpression leftQueryExpression:
                        return CompareBindTwoWay(leftQueryExpression, (BindTwoWayExpression)right);
                    case DistinctExpression leftQueryExpression:
                        return CompareDistinct(leftQueryExpression, (DistinctExpression)right);
                    case ExcludeExpression leftQueryExpression:
                        return CompareExclude(leftQueryExpression, (ExcludeExpression)right);
                    case GroupingQueryExpression leftQueryExpression:
                        return CompareGroupingQuery(leftQueryExpression, (GroupingQueryExpression)right);
                    case IgnoreAllFiltersExpression leftQueryExpression:
                        return CompareIgnoreAllFilters(leftQueryExpression, (IgnoreAllFiltersExpression)right);
                    case IncludeExpression leftQueryExpression:
                        return CompareInclude(leftQueryExpression, (IncludeExpression)right);
                    case JoinQueryExpression leftQueryExpression:
                        return CompareJoinQuery(leftQueryExpression, (JoinQueryExpression)right);
                    case TrackingExpression leftQueryExpression:
                        return CompareTracking(leftQueryExpression, (TrackingExpression)right);
                    case SplitQueryExpression leftQueryExpression:
                        return CompareSplitQuery(leftQueryExpression, (SplitQueryExpression)right);
                    default:
                        throw new NotSupportedException();
                }
            }

            bool CompareRootQuery(RootQueryExpression left, RootQueryExpression right)
            {
                if (left.ExplicitTable != right.ExplicitTable || left.Lock != right.Lock)
                {
                    return false;
                }

                if (left.GlobalFilters.Count != right.GlobalFilters.Count)
                {
                    return false;
                }

                if (left.ContextFilters.Count != right.ContextFilters.Count)
                {
                    return false;
                }

                for (int i = 0; i < left.GlobalFilters.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.GlobalFilters[i], right.GlobalFilters[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < left.ContextFilters.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.ContextFilters[i], right.ContextFilters[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool CompareWhere(WhereExpression left, WhereExpression right)
            {
                return ExpressionEqualityComparer.Instance.Equals(left.Predicate, right.Predicate);
            }

            bool CompareOrder(OrderExpression left, OrderExpression right)
            {
                return ExpressionEqualityComparer.Instance.Equals(left.KeySelector, right.KeySelector);
            }

            bool CompareSelect(SelectExpression left, SelectExpression right)
            {
                return ExpressionEqualityComparer.Instance.Equals(left.Selector, right.Selector);
            }

            bool CompareSkip(SkipExpression left, SkipExpression right)
            {
                return left.Count == right.Count;
            }

            bool CompareTake(TakeExpression left, TakeExpression right)
            {
                return left.Count == right.Count;
            }

            bool ComparePaging(PagingExpression left, PagingExpression right)
            {
                return left.PageNumber == right.PageNumber && left.PageSize == right.PageSize;
            }

            bool CompareAggregateQuery(AggregateQueryExpression left, AggregateQueryExpression right)
            {
                if (left.Method != right.Method)
                    return false;

                for (int i = 0; i < left.Arguments.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.Arguments[i], right.Arguments[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool CompareBindTwoWay(BindTwoWayExpression left, BindTwoWayExpression right)
            {
                return true;
            }

            bool CompareDistinct(DistinctExpression left, DistinctExpression right)
            {
                return true;
            }

            bool CompareExclude(ExcludeExpression left, ExcludeExpression right)
            {
                return ExpressionEqualityComparer.Instance.Equals(left.Field, right.Field);
            }

            bool CompareGroupingQuery(GroupingQueryExpression left, GroupingQueryExpression right)
            {
                if (!ExpressionEqualityComparer.Instance.Equals(left.Selector, right.Selector))
                {
                    return false;
                }

                for (int i = 0; i < left.GroupKeySelectors.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.GroupKeySelectors[i], right.GroupKeySelectors[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < left.HavingPredicates.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.HavingPredicates[i], right.HavingPredicates[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < left.Orderings.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.Orderings[i].KeySelector, right.Orderings[i].KeySelector))
                    {
                        return false;
                    }

                    if (left.Orderings[i].OrderType != right.Orderings[i].OrderType)
                    {
                        return false;
                    }
                }


                return true;
            }

            bool CompareIgnoreAllFilters(IgnoreAllFiltersExpression left, IgnoreAllFiltersExpression right)
            {
                return true;
            }

            bool CompareInclude(IncludeExpression left, IncludeExpression right)
            {
                return this.CompareNavigationNode(left.NavigationNode, right.NavigationNode);
            }

            bool CompareJoinQuery(JoinQueryExpression left, JoinQueryExpression right)
            {
                if (!ExpressionEqualityComparer.Instance.Equals(left.Selector, right.Selector))
                {
                    return false;
                }

                for (int i = 0; i < left.JoinedQueries.Count; i++)
                {
                    if (left.JoinedQueries[i].JoinType != right.JoinedQueries[i].JoinType)
                    {
                        return false;
                    }

                    if (!ExpressionEqualityComparer.Instance.Equals(left.JoinedQueries[i].Condition, right.JoinedQueries[i].Condition))
                    {
                        return false;
                    }

                    if (!this.Compare(left.JoinedQueries[i].Query, right.JoinedQueries[i].Query))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool CompareTracking(TrackingExpression left, TrackingExpression right)
            {
                return true;
            }

            bool CompareSplitQuery(SplitQueryExpression left, SplitQueryExpression right)
            {
                return true;
            }

            bool CompareNavigationNode(NavigationNode left, NavigationNode right)
            {
                if (left == right)
                    return true;

                if (left == null || right == null)
                {
                    return false;
                }

                if (left.Property != right.Property)
                    return false;

                if (!ExpressionEqualityComparer.Instance.Equals(left.Condition, right.Condition))
                {
                    return false;
                }


                for (int i = 0; i < left.ExcludedFields.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.ExcludedFields[i], right.ExcludedFields[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < left.GlobalFilters.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.GlobalFilters[i], right.GlobalFilters[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < left.ContextFilters.Count; i++)
                {
                    if (!ExpressionEqualityComparer.Instance.Equals(left.ContextFilters[i], right.ContextFilters[i]))
                    {
                        return false;
                    }
                }

                return CompareNavigationNode(left.Next, right.Next);
            }
        }
    }
}
