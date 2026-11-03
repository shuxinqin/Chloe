using Chloe.Core;
using Chloe.DbExpressions;
using Chloe.Infrastructure;
using Chloe.RDBMS;

namespace Chloe.SonnetDB
{
    class DbExpressionTranslator : IDbExpressionTranslator
    {
        SonnetDBContextProvider ContextProvider { get; set; }

        public DbExpressionTranslator(SonnetDBContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }

        public DbCommandInfo Translate(DbExpression expression, List<object> variables)
        {
            SqlGeneratorOptions options = this.CreateOptions();
            SqlGenerator generator = new SqlGenerator(options);

            expression = EvaluableDbExpressionTransformer.Transform(expression, variables);
            expression = DbExpressionNormalizer.Normalize(expression);
            expression.Accept(generator);

            DbCommandInfo result = new DbCommandInfo();
            result.Parameters = generator.Parameters;
            result.CommandText = generator.SqlBuilder.ToSql();

            return result;
        }

        SqlGeneratorOptions CreateOptions()
        {
            var options = new SqlGeneratorOptions()
            {
                LeftQuoteChar = UtilConstants.LeftQuoteChar,
                RightQuoteChar = UtilConstants.RightQuoteChar,
                MaxInItems = this.ContextProvider.Options.MaxInItems,
                TreatEmptyStringAsNull = this.ContextProvider.Options.TreatEmptyStringAsNull,
            };

            return options;
        }
    }
}
