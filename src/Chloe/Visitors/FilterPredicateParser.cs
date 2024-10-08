﻿using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Query;
using Chloe.Utility;
using System.Linq.Expressions;

namespace Chloe.Visitors
{
    public class FilterPredicateParser : ExpressionVisitor<DbExpression>
    {
        public static DbExpression Parse(QueryContext queryContext, LambdaExpression filterPredicate, ScopeParameterDictionary scopeParameters, StringSet scopeTables, QueryModel? queryModel)
        {
            return GeneralExpressionParser.Parse(queryContext, BooleanResultExpressionTransformer.Transform(filterPredicate), scopeParameters, scopeTables, queryModel);
        }

        public static DbExpression Parse(QueryContext queryContext, LambdaExpression filterPredicate, TypeDescriptor typeDescriptor, DbTable dbTable, QueryModel? queryModel)
        {
            return Parse(queryContext, filterPredicate, typeDescriptor, dbTable, new QueryOptions(), queryModel);
        }

        public static DbExpression Parse(QueryContext queryContext, LambdaExpression filterPredicate, TypeDescriptor typeDescriptor, DbTable dbTable, QueryOptions queryOptions, QueryModel? queryModel)
        {
            ComplexObjectModel objectModel = typeDescriptor.GenObjectModel(dbTable, queryContext, queryOptions);
            ScopeParameterDictionary scopeParameters = new ScopeParameterDictionary(1);
            scopeParameters.Add(filterPredicate.Parameters[0], objectModel);

            StringSet scopeTables = new StringSet();
            scopeTables.Add(dbTable.Name);

            DbExpression conditionExp = FilterPredicateParser.Parse(queryContext, filterPredicate, scopeParameters, scopeTables, queryModel);
            return conditionExp;
        }
    }
}
