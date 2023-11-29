﻿using Chloe.RDBMS;
using System.Reflection;

namespace Chloe.SQLite
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        static Dictionary<string, IPropertyHandler[]> InitPropertyHandlers()
        {
            return PublicHelper.FindPropertyHandlers(Assembly.GetExecutingAssembly());
        }
    }
}
