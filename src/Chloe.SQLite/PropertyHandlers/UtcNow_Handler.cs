﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SQLite.PropertyHandlers
{
    class UtcNow_Handler : UtcNow_HandlerBase
    {
        public override void Process(DbMemberExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("DATETIME()");
        }
    }
}
