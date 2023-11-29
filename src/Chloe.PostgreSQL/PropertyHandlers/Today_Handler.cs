﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.PostgreSQL.PropertyHandlers
{
    class Today_Handler : Today_HandlerBase
    {
        public override void Process(DbMemberExpression exp, SqlGeneratorBase generator)
        {
            SqlGeneratorBase.BuildCastState(generator, "NOW()", "DATE");
        }
    }
}
