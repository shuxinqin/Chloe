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
        DatabaseProvider _databaseProvider;

        public PostgreSQLContext(IDbConnectionFactory dbConnectionFactory)
        {
            PublicHelper.CheckNull(dbConnectionFactory);
            this._databaseProvider = new DatabaseProvider(dbConnectionFactory, this);
        }

        /// <summary>
        /// 是否将 sql 中的表名/字段名转成小写。默认为 true。
        /// </summary>
        public bool ConvertToLowercase { get; set; } = true;
        public override IDatabaseProvider DatabaseProvider
        {
            get { return this._databaseProvider; }
        }

        public override TEntity Insert<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            Dictionary<PropertyDescriptor, object> keyValueMap = PrimaryKeyHelper.CreateKeyValueMap(typeDescriptor);

            Dictionary<PropertyDescriptor, DbExpression> insertColumns = new Dictionary<PropertyDescriptor, DbExpression>();
            foreach (PropertyDescriptor propertyDescriptor in typeDescriptor.PropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                    continue;

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor);
                    insertColumns.Add(propertyDescriptor, getNextValueForSequenceExp);
                    continue;
                }

                object val = propertyDescriptor.GetValue(entity);

                if (propertyDescriptor.IsPrimaryKey)
                {
                    keyValueMap[propertyDescriptor] = val;
                }

                DbParameterExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                insertColumns.Add(propertyDescriptor, valExp);
            }

            PropertyDescriptor nullValueKey = keyValueMap.Where(a => a.Value == null && !a.Key.IsAutoIncrement).Select(a => a.Key).FirstOrDefault();
            if (nullValueKey != null)
            {
                /* 主键为空并且主键又不是自增列 */
                throw new ChloeException(string.Format("The primary key '{0}' could not be null.", nullValueKey.Property.Name));
            }

            DbTable dbTable = table == null ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            DbInsertExpression insertExp = new DbInsertExpression(dbTable);

            foreach (var kv in insertColumns)
            {
                insertExp.InsertColumns.Add(kv.Key.Column, kv.Value);
            }

            List<Action<TEntity, IDataReader>> mappers = new List<Action<TEntity, IDataReader>>();
            foreach (var item in typeDescriptor.PropertyDescriptors.Where(a => a.IsAutoIncrement || a.HasSequence()))
            {
                mappers.Add(GetMapper<TEntity>(item, insertExp.Returns.Count));
                insertExp.Returns.Add(item.Column);
            }

            if (mappers.Count == 0)
            {
                this.ExecuteNonQuery(insertExp);
                return entity;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(insertExp, out parameters);

            IDataReader dataReader = this.Session.ExecuteReader(sql, parameters.ToArray());
            using (dataReader)
            {
                dataReader.Read();
                foreach (var mapper in mappers)
                {
                    mapper(entity, dataReader);
                }
            }

            return entity;
        }
        public override object Insert<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            if (typeDescriptor.PrimaryKeys.Count > 1)
            {
                /* 对于多主键的实体，暂时不支持调用这个方法进行插入 */
                throw new NotSupportedException(string.Format("Can not call this method because entity '{0}' has multiple keys.", typeDescriptor.Definition.Type.FullName));
            }

            PropertyDescriptor keyPropertyDescriptor = typeDescriptor.PrimaryKeys.FirstOrDefault();

            Dictionary<MemberInfo, Expression> insertColumns = InitMemberExtractor.Extract(content);

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);
            DbInsertExpression insertExp = new DbInsertExpression(explicitDbTable ?? typeDescriptor.Table);

            object keyVal = null;

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PropertyDescriptor propertyDescriptor = typeDescriptor.TryGetPropertyDescriptor(key);

                if (propertyDescriptor == null)
                    throw new ChloeException(string.Format("The member '{0}' does not map any column.", key.Name));

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the identity column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Can not insert value into the column '{0}', because it's mapping member has define a sequence.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    if (val == null)
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", propertyDescriptor.Property.Name));
                    else
                    {
                        keyVal = val;
                        insertExp.InsertColumns.Add(propertyDescriptor.Column, DbExpression.Parameter(keyVal));
                        continue;
                    }
                }

                insertExp.InsertColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            foreach (var item in typeDescriptor.PropertyDescriptors.Where(a => a.HasSequence()))
            {
                DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(item);
                insertExp.InsertColumns.Add(item.Column, getNextValueForSequenceExp);
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
                {
                    throw new ChloeException(string.Format("The primary key '{0}' could not be null.", keyPropertyDescriptor.Property.Name));
                }
            }

            if (keyPropertyDescriptor == null)
            {
                this.ExecuteNonQuery(insertExp);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }
            if (!keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
            {
                this.ExecuteNonQuery(insertExp);
                return keyVal;
            }

            insertExp.Returns.Add(keyPropertyDescriptor.Column);

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(insertExp, out parameters);

            object ret = this.Session.ExecuteScalar(sql, parameters.ToArray());
            if (ret == null || ret == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity/sequence value.");
            }

            ret = PublicHelper.ConvertObjType(ret, typeDescriptor.AutoIncrement.PropertyType);
            return ret;
        }

        [Obsolete("This method will be removed in future versions.")]
        public override void InsertRange<TEntity>(List<TEntity> entities, bool keepIdentity = false, string table = null)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) values(...),(...)... 
             * 该方法相对循环一条一条插入，速度提升 2/3 这样
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int maxParameters = 2100;
            int batchSize = 50; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            var e = typeDescriptor.PropertyDescriptors as IEnumerable<PropertyDescriptor>;
            if (keepIdentity == false)
                e = e.Where(a => a.IsAutoIncrement == false);
            List<PropertyDescriptor> mappingPropertyDescriptors = e.ToList();
            int maxDbParamsCount = maxParameters - mappingPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = string.IsNullOrEmpty(table) ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            string sqlTemplate = this.AppendInsertRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Action insertAction = () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(",");

                    sqlBuilder.Append("(");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];
                        object val = mappingPropertyDescriptor.GetValue(entity);
                        if (val == null)
                        {
                            sqlBuilder.Append("NULL");
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (Utils.IsToStringableNumericType(valType))
                        {
                            sqlBuilder.Append(val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("true");
                            else
                                sqlBuilder.AppendFormat("false");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);
                        sqlBuilder.Append(paramName);
                    }
                    sqlBuilder.Append(")");

                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 120/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
                        string sql = sqlBuilder.ToString();
                        this.Session.ExecuteNonQuery(sql, dbParams.ToArray());

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Action fAction = () =>
            {
                insertAction();
            };

            if (this.Session.IsInTransaction)
            {
                fAction();
            }
            else
            {
                /* 因为分批插入，所以需要开启事务保证数据一致性 */
                this.Session.BeginTransaction();
                try
                {
                    fAction();
                    this.Session.CommitTransaction();
                }
                catch
                {
                    if (this.Session.IsInTransaction)
                        this.Session.RollbackTransaction();
                    throw;
                }
            }
        }
        public override void InsertRange<TEntity>(List<TEntity> entities, string table)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) values(...),(...)... 
             * 该方法相对循环一条一条插入，速度提升 2/3 这样
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int maxParameters = 2100;
            int batchSize = 50; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<PropertyDescriptor> mappingPropertyDescriptors = typeDescriptor.PropertyDescriptors.Where(a => a.IsAutoIncrement == false).ToList();
            int maxDbParamsCount = maxParameters - mappingPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = string.IsNullOrEmpty(table) ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            string sqlTemplate = this.AppendInsertRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Action insertAction = () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(",");

                    sqlBuilder.Append("(");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];

                        if (mappingPropertyDescriptor.HasSequence())
                        {
                            sqlBuilder.Append("nextval(");
                            sqlBuilder.Append(mappingPropertyDescriptor.Definition.SequenceName);
                            sqlBuilder.Append(")");
                            continue;
                        }

                        object val = mappingPropertyDescriptor.GetValue(entity);
                        if (val == null)
                        {
                            sqlBuilder.Append("NULL");
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (Utils.IsToStringableNumericType(valType))
                        {
                            sqlBuilder.Append(val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("true");
                            else
                                sqlBuilder.AppendFormat("false");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);
                        sqlBuilder.Append(paramName);
                    }
                    sqlBuilder.Append(")");

                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 120/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
                        string sql = sqlBuilder.ToString();
                        this.Session.ExecuteNonQuery(sql, dbParams.ToArray());

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Action fAction = () =>
            {
                insertAction();
            };

            if (this.Session.IsInTransaction)
            {
                fAction();
            }
            else
            {
                /* 因为分批插入，所以需要开启事务保证数据一致性 */
                this.Session.BeginTransaction();
                try
                {
                    fAction();
                    this.Session.CommitTransaction();
                }
                catch
                {
                    if (this.Session.IsInTransaction)
                        this.Session.RollbackTransaction();
                    throw;
                }
            }
        }

        /// <summary>
        /// 批量更新数据
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="table"></param>
        public override int UpdateRange<TEntity>(List<TEntity> entities, string table = null)
        {
            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return 0;

            int maxParameters = 2100;
            int batchSize = 50; /* 每批实体大小，此值通过测试得出相对插入速度比较快的一个值 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            var e = typeDescriptor.PropertyDescriptors as IEnumerable<PropertyDescriptor>;
            
            List<PropertyDescriptor> mappingPropertyDescriptors = e.ToList();
            int maxDbParamsCount = maxParameters - mappingPropertyDescriptors.Count; /* 控制一个 sql 的参数个数 */

            DbTable dbTable = string.IsNullOrEmpty(table) ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            string sqlTemplate = this.AppendUpdateRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Action updateAction = () =>
            {
                int batchCount = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();

                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (batchCount > 0)
                        sqlBuilder.Append(";" + Environment.NewLine);

                    var str = sqlTemplate;
                    //一个实体对象构造SQL
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        PropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];
                        object val = mappingPropertyDescriptor.GetValue(entity);

                        if (mappingPropertyDescriptor.IsPrimaryKey)
                        {
                            string pkName = UtilConstants.ParameterNamePrefix + mappingPropertyDescriptor.Column.Name + i;
                            DbParam pkParam = new DbParam(pkName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                            dbParams.Add(pkParam);
                            str = str.Replace("{PK}", pkName);
                        }

                        if (val == null)
                        {
                            str = str.Replace("{" + j + "}", "= NULL");
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (Utils.IsToStringableNumericType(valType))
                        {
                            str = str.Replace("{" + j + "}", " = " + val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                str = str.Replace("{" + j + "}", "= true");
                            else
                                str = str.Replace("{" + j + "}", "= false");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);
                        str = str.Replace("{" + j + "}", "= " + paramName);
                    }
                    sqlBuilder.Append(str);
                    batchCount++;

                    if ((batchCount >= 20 && dbParams.Count >= 120/*参数个数太多也会影响速度*/) || dbParams.Count >= maxDbParamsCount || batchCount >= batchSize || (i + 1) == entities.Count)
                    {
                        //sqlBuilder.Insert(0, sqlTemplate);
                        string sql = sqlBuilder.ToString();
                        this.Session.ExecuteNonQuery(sql, dbParams.ToArray());

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        batchCount = 0;
                    }
                }
            };

            Action fAction = () =>
            {
                updateAction();
            };

            if (this.Session.IsInTransaction)
            {
                fAction();
            }
            else
            {
                /* 因为分批插入，所以需要开启事务保证数据一致性 */
                this.Session.BeginTransaction();
                try
                {
                    fAction();
                    this.Session.CommitTransaction();
                }
                catch
                {
                    if (this.Session.IsInTransaction)
                        this.Session.RollbackTransaction();
                    throw;
                }
            }
            return entities.Count;
        }

        protected override string GetSelectLastInsertIdClause()
        {
            throw new NotSupportedException();
        }
    }
}
