using Chloe.Core;
using Chloe.Visitors;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.Reflection;
using Chloe.Threading.Tasks;
using Chloe.Utility;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Chloe.RDBMS;

#if NET6 || NET8 || NET10
using Microsoft.Data.SqlClient;
#else
using System.Data.SqlClient;
#endif

namespace Chloe.SqlServer
{
    public partial class MsSqlContextProvider : DbContextProvider
    {
        DatabaseProvider _databaseProvider;

        public MsSqlContextProvider(string connString, DbContext dbContext) : this(new DefaultDbConnectionFactory(connString), dbContext)
        {

        }

        public MsSqlContextProvider(Func<IDbConnection> dbConnectionFactory, DbContext dbContext) : this(new DbConnectionFactory(dbConnectionFactory), dbContext)
        {

        }

        public MsSqlContextProvider(IDbConnectionFactory dbConnectionFactory, DbContext dbContext) : this(new MsSqlOptions() { DbConnectionFactory = dbConnectionFactory }, dbContext)
        {

        }

        public MsSqlContextProvider(MsSqlOptions options, DbContext dbContext) : base(options, dbContext)
        {
            this._databaseProvider = new DatabaseProvider(this);
        }

        public new MsSqlOptions Options { get { return base.Options as MsSqlOptions; } }

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
                if (propertyDescriptor.IsAutoIncrement || propertyDescriptor.IsTimestamp())
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

            if (outputColumns.Count == 0)
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, entity, exp);
                }, insertExpression);

                await this.ExecuteNonQuery(insertExpression, @async);
                return entity;
            }

            List<Action<TEntity, IDataReader>> mappers = new List<Action<TEntity, IDataReader>>();
            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo;
            if (outputColumns.Count == 1 && outputColumns[0].IsAutoIncrement)
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, entity, exp);
                }, insertExpression);

                dbCommandInfo = translator.Translate(insertExpression);

                /* 自增 id 不能用 output inserted.Id 输出，因为如果表设置了触发器的话会报错 */
                dbCommandInfo.CommandText = string.Concat(dbCommandInfo.CommandText, ";", this.GetSelectLastInsertIdClause());
                mappers.Add(PublicHelper.GetMapper<TEntity>(outputColumns[0], 0));
            }
            else
            {
                foreach (PrimitivePropertyDescriptor outputColumn in outputColumns)
                {
                    mappers.Add(PublicHelper.GetMapper<TEntity>(outputColumn, insertExpression.Returns.Count));
                    insertExpression.Returns.Add(outputColumn.Column);
                }

                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, entity, exp);
                }, insertExpression);

                dbCommandInfo = translator.Translate(insertExpression);
            }

            IDataReader dataReader = this.Session.ExecuteReader(dbCommandInfo.CommandText, dbCommandInfo.GetParameters());
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
                    throw new ChloeException(string.Format("Could not insert value into the identity column '{0}'.", propertyDescriptor.Column.Name));

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

            foreach (var item in typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.HasSequence()))
            {
                DbMethodCallExpression getNextValueForSequenceExp = PublicHelper.MakeNextValueForSequenceDbExpression(item, dbTable.Schema);
                insertExpression.AppendInsertColumn(item.Column, getNextValueForSequenceExp);
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
                {
                    PublicHelper.EnsurePrimaryKeyNotNull(keyPropertyDescriptor, keyVal);
                }
            }

            if (keyPropertyDescriptor == null)
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
                }, insertExpression);

                await this.ExecuteNonQuery(insertExpression, @async);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }
            if (!keyPropertyDescriptor.IsAutoIncrement && !keyPropertyDescriptor.HasSequence())
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
                }, insertExpression);

                await this.ExecuteNonQuery(insertExpression, @async);
                return keyVal;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo;

            if (keyPropertyDescriptor.IsAutoIncrement)
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
                }, insertExpression);

                dbCommandInfo = translator.Translate(insertExpression);

                /* 自增 id 不能用 output inserted.Id 输出，因为如果表设置了触发器的话会报错 */
                dbCommandInfo.CommandText = string.Concat(dbCommandInfo.CommandText, ";", this.GetSelectLastInsertIdClause());
            }
            else if (keyPropertyDescriptor.HasSequence())
            {
                insertExpression.Returns.Add(keyPropertyDescriptor.Column);
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
                }, insertExpression);

                dbCommandInfo = translator.Translate(insertExpression);
            }
            else
            {
                insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
                {
                    return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
                }, insertExpression);

                dbCommandInfo = translator.Translate(insertExpression);
            }

            object ret = this.Session.ExecuteScalar(dbCommandInfo.CommandText, dbCommandInfo.GetParameters());
            if (ret == null || ret == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity/sequence value.");
            }

            ret = PublicHelper.ConvertObjectType(ret, typeDescriptor.AutoIncrement.PropertyType);
            return ret;
        }
        protected override async Task InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table, bool @async)
        {
            /*
             * 将 entities 分批插入数据库
             * 每批生成 insert into TableName(...) values(...),(...)... 
             */

            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int countPerBatch = batchSize ?? this.Options.DefaultBatchSizeForInsertRange; /* 每批实体个数 */

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<PrimitivePropertyDescriptor> mappingPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.IsAutoIncrement == false && !a.IsTimestamp()).ToList();

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
                        sqlBuilder.Append(",");

                    sqlBuilder.Append("(");
                    for (int j = 0; j < mappingPropertyDescriptors.Count; j++)
                    {
                        if (j > 0)
                            sqlBuilder.Append(",");

                        PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[j];

                        if (mappingPropertyDescriptor.HasSequence())
                        {
                            string sequenceSchema = mappingPropertyDescriptor.Definition.SequenceSchema;
                            sequenceSchema = string.IsNullOrEmpty(sequenceSchema) ? dbTable.Schema : sequenceSchema;

                            sqlBuilder.Append("NEXT VALUE FOR ");
                            if (!string.IsNullOrEmpty(sequenceSchema))
                            {
                                sqlBuilder.Append(Utils.QuoteName(sequenceSchema));
                                sqlBuilder.Append(".");
                            }
                            sqlBuilder.Append(Utils.QuoteName(mappingPropertyDescriptor.Definition.SequenceName));
                            continue;
                        }

                        object val = mappingPropertyDescriptor.GetValue(entity);

                        PublicHelper.NotNullCheck(mappingPropertyDescriptor, val);

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

                        if (PublicHelper.IsToStringableNumericType(valType))
                        {
                            sqlBuilder.Append(val.ToString());
                            continue;
                        }

                        if (val is bool)
                        {
                            if ((bool)val == true)
                                sqlBuilder.AppendFormat("1");
                            else
                                sqlBuilder.AppendFormat("0");
                            continue;
                        }

                        string paramName = UtilConstants.ParameterNamePrefix + dbParams.Count.ToString();
                        DbParam dbParam = new DbParam(paramName, val) { DbType = mappingPropertyDescriptor.Column.DbType };
                        dbParams.Add(dbParam);

                        if (!this.Options.BindParameterByName)
                            sqlBuilder.Append("?");
                        else
                            sqlBuilder.Append(paramName);
                    }
                    sqlBuilder.Append(")");

                    countOfCurrentBatch++;

                    if (countOfCurrentBatch >= countPerBatch || (dbParams.Count + mappingPropertyDescriptors.Count)/*为确保参数个数不超过最大限制*/ >= this.Options.MaxNumberOfParameters || (i + 1) == entities.Count)
                    {
                        sqlBuilder.Insert(0, sqlTemplate);
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

        /// <summary>
        /// 利用 SqlBulkCopy 批量插入数据。
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="table"></param>
        /// <param name="batchSize">设置 SqlBulkCopy.BatchSize 的值</param>
        /// <param name="bulkCopyTimeout">设置 SqlBulkCopy.BulkCopyTimeout 的值</param>
        /// <param name="keepIdentity">是否保留源自增值。false 由数据库分配自增值</param>
        public virtual void BulkInsert<TEntity>(List<TEntity> entities, string table = null, int? batchSize = null, int? bulkCopyTimeout = null, bool keepIdentity = false)
        {
            this.BulkInsert(entities, table, batchSize, bulkCopyTimeout, keepIdentity, false).GetResult();
        }
        public virtual async Task BulkInsertAsync<TEntity>(List<TEntity> entities, string table = null, int? batchSize = null, int? bulkCopyTimeout = null, bool keepIdentity = false)
        {
            await this.BulkInsert(entities, table, batchSize, bulkCopyTimeout, keepIdentity, true);
        }
        protected virtual async Task BulkInsert<TEntity>(List<TEntity> entities, string table, int? batchSize, int? bulkCopyTimeout, bool keepIdentity, bool @async)
        {
            PublicHelper.CheckNull(entities);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            DataTable dtToWrite = ConvertToSqlBulkCopyDataTable(entities, typeDescriptor);

            bool shouldCloseConnection = false;
            SqlConnection conn = this.Session.CurrentConnection as SqlConnection;
            try
            {
                SqlTransaction externalTransaction = null;
                if (this.Session.IsInTransaction)
                {
                    externalTransaction = this.Session.CurrentTransaction as SqlTransaction;
                }

                SqlBulkCopyOptions sqlBulkCopyOptions = SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.FireTriggers;
                if (keepIdentity)
                    sqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity | sqlBulkCopyOptions;

                SqlBulkCopy sbc = new SqlBulkCopy(conn, sqlBulkCopyOptions, externalTransaction);

                using (sbc)
                {
                    for (int i = 0; i < dtToWrite.Columns.Count; i++)
                    {
                        var column = dtToWrite.Columns[i];
                        sbc.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    if (batchSize != null)
                        sbc.BatchSize = batchSize.Value;

                    DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
                    sbc.DestinationTableName = AppendTableName(dbTable);

                    if (bulkCopyTimeout != null)
                        sbc.BulkCopyTimeout = bulkCopyTimeout.Value;

                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                        shouldCloseConnection = true;
                    }

                    if (@async)
                        await sbc.WriteToServerAsync(dtToWrite);
                    else
                        sbc.WriteToServer(dtToWrite);
                }
            }
            finally
            {
                if (conn != null)
                {
                    if (shouldCloseConnection && conn.State == ConnectionState.Open)
                        conn.Close();
                }
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

            PrimitivePropertyDescriptor rowVersionDescriptor = null;
            object rowVersionValue = null;
            object rowVersionNewValue = null;
            if (typeDescriptor.HasRowVersion())
            {
                rowVersionDescriptor = typeDescriptor.RowVersion;
                if (rowVersionDescriptor.IsTimestamp())
                {
                    rowVersionValue = rowVersionDescriptor.GetValue(entity);
                    this.EnsureRowVersionValueIsNotNull(rowVersionValue);
                    keyValues.Add(rowVersionDescriptor, rowVersionValue);
                }
                else
                {
                    rowVersionValue = rowVersionDescriptor.GetValue(entity);
                    rowVersionNewValue = PublicHelper.IncreaseRowVersionNumber(rowVersionValue);
                    updateExpression.AppendUpdateColumn(rowVersionDescriptor.Column, new DbParameterExpression(rowVersionNewValue, rowVersionDescriptor.PropertyType, rowVersionDescriptor.Column.DbType));
                    keyValues.Add(rowVersionDescriptor, rowVersionValue);
                }
            }

            if (updateExpression.UpdateColumns.Count == 0)
                return 0;

            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable);
            updateExpression.Condition = conditionExp;

            int rowsAffected = 0;
            if (rowVersionDescriptor == null)
            {
                rowsAffected = await this.ExecuteNonQuery(updateExpression, @async);
                if (entityState != null)
                    entityState.Refresh();
                return rowsAffected;
            }

            if (rowVersionDescriptor.IsTimestamp())
            {
                List<Action<TEntity, IDataReader>> mappers = new List<Action<TEntity, IDataReader>>();
                mappers.Add(PublicHelper.GetMapper<TEntity>(rowVersionDescriptor, updateExpression.Returns.Count));
                updateExpression.Returns.Add(rowVersionDescriptor.Column);

                IDataReader dataReader = await this.ExecuteReader(updateExpression, @async);
                using (dataReader)
                {
                    while (dataReader.Read())
                    {
                        rowsAffected++;
                        foreach (var mapper in mappers)
                        {
                            mapper(entity, dataReader);
                        }
                    }
                }

                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
            }
            else
            {
                rowsAffected = await this.ExecuteNonQuery(updateExpression, @async);
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
                rowVersionDescriptor.SetValue(entity, rowVersionNewValue);
            }

            if (entityState != null)
                entityState.Refresh();

            return rowsAffected;
        }

        protected override async Task<int> Delete<TEntity>(TEntity entity, string table, bool @async)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            PairList<PrimitivePropertyDescriptor, object> keyValues = new PairList<PrimitivePropertyDescriptor, object>(typeDescriptor.PrimaryKeys.Count);

            foreach (PrimitivePropertyDescriptor keyPropertyDescriptor in typeDescriptor.PrimaryKeys)
            {
                object keyValue = keyPropertyDescriptor.GetValue(entity);
                PublicHelper.EnsurePrimaryKeyNotNull(keyPropertyDescriptor, keyValue);
                keyValues.Add(keyPropertyDescriptor, keyValue);
            }

            PrimitivePropertyDescriptor rowVersionDescriptor = null;
            if (typeDescriptor.HasRowVersion())
            {
                rowVersionDescriptor = typeDescriptor.RowVersion;
                var rowVersionValue = typeDescriptor.RowVersion.GetValue(entity);
                this.EnsureRowVersionValueIsNotNull(rowVersionValue);
                keyValues.Add(typeDescriptor.RowVersion, rowVersionValue);
            }

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable);
            DbDeleteExpression e = new DbDeleteExpression(dbTable, conditionExp);

            int rowsAffected = await this.ExecuteNonQuery(e, @async);

            if (rowVersionDescriptor != null)
            {
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
            }

            return rowsAffected;
        }

        DataTable ConvertToSqlBulkCopyDataTable<TModel>(List<TModel> modelList, TypeDescriptor typeDescriptor)
        {
            DataTable dt = new DataTable();

            var mappingPropertyDescriptors = typeDescriptor.PrimitivePropertyDescriptors.Where(a => !a.IsTimestamp()).ToList();

            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];

                Type dataType = mappingPropertyDescriptor.PropertyType.GetUnderlyingType();
                if (dataType.IsEnum)
                    dataType = Enum.GetUnderlyingType(dataType);

                dt.Columns.Add(new DataColumn(mappingPropertyDescriptor.Column.Name, dataType));
            }

            foreach (var model in modelList)
            {
                DataRow dr = dt.NewRow();
                for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
                {
                    PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                    object value = mappingPropertyDescriptor.GetValue(model);

                    PublicHelper.NotNullCheck(mappingPropertyDescriptor, value);

                    if (mappingPropertyDescriptor.PropertyType.GetUnderlyingType().IsEnum)
                    {
                        if (value != null)
                            value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
                    }

                    dr[i] = value ?? DBNull.Value;
                }

                dt.Rows.Add(dr);
            }

            return dt;
        }
        void EnsureRowVersionValueIsNotNull(object rowVersionValue)
        {
            if (rowVersionValue == null)
            {
                throw new ArgumentException("The row version member of entity cannot be null.");
            }
        }
    }
}
