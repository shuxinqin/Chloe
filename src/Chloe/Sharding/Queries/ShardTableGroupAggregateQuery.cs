﻿using Chloe.Reflection;
using Chloe.Sharding.Models;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Chloe.Sharding.Queries
{
    internal class ShardTableGroupAggregateQuery : FeatureEnumerable<object>
    {
        IParallelQueryContext QueryContext;
        ISharedDbContextProviderPool DbContextProviderPool;
        GroupAggregateQueryModel QueryModel;
        bool LazyQuery;

        public ShardTableGroupAggregateQuery(IParallelQueryContext queryContext, ISharedDbContextProviderPool dbContextProviderPool, GroupAggregateQueryModel queryModel, bool lazyQuery)
        {
            this.QueryContext = queryContext;
            this.DbContextProviderPool = dbContextProviderPool;
            this.QueryModel = queryModel;
            this.LazyQuery = lazyQuery;
        }

        public override IFeatureEnumerator<object> GetFeatureEnumerator(CancellationToken cancellationToken = default)
        {
            return new Enumerator(this, cancellationToken);
        }

        class Enumerator : QueryEnumerator<object>
        {
            ShardTableGroupAggregateQuery _enumerable;
            CancellationToken _cancellationToken;

            public Enumerator(ShardTableGroupAggregateQuery enumerable, CancellationToken cancellationToken = default) : base(enumerable.QueryContext, enumerable.DbContextProviderPool)
            {
                this._enumerable = enumerable;
                this._cancellationToken = cancellationToken;
            }

            protected override async Task<(IFeatureEnumerable<object> Query, bool IsLazyQuery)> CreateQuery(IDbContextProvider dbContextProvider, bool @async)
            {
                var q = this.MakeGroupAggregateQuery(dbContextProvider);

                if (!this._enumerable.LazyQuery)
                {
                    IEnumerable dataList = null;
                    if (@async)
                    {
                        dataList = await q.ToListAsync();
                    }
                    else
                    {
                        dataList = q.ToList();
                    }

                    return (new FeatureEnumerableAdapter<object>(dataList), false);
                }

                var lazyEnumerable = q.FastInvokeMethod(nameof(IQuery<object>.AsEnumerable)) as IEnumerable;
                return (new FeatureEnumerableAdapter<object>(lazyEnumerable), true);
            }

            IQuery MakeGroupAggregateQuery(IDbContextProvider dbContextProvider)
            {
                GroupAggregateQueryModel queryModel = this._enumerable.QueryModel;
                var method = this.GetType().GetMethod(nameof(Enumerator.MakeTypedGroupAggregateQuery), BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(queryModel.RootEntityType);
                var query = (IQuery)method.FastInvoke(null, dbContextProvider, queryModel);
                return query;
            }

            static IQuery MakeTypedGroupAggregateQuery<T>(IDbContextProvider dbContextProvider, GroupAggregateQueryModel queryModel)
            {
                var query = dbContextProvider.Query<T>(queryModel.Table.Name, queryModel.Lock);

                foreach (var condition in queryModel.Conditions)
                {
                    query = query.Where((Expression<Func<T, bool>>)condition);
                }

                object groupQuery = null;
                foreach (var groupKeySelector in queryModel.GroupKeySelectors)
                {
                    if (groupQuery == null)
                    {
                        var groupMethod = typeof(IQuery<T>).GetMethod(nameof(IQuery<object>.GroupBy)).MakeGenericMethod(groupKeySelector.Body.Type);
                        groupQuery = groupMethod.FastInvoke(query, groupKeySelector);
                        continue;
                    }

                    var andByMethod = groupQuery.GetType().GetMethod(nameof(IGroupingQuery<object>.AndBy)).MakeGenericMethod(groupKeySelector.Body.Type);
                    groupQuery = andByMethod.FastInvoke(groupQuery, groupKeySelector);
                }

                var selectMethod = groupQuery.GetType().GetMethod(nameof(IGroupingQuery<object>.Select)).MakeGenericMethod(queryModel.Selector.Body.Type);

                var q = (IQuery)selectMethod.FastInvoke(groupQuery, queryModel.Selector);
                return q;
            }
        }
    }
}
