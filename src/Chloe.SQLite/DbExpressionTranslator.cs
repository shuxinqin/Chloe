﻿using Chloe.Core;
using Chloe.DbExpressions;
using Chloe.Infrastructure;
using Chloe.RDBMS;

namespace Chloe.SQLite
{
    class DbExpressionTranslator : IDbExpressionTranslator
    {
        SQLiteContextProvider ContextProvider { get; set; }

        public DbExpressionTranslator(SQLiteContextProvider contextProvider)
        {
            this.ContextProvider = contextProvider;
        }

        public DbCommandInfo Translate(DbExpression expression)
        {
            SqlGeneratorOptions options = this.CreateOptions();
            SqlGenerator generator = new SqlGenerator(options);
            expression = EvaluableDbExpressionTransformer.Transform(expression);
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
                MaxInItems = this.ContextProvider.Options.MaxInItems
            };

            return options;
        }
    }
}
