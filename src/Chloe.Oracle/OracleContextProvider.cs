using Chloe.Core;
using Chloe.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.RDBMS;
using Chloe.Utility;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Chloe.Oracle
{
    public partial class OracleContextProvider : DbContextProvider
    {
        DatabaseProvider _databaseProvider;

        public OracleContextProvider(Func<IDbConnection> dbConnectionFactory, DbContext dbContext) : this(new DbConnectionFactory(dbConnectionFactory), dbContext)
        {

        }

        public OracleContextProvider(IDbConnectionFactory dbConnectionFactory, DbContext dbContext) : this(new OracleOptions() { DbConnectionFactory = dbConnectionFactory }, dbContext)
        {

        }

        public OracleContextProvider(OracleOptions options, DbContext dbContext) : base(options, dbContext)
        {
            this._databaseProvider = new DatabaseProvider(this);
        }

        public new OracleOptions Options { get { return base.Options as OracleOptions; } }
        public override IDatabaseProvider DatabaseProvider
        {
            get { return this._databaseProvider; }
        }


        /// <summary>
        /// 设置属性解析器。
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="handler"></param>
        public static void SetPropertyHandler(string propertyName, IPropertyHandler handler)
        {
            PublicHelper.CheckNull(propertyName, nameof(propertyName));
            PublicHelper.CheckNull(handler, nameof(handler));
            lock (SqlGenerator.PropertyHandlerDic)
            {
                List<IPropertyHandler> propertyHandlers = new List<IPropertyHandler>();
                propertyHandlers.Add(handler);
                if (SqlGenerator.PropertyHandlerDic.TryGetValue(propertyName, out var propertyHandlerArray))
                {
                    propertyHandlers.AddRange(propertyHandlerArray);
                }

                SqlGenerator.PropertyHandlerDic[propertyName] = propertyHandlers.ToArray();
            }
        }

        /// <summary>
        /// 设置方法解析器。
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="handler"></param>
        public static void SetMethodHandler(string methodName, IMethodHandler handler)
        {
            PublicHelper.CheckNull(methodName, nameof(methodName));
            PublicHelper.CheckNull(handler, nameof(handler));
            lock (SqlGenerator.MethodHandlerDic)
            {
                List<IMethodHandler> methodHandlers = new List<IMethodHandler>();
                methodHandlers.Add(handler);
                if (SqlGenerator.MethodHandlerDic.TryGetValue(methodName, out var methodHandlerArray))
                {
                    methodHandlers.AddRange(methodHandlerArray);
                }

                SqlGenerator.MethodHandlerDic[methodName] = methodHandlers.ToArray();
            }
        }

        protected override async Task<TEntity> Insert<TEntity>(TEntity entity, string table, bool @async)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            InsertStrategy insertStrategy = this.Options.InsertStrategy;
            PrimitivePropertyDescriptor firstIgnoredProperty = null;
            object firstIgnoredPropertyValue = null;

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbInsertExpression insertExpression = new DbInsertExpression(dbTable);

            List<PrimitivePropertyDescriptor> outputColumns = new List<PrimitivePropertyDescriptor>();
            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                {
                    outputColumns.Add(propertyDescriptor);
                    continue;
                }

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor, dbTable.Schema);
                    insertExpression.AppendInsertColumn(propertyDescriptor.Column, getNextValueForSequenceExp);
                    outputColumns.Add(propertyDescriptor);
                    continue;
                }

                object val = propertyDescriptor.GetValue(entity);

                PublicHelper.NotNullCheck(propertyDescriptor, val);
                PublicHelper.EnsurePrimaryKeyNotNull(propertyDescriptor, val);

                if (PublicHelper.CanIgnoreInsert(insertStrategy, val))
                {
                    if (firstIgnoredProperty == null)
                    {
                        firstIgnoredProperty = propertyDescriptor;
                        firstIgnoredPropertyValue = val;
                    }

                    continue;
                }

                DbParameterExpression valExp = new DbParameterExpression(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                insertExpression.AppendInsertColumn(propertyDescriptor.Column, valExp);
            }

            if (insertExpression.InsertColumns.Count == 0 && firstIgnoredProperty != null)
            {
                DbExpression valExp = new DbParameterExpression(firstIgnoredPropertyValue, firstIgnoredProperty.PropertyType, firstIgnoredProperty.Column.DbType);
                insertExpression.AppendInsertColumn(firstIgnoredProperty.Column, valExp);
            }

            insertExpression.Returns.AddRange(outputColumns.Select(a => a.Column));

            insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, entity, exp);
            }, insertExpression);

            DbCommandInfo dbCommandInfo = this.Translate(insertExpression);
            await this.ExecuteNonQuery(dbCommandInfo, @async);

            List<DbParam> outputParams = dbCommandInfo.Parameters.Where(a => a.Direction == ParamDirection.Output).ToList();

            for (int i = 0; i < outputColumns.Count; i++)
            {
                PrimitivePropertyDescriptor propertyDescriptor = outputColumns[i];
                string putputColumnName = Utils.GenOutputColumnParameterName(propertyDescriptor.Column.Name);
                DbParam outputParam = outputParams.Where(a => a.Name == putputColumnName).First();
                var outputValue = PublicHelper.ConvertObjectType(outputParam.Value, propertyDescriptor.PropertyType);
                outputColumns[i].SetValue(entity, outputValue);
            }

            return entity;
        }
        protected override async Task<object> Insert<TEntity>(Expression<Func<TEntity>> content, string table, bool @async)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            if (typeDescriptor.PrimaryKeys.Count > 1)
            {
                /* 对于多主键的实体，暂时不支持调用这个方法进行插入 */
                throw new NotSupportedException(string.Format("Can not call this method because entity '{0}' has multiple keys.", typeDescriptor.Definition.Type.FullName));
            }

            PrimitivePropertyDescriptor keyPropertyDescriptor = typeDescriptor.PrimaryKeys.FirstOrDefault();

            List<KeyValuePair<MemberInfo, Expression>> insertColumns = InitMemberExtractor.Extract(content);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);

            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(dbTable);
            DbInsertExpression insertExpression = new DbInsertExpression(dbTable);

            object keyVal = null;

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the auto increment column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Can not insert value into the column '{0}', because it's mapping member has define a sequence.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    PublicHelper.EnsurePrimaryKeyNotNull(propertyDescriptor, val);

                    keyVal = val;
                    insertExpression.AppendInsertColumn(propertyDescriptor.Column, new DbParameterExpression(keyVal, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType));

                    continue;
                }

                insertExpression.AppendInsertColumn(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement && propertyDescriptor.IsPrimaryKey)
                {
                    insertExpression.Returns.Add(propertyDescriptor.Column);
                    continue;
                }

                if (propertyDescriptor.HasSequence())
                {
                    DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(propertyDescriptor, dbTable.Schema);
                    insertExpression.AppendInsertColumn(propertyDescriptor.Column, getNextValueForSequenceExp);

                    if (propertyDescriptor.IsPrimaryKey)
                    {
                        insertExpression.Returns.Add(propertyDescriptor.Column);
                    }

                    continue;
                }
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
                {
                    PublicHelper.EnsurePrimaryKeyNotNull(keyPropertyDescriptor, keyVal);
                }
            }


            insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
            }, insertExpression);

            DbCommandInfo dbCommandInfo = this.Translate(insertExpression);
            await this.ExecuteNonQuery(dbCommandInfo, @async);

            if (keyPropertyDescriptor != null && (keyPropertyDescriptor.IsAutoIncrement || keyPropertyDescriptor.HasSequence()))
            {
                string outputColumnName = Utils.GenOutputColumnParameterName(keyPropertyDescriptor.Column.Name);
                DbParam outputParam = dbCommandInfo.Parameters.Where(a => a.Direction == ParamDirection.Output && a.Name == outputColumnName).First();
                keyVal = PublicHelper.ConvertObjectType(outputParam.Value, keyPropertyDescriptor.PropertyType);
            }

            return keyVal; /* It will return null if an entity does not define primary key. */
        }
        protected override async Task InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table, bool @async)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) select ... from dual union all select ... from dual...
             * 对于 oracle，貌似速度提升不了...- -
             * #期待各码友的优化建议#
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int countPerBatch = batchSize ?? this.Options.DefaultBatchSizeForInsertRange; /* 每批实体个数 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<PrimitivePropertyDescriptor> mappingPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.IsAutoIncrement == false).ToList();

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            string sqlTemplate = AppendInsertRangeSqlTemplate(dbTable, mappingPropertyDescriptors);

            Func<Task> insertAction = async () =>
            {
                for (int i = 0; i < this.Context.Butler.DbContextInterceptors.Count; i++)
                {
                    this.Context.Butler.DbContextInterceptors[i].InsertRangeExecuting(this.Context, entities);
                }

                int countOfCurrentBatch = 0;
                List<DbParam> dbParams = new List<DbParam>();
                StringBuilder sqlBuilder = new StringBuilder();
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    if (countOfCurrentBatch > 0)
                        sqlBuilder.Append(" UNION ALL ");

                    sqlBuilder.Append("SELECT ");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];

                        object val = mappingPropertyDescriptor.GetValue(entity);

                        PublicHelper.NotNullCheck(mappingPropertyDescriptor, val);

                        if (val == null)
                        {
                            sqlBuilder.Append("NULL");
                            sqlBuilder.Append(" C").Append(j.ToString());
                            continue;
                        }

                        Type valType = val.GetType();
                        if (valType.IsEnum)
                        {
                            val = Convert.ChangeType(val, Enum.GetUnderlyingType(valType));
                            valType = val.GetType();
                        }

                        if (PublicHelper.IsToStringableNumericType(valType))
                        {
                            sqlBuilder.Append(val.ToString());
                        }
                        else if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("1");
                            else
                                sqlBuilder.AppendFormat("0");
                        }
                        else
                        {
                            string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                            DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                            dbParams.Add(dbParam);
                            sqlBuilder.Append(paramName);
                        }

                        sqlBuilder.Append(" C").Append(j.ToString());
                    }

                    sqlBuilder.Append(" FROM DUAL");

                    countOfCurrentBatch++;

                    if (countOfCurrentBatch >= countPerBatch || (dbParams.Count + mappingPropertyDescriptors.Count)/*为确保参数个数不超过最大限制*/ >= this.Options.MaxNumberOfParameters || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
                        sqlBuilder.Append(") T");

                        string sql = sqlBuilder.ToString();
                        await this.Session.ExecuteNonQuery(sql, dbParams.ToArray(), @async);

                        sqlBuilder.Clear();
                        dbParams.Clear();
                        countOfCurrentBatch = 0;
                    }
                }
            };

            Func<Task> fAction = insertAction;

            if (this.Session.IsInTransaction)
            {
                await fAction();
                return;
            }

            /* 因为分批插入，所以需要开启事务保证数据一致性 */
            this.Session.BeginTransaction();
            try
            {
                await fAction();
                this.Session.CommitTransaction();
            }
            catch
            {
                this.Session.RollbackTransaction();
                throw;
            }
        }

        protected override async Task<int> Update<TEntity>(TEntity entity, string table, bool @async)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbUpdateExpression updateExpression = new DbUpdateExpression(dbTable);

            PairList<PrimitivePropertyDescriptor, object> keyValues = new PairList<PrimitivePropertyDescriptor, object>(typeDescriptor.PrimaryKeys.Count);

            IEntityState entityState = this.TryGetTrackedEntityState(entity);

            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsPrimaryKey)
                {
                    var keyValue = propertyDescriptor.GetValue(entity);
                    PublicHelper.EnsurePrimaryKeyNotNull(propertyDescriptor, keyValue);
                    keyValues.Add(propertyDescriptor, keyValue);
                    continue;
                }

                if (propertyDescriptor.CannotUpdate())
                    continue;

                object val = propertyDescriptor.GetValue(entity);

                if (entityState != null && !entityState.HasChanged(propertyDescriptor, val))
                    continue;

                PublicHelper.NotNullCheck(propertyDescriptor, val);

                DbExpression valExp = new DbParameterExpression(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                updateExpression.AppendUpdateColumn(propertyDescriptor.Column, valExp);
            }

            object rowVersionNewValue = null;
            if (typeDescriptor.HasRowVersion())
            {
                var rowVersionDescriptor = typeDescriptor.RowVersion;
                var rowVersionOldValue = rowVersionDescriptor.GetValue(entity);
                rowVersionNewValue = PublicHelper.IncreaseRowVersionNumber(rowVersionOldValue);
                updateExpression.AppendUpdateColumn(rowVersionDescriptor.Column, new DbParameterExpression(rowVersionNewValue, rowVersionDescriptor.PropertyType, rowVersionDescriptor.Column.DbType));
                keyValues.Add(rowVersionDescriptor, rowVersionOldValue);
            }

            if (updateExpression.UpdateColumns.Count == 0)
                return 0;

            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable);
            updateExpression.Condition = conditionExp;

            int rowsAffected = await this.ExecuteNonQuery(updateExpression, @async);

            if (typeDescriptor.HasRowVersion())
            {
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
                typeDescriptor.RowVersion.SetValue(entity, rowVersionNewValue);
            }

            if (entityState != null)
                entityState.Refresh();

            return rowsAffected;
        }

        string AppendInsertRangeSqlTemplate(DbTable table, List<PrimitivePropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("INSERT INTO ");
            sqlBuilder.Append(this.AppendTableName(table));
            sqlBuilder.Append("(");

            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");
                sqlBuilder.Append(this.QuoteName(mappingPropertyDescriptor.Column.Name));
            }

            sqlBuilder.Append(")");

            sqlBuilder.Append(" SELECT ");
            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");

                if (mappingPropertyDescriptor.HasSequence())
                    sqlBuilder.AppendFormat("{0}.{1}", this.QuoteName(mappingPropertyDescriptor.Definition.SequenceName), this.QuoteName("NEXTVAL"));
                else
                {
                    sqlBuilder.Append("C").Append(i.ToString());
                }
            }
            sqlBuilder.Append(" FROM (");

            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }

        string AppendTableName(DbTable table)
        {
            if (string.IsNullOrEmpty(table.Schema))
                return this.QuoteName(table.Name);

            return string.Format("{0}.{1}", this.QuoteName(table.Schema), this.QuoteName(table.Name));
        }
        string QuoteName(string name)
        {
            if (this.Options.ConvertToUppercase)
                return string.Concat("\"", name.ToUpper(), "\"");

            return string.Concat("\"", name, "\"");
        }
    }
}
