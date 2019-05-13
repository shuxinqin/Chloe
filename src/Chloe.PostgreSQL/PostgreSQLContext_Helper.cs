using Chloe.Core;
using Chloe.Core.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Entity;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.InternalExtensions;
using Chloe.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Chloe.PostgreSQL
{
    public partial class PostgreSQLContext : DbContext
    {
        static Action<TEntity, IDataReader> GetMapper<TEntity>(PropertyDescriptor propertyDescriptor, int ordinal)
        {
            Action<TEntity, IDataReader> mapper = (TEntity entity, IDataReader reader) =>
            {
                object value = reader.GetValue(ordinal);
                if (value == null || value == DBNull.Value)
                    throw new ChloeException("Unable to get the identity/sequence value.");

                value = PublicHelper.ConvertObjType(value, propertyDescriptor.PropertyType);
                propertyDescriptor.SetValue(entity, value);
            };

            return mapper;
        }

        string AppendInsertRangeSqlTemplate(DbTable table, List<PropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("INSERT INTO ");
            sqlBuilder.Append(this.AppendTableName(table));
            sqlBuilder.Append("(");

            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");
                sqlBuilder.Append(Utils.QuoteName(mappingPropertyDescriptor.Column.Name, this.ConvertToLowercase));
            }

            sqlBuilder.Append(") VALUES");

            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }
        string AppendUpdateRangeSqlTemplate(DbTable table, List<PropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("UPDATE ");
            sqlBuilder.Append(this.AppendTableName(table));
            sqlBuilder.Append(" SET ");
            PropertyDescriptor primaryKeyDescriptor = null;
            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (mappingPropertyDescriptor.IsPrimaryKey)
                {
                    primaryKeyDescriptor = mappingPropertyDescriptor;
                    continue;
                }
                if (i > 0) sqlBuilder.Append(", ");
                sqlBuilder.Append(Utils.QuoteName(mappingPropertyDescriptor.Column.Name, this.ConvertToLowercase));
                sqlBuilder.Append(" {" + i + "} ");
            }
            sqlBuilder.Append(" WHERE " + Utils.QuoteName(primaryKeyDescriptor.Column.Name, this.ConvertToLowercase));
            sqlBuilder.Append(" = {PK} ");
            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }
        string AppendTableName(DbTable table)
        {
            if (string.IsNullOrEmpty(table.Schema))
                return Utils.QuoteName(table.Name, this.ConvertToLowercase);

            return string.Format("{0}.{1}", Utils.QuoteName(table.Schema, this.ConvertToLowercase), Utils.QuoteName(table.Name, this.ConvertToLowercase));
        }
    }
}
