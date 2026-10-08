using Chloe.DbExpressions;
using Chloe.RDBMS;

namespace Chloe.SonnetDB
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        static Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>> InitAggregateHandlers()
        {
            var aggregateHandlers = new Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>>();
            aggregateHandlers.Add("Count", Aggregate_Count);
            aggregateHandlers.Add("LongCount", Aggregate_LongCount);
            aggregateHandlers.Add("Sum", Aggregate_Sum);
            aggregateHandlers.Add("Max", Aggregate_Max);
            aggregateHandlers.Add("Min", Aggregate_Min);
            aggregateHandlers.Add("Average", Aggregate_Average);

            var ret = PublicHelper.Clone(aggregateHandlers);
            return ret;
        }

        static void Aggregate_Count(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_Count(generator);
        }
        static void Aggregate_LongCount(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_LongCount(generator);
        }
        static void Aggregate_Sum(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_Sum(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
        static void Aggregate_Max(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_Max(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
        static void Aggregate_Min(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_Min(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
        static void Aggregate_Average(DbAggregateExpression exp, SqlGeneratorBase generator)
        {
            Aggregate_Average(generator, exp.Arguments.First(), exp.Method.ReturnType);
        }
    }
}
