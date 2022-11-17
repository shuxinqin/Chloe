using Chloe.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Chloe.Entity
{
    /// <summary>
    /// 名称映射配置
    /// </summary>
    public class EntityNameConfig
    {
        public EntityNameConfig()
        {

        }
        public EntityNameConfig(bool mapCamelCaseToUnderscore, bool mapTableName = true, string tablePrefix = "", string columnPrefix = "")
        {
            MapCamelCaseToUnderscore = mapCamelCaseToUnderscore;
            MapTableName = mapTableName;
            TablePrefix = tablePrefix;
            ColumnPrefix = columnPrefix;
        }
        /// <summary>
        /// 是否开启映射
        /// </summary>
        public bool MapCamelCaseToUnderscore { get; set; }
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

        public static EntityNameConfig Of(Type type)
        {
            var attribute = type.GetCustomAttribute(typeof(CamelCaseToUnderscoreAttribute)) as CamelCaseToUnderscoreAttribute;
            if (attribute == null)
            {
                return new EntityNameConfig();
            }

            return new EntityNameConfig
            {
                MapCamelCaseToUnderscore = true,
                ColumnPrefix = attribute.ColumnPrefix,
                MapTableName = attribute.MapTableName,
                TablePrefix = attribute.TablePrefix,
            };
        }
    }
}
