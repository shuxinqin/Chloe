﻿using Chloe.DbExpressions;
using Chloe.QueryExpressions;
using Chloe.Query.Visitors;
using Chloe.Utility;
using System.Linq.Expressions;
using Chloe.Visitors;
using System.Reflection;

namespace Chloe.Query.QueryState
{
    abstract class QueryStateBase : IQueryState
    {
        QueryModel _queryModel;
        protected QueryStateBase(QueryContext context, QueryModel queryModel)
        {
            this.QueryContext = context;
            this._queryModel = queryModel;
        }

        public QueryContext QueryContext { get; set; }
        public QueryModel QueryModel { get { return this._queryModel; } }

        public virtual IQueryState Accept(WhereExpression exp)
        {
            ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(exp.Predicate.Parameters[0], this._queryModel.ResultModel);

            DbExpression whereCondition = FilterPredicateParser.Parse(this.QueryContext, exp.Predicate, scopeParameters, this._queryModel.ScopeTables, this._queryModel);
            this._queryModel.AppendCondition(whereCondition);

            return this;
        }
        public virtual IQueryState Accept(OrderExpression exp)
        {
            if (exp.NodeType == QueryExpressionType.OrderBy || exp.NodeType == QueryExpressionType.OrderByDesc)
                this._queryModel.Orderings.Clear();

            DbOrdering ordering = ParseOrderExpression(exp);

            if (this._queryModel.InheritOrderings)
            {
                this._queryModel.Orderings.Clear();
                this._queryModel.InheritOrderings = false;
            }

            this._queryModel.Orderings.Add(ordering);

            return this;
        }
        public virtual IQueryState Accept(SelectExpression exp)
        {
            QueryModel result = this.CreateNewQueryModel(exp.Selector);
            return this.CreateQueryState(result);
        }
        public virtual IQueryState Accept(SkipExpression exp)
        {
            SkipQueryState state = new SkipQueryState(this.QueryContext, this.QueryModel, exp.Count);
            return state;
        }
        public virtual IQueryState Accept(TakeExpression exp)
        {
            TakeQueryState state = new TakeQueryState(this.QueryContext, this.QueryModel, exp.Count);
            return state;
        }
        public virtual IQueryState Accept(AggregateQueryExpression exp)
        {
            List<DbExpression> dbArguments = new List<DbExpression>(exp.Arguments.Count);
            foreach (Expression argument in exp.Arguments)
            {
                var arg = (LambdaExpression)argument;
                ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(arg.Parameters[0], this._queryModel.ResultModel);

                var dbArgument = GeneralExpressionParser.Parse(this.QueryContext, arg, scopeParameters, this._queryModel.ScopeTables, this.QueryModel);
                dbArguments.Add(dbArgument);
            }

            DbAggregateExpression dbAggregateExp = new DbAggregateExpression(exp.ElementType, exp.Method, dbArguments);
            PrimitiveObjectModel resultModel = new PrimitiveObjectModel(this.QueryModel.Options, exp.ElementType, dbAggregateExp);

            QueryModel queryModel = new QueryModel(this._queryModel.Options, this._queryModel.ScopeParameters, this._queryModel.ScopeTables, this._queryModel.TouchedTables);

            queryModel.ResultModel = resultModel;
            queryModel.FromTable = this._queryModel.FromTable;
            queryModel.AppendCondition(this._queryModel.Condition);
            queryModel.GlobalFilters.AppendRange(this._queryModel.GlobalFilters);
            queryModel.ContextFilters.AppendRange(this._queryModel.ContextFilters);

            AggregateQueryState state = new AggregateQueryState(this.QueryContext, queryModel);
            return state;
        }
        public virtual IQueryState Accept(GroupingQueryExpression exp)
        {
            foreach (LambdaExpression keySelector in exp.GroupKeySelectors)
            {
                ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(keySelector.Parameters[0], this._queryModel.ResultModel);

                this._queryModel.GroupSegments.AppendRange(GroupKeySelectorParser.Parse(this.QueryContext, keySelector, scopeParameters, this._queryModel.ScopeTables, this._queryModel));
            }

            foreach (LambdaExpression havingPredicate in exp.HavingPredicates)
            {
                ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(havingPredicate.Parameters[0], this._queryModel.ResultModel);

                var havingCondition = FilterPredicateParser.Parse(this.QueryContext, havingPredicate, scopeParameters, this._queryModel.ScopeTables, this._queryModel);
                this._queryModel.AppendHavingCondition(havingCondition);
            }

            if (exp.Orderings.Count > 0)
            {
                this._queryModel.Orderings.Clear();
                this._queryModel.InheritOrderings = false;

                for (int i = 0; i < exp.Orderings.Count; i++)
                {
                    GroupingQueryOrdering groupOrdering = exp.Orderings[i];

                    ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(groupOrdering.KeySelector.Parameters[0], this._queryModel.ResultModel);

                    DbExpression orderingDbExp = GeneralExpressionParser.Parse(this.QueryContext, groupOrdering.KeySelector, scopeParameters, this._queryModel.ScopeTables, this._queryModel);

                    DbOrdering ordering = new DbOrdering(orderingDbExp, groupOrdering.OrderType);
                    this._queryModel.Orderings.Add(ordering);
                }
            }

            QueryModel newQueryModel = this.CreateNewQueryModel(exp.Selector);
            return new GroupQueryState(this.QueryContext, newQueryModel);
        }
        public virtual IQueryState Accept(DistinctExpression exp)
        {
            DistinctQueryState state = new DistinctQueryState(this.QueryContext, this.QueryModel);
            return state;
        }
        public virtual IQueryState Accept(IncludeExpression exp)
        {
            throw new NotSupportedException("Cannot call 'Include' method now.");
        }
        public virtual IQueryState Accept(BindTwoWayExpression exp)
        {
            this.QueryModel.Options.BindTwoWay = true;
            return this;
        }
        public virtual IQueryState Accept(ExcludeExpression exp)
        {
            List<LinkedNode<MemberInfo>> fields = ExcludeFieldExtractor.Extract(exp.Field);
            this.QueryModel.ResultModel.ExcludePrimitiveMembers(fields);

            return this;
        }
        public virtual IQueryState Accept(IgnoreAllFiltersExpression exp)
        {
            throw new NotSupportedException("Cannot call 'IgnoreAllFilters' method now.");
        }
        public virtual IQueryState Accept(TrackingExpression exp)
        {
            this.QueryModel.Options.IsTracking = true;
            return this;
        }
        public virtual IQueryState Accept(PagingExpression exp)
        {
            throw new NotSupportedException();
        }

        public virtual QueryModel CreateNewQueryModel(LambdaExpression selector)
        {
            QueryModel newQueryModel = this._queryModel.Clone();

            ComplexObjectModel complexObjectModel = this._queryModel.ResultModel as ComplexObjectModel;
            if (complexObjectModel != null)
                complexObjectModel.SetupFilters(this._queryModel.Options.IgnoreFilters);

            ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(selector.Parameters[0], this._queryModel.ResultModel);
            IObjectModel newResultModel = SelectorResolver.Resolve(this.QueryContext, selector, this.QueryModel.Options, scopeParameters, this._queryModel.ScopeTables, newQueryModel);
            newQueryModel.ResultModel = newResultModel;

            return newQueryModel;
        }
        public virtual IQueryState CreateQueryState(QueryModel result)
        {
            return new GeneralQueryState(this.QueryContext, result);
        }

        public virtual MappingData GenerateMappingData()
        {
            MappingData data = new MappingData();

            ComplexObjectModel complexObjectModel = this._queryModel.ResultModel as ComplexObjectModel;
            if (complexObjectModel != null)
            {
                complexObjectModel.SetupCollection(this._queryModel);
                complexObjectModel.SetupFilters(this._queryModel.Options.IgnoreFilters);
            }

            DbSqlQueryExpression sqlQuery = this.CreateSqlQuery();

            var objectActivatorCreator = this._queryModel.ResultModel.GenarateObjectActivatorCreator(sqlQuery.ColumnSegments, new HashSet<string>());
            objectActivatorCreator.IsRoot = true;

            data.Context = this.QueryContext;
            data.SqlQuery = sqlQuery;
            data.ObjectActivatorCreator = objectActivatorCreator;
            data.IsTrackingQuery = this._queryModel.Options.IsTracking;
            data.CanBeCachced = this._queryModel.TouchedTables.Count == 0;

            return data;
        }

        public virtual GeneralQueryState AsSubqueryState()
        {
            DbSqlQueryExpression sqlQuery = this.CreateSqlQuery();
            DbSubqueryExpression subquery = new DbSubqueryExpression(sqlQuery);

            QueryModel newQueryModel = new QueryModel(this._queryModel.Options, this._queryModel.ScopeParameters, this._queryModel.ScopeTables, this._queryModel.TouchedTables);

            DbTableSegment tableSeg = new DbTableSegment(subquery, newQueryModel.GenerateUniqueTableAlias(), LockType.Unspecified);
            DbFromTableExpression fromTable = new DbFromTableExpression(tableSeg);

            newQueryModel.FromTable = fromTable;

            DbTable aliasTable = new DbTable(tableSeg.Alias);

            //用于存储列别名
            HashSet<string> columnAliasSet = new HashSet<string>();

            //根据旧的生成新 ResultModel
            IObjectModel newResultModel = this.QueryModel.ResultModel.ToNewObjectModel(sqlQuery.ColumnSegments, columnAliasSet, aliasTable, fromTable);
            newQueryModel.ResultModel = newResultModel;

            if (!sqlQuery.IsDistinct || this.QueryModel.Orderings.All(a => sqlQuery.ColumnSegments.Find(columnSegment => DbExpressionEqualityComparer.Instance.Equals(a.Expression, columnSegment.Body)) != null))
            {
                //注：如果是 distinct 查询，排序的字段不在 ColumnSegments 之中，则不能往 ColumnSegments 中添加字段，否则查出的数据会不准

                //得将 subquery.SqlQuery.Orders 告诉 以下创建的 result
                //将 orderPart 传递下去
                for (int i = 0; i < this.QueryModel.Orderings.Count; i++)
                {
                    DbOrdering ordering = this.QueryModel.Orderings[i];
                    DbExpression orderingExp = ordering.Expression;

                    string alias = null;

                    DbColumnSegment columnExpression = sqlQuery.ColumnSegments.Find(a => DbExpressionEqualityComparer.Instance.Equals(orderingExp, a.Body));

                    // 对于重复的则不需要往 sqlQuery.Columns 重复添加了
                    if (columnExpression != null)
                    {
                        alias = columnExpression.Alias;
                    }
                    else
                    {
                        alias = Utils.GenerateUniqueColumnAlias(columnAliasSet);
                        DbColumnSegment columnSeg = new DbColumnSegment(orderingExp, alias);
                        sqlQuery.ColumnSegments.Add(columnSeg);
                    }

                    DbColumnAccessExpression columnAccessExpression = new DbColumnAccessExpression(aliasTable, DbColumn.MakeColumn(orderingExp, alias));
                    newQueryModel.Orderings.Add(new DbOrdering(columnAccessExpression, ordering.OrderType));
                }
            }

            newQueryModel.InheritOrderings = true;

            GeneralQueryState queryState = new GeneralQueryState(this.QueryContext, newQueryModel);
            return queryState;
        }
        public virtual DbSqlQueryExpression CreateSqlQuery()
        {
            DbSqlQueryExpression sqlQuery = this._queryModel.CreateSqlQuery();
            return sqlQuery;
        }

        protected DbOrdering ParseOrderExpression(OrderExpression orderExp)
        {
            ScopeParameterDictionary scopeParameters = this._queryModel.ScopeParameters.Clone(orderExp.KeySelector.Parameters[0], this._queryModel.ResultModel);

            DbExpression dbExpression = GeneralExpressionParser.Parse(this.QueryContext, orderExp.KeySelector, scopeParameters, this._queryModel.ScopeTables, this.QueryModel);
            DbOrderType orderType;
            if (orderExp.NodeType == QueryExpressionType.OrderBy || orderExp.NodeType == QueryExpressionType.ThenBy)
            {
                orderType = DbOrderType.Asc;
            }
            else if (orderExp.NodeType == QueryExpressionType.OrderByDesc || orderExp.NodeType == QueryExpressionType.ThenByDesc)
            {
                orderType = DbOrderType.Desc;
            }
            else
                throw new NotSupportedException(orderExp.NodeType.ToString());

            DbOrdering ordering = new DbOrdering(dbExpression, orderType);

            return ordering;
        }

        public virtual QueryModel ToFromQueryModel()
        {
            QueryModel newQueryModel = new QueryModel(this._queryModel.Options, this._queryModel.ScopeParameters, this._queryModel.ScopeTables, this._queryModel.TouchedTables);

            string alias = newQueryModel.GenerateUniqueTableAlias();
            DbSqlQueryExpression sqlQuery = this.CreateSqlQuery();
            DbSubqueryExpression subquery = new DbSubqueryExpression(sqlQuery);

            DbTableSegment tableSeg = new DbTableSegment(subquery, alias, LockType.Unspecified);
            DbFromTableExpression fromTable = new DbFromTableExpression(tableSeg);

            DbTable aliasTable = new DbTable(tableSeg.Alias);
            IObjectModel newModel = this.QueryModel.ResultModel.ToNewObjectModel(sqlQuery.ColumnSegments, new HashSet<string>(), aliasTable, fromTable);

            newQueryModel.FromTable = fromTable;
            newQueryModel.ResultModel = newModel;
            return newQueryModel;
        }

        public virtual JoinQueryResult ToJoinQueryResult(JoinType joinType, LambdaExpression conditionExpression, ScopeParameterDictionary scopeParameters, StringSet scopeTables, Func<string, string> tableAliasGenerator)
        {
            DbSqlQueryExpression sqlQuery = this.CreateSqlQuery();
            DbSubqueryExpression subquery = new DbSubqueryExpression(sqlQuery);

            string alias = tableAliasGenerator(UtilConstants.DefaultTableAlias);
            DbTableSegment tableSeg = new DbTableSegment(subquery, alias, LockType.Unspecified);
            DbJoinTableExpression joinTable = new DbJoinTableExpression(joinType.AsDbJoinType(), tableSeg);

            DbTable aliasTable = new DbTable(tableSeg.Alias);
            IObjectModel newModel = this.QueryModel.ResultModel.ToNewObjectModel(sqlQuery.ColumnSegments, new HashSet<string>(), aliasTable, joinTable);

            scopeParameters[conditionExpression.Parameters[conditionExpression.Parameters.Count - 1]] = newModel;

            DbExpression condition = GeneralExpressionParser.Parse(this.QueryContext, conditionExpression, scopeParameters, scopeTables, this._queryModel);
            joinTable.Condition = condition;

            JoinQueryResult result = new JoinQueryResult();
            result.ResultModel = newModel;
            result.JoinTable = joinTable;
            return result;
        }

        public IQueryState Accept(JoinQueryExpression exp)
        {
            throw new NotImplementedException();
        }
    }
}
