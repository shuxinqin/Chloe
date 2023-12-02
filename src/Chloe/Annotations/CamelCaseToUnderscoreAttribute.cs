using System;
using System.Collections.Generic;
using System.Text;

namespace Chloe.Annotations
{
    /// <summary>
    /// 驼峰命名与下划线命名映射
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CamelCaseToUnderscoreAttribute : Attribute
    {
        public CamelCaseToUnderscoreAttribute(bool mapTableName = true, string tablePrefix = "", string columnPrefix = "")
        {
            MapTableName = mapTableName;
            TablePrefix = tablePrefix;
            ColumnPrefix = columnPrefix;
        }
        /// <summary>
        /// 是否映射表名
        /// </summary>
        public bool MapTableName { get; set; }
        /// <summary>
        /// 表名前缀
        /// </summary>
        public string TablePrefix { get; set; }
        /// <summary>
        /// 列名前缀
        /// </summary>
        public string ColumnPrefix { get; set; }
    }
}
