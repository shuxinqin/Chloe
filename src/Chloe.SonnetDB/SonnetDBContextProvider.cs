using Chloe.Core;
using Chloe.Exceptions;
using Chloe.Visitors;
using System.Linq.Expressions;
using System.Reflection;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.RDBMS;
using System.Data;
using System.Threading.Tasks;

namespace Chloe.SonnetDB
{
    public class SonnetDBContextProvider : DbContextProvider
    {
        DatabaseProvider _databaseProvider;

        public SonnetDBContextProvider(Func<IDbConnection> dbConnectionFactory, DbContext dbContext) : this(new DbConnectionFactory(dbConnectionFactory), dbContext)
        {

        }

        public SonnetDBContextProvider(IDbConnectionFactory dbConnectionFactory, DbContext dbContext) : this(new SonnetDBOptions() { DbConnectionFactory = dbConnectionFactory }, dbContext)
        {

        }

        public SonnetDBContextProvider(SonnetDBOptions options, DbContext dbContext) : base(options, dbContext)
        {
            this._databaseProvider = new DatabaseProvider(this);
        }

        public new SonnetDBOptions Options { get { return base.Options as SonnetDBOptions; } }
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
            EnsureInsertMappingSupported(typeDescriptor);

            InsertStrategy insertStrategy = this.Options.InsertStrategy;
            PrimitivePropertyDescriptor firstIgnoredProperty = null;
            object firstIgnoredPropertyValue = null;

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbInsertExpression insertExpression = new DbInsertExpression(dbTable);

            foreach (PrimitivePropertyDescriptor propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                    continue;

                object val = propertyDescriptor.GetValue(entity);
                Utils.EnsureSupportedValue(val);
                val = Utils.NormalizeParameterValue(val);

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

            if (typeDescriptor.AutoIncrement != null)
                insertExpression.Returns.Add(typeDescriptor.AutoIncrement.Column);

            insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, entity, exp);
            }, insertExpression);

            PrimitivePropertyDescriptor autoIncrementPropertyDescriptor = typeDescriptor.AutoIncrement;
            if (autoIncrementPropertyDescriptor == null)
            {
                await this.ExecuteNonQuery(insertExpression, @async);
                return entity;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo = translator.Translate(insertExpression);



            object retIdentity = await this.ExecuteScalar(dbCommandInfo, @async);

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, autoIncrementPropertyDescriptor.PropertyType);
            autoIncrementPropertyDescriptor.SetValue(entity, retIdentity);
            return entity;
        }

        protected override async Task<object> Insert<TEntity>(Expression<Func<TEntity>> content, string table, bool @async)
        {
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            EnsureInsertMappingSupported(typeDescriptor);

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

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement)
                {
                    PublicHelper.EnsurePrimaryKeyNotNull(keyPropertyDescriptor, keyVal);
                }
            }

            if (keyPropertyDescriptor != null && keyPropertyDescriptor.IsAutoIncrement)
                insertExpression.Returns.Add(keyPropertyDescriptor.Column);

            insertExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.InsertExecuting<TEntity>(this.Context, content, exp);
            }, insertExpression);

            if (keyPropertyDescriptor == null || !keyPropertyDescriptor.IsAutoIncrement)
            {
                await this.ExecuteNonQuery(insertExpression, @async);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            DbCommandInfo dbCommandInfo = translator.Translate(insertExpression);


            object retIdentity = await this.ExecuteScalar(dbCommandInfo, @async);
            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, typeDescriptor.AutoIncrement.PropertyType);
            return retIdentity;
        }

        static void EnsureInsertMappingSupported(TypeDescriptor typeDescriptor)
        {
            if (typeDescriptor.PrimaryKeys.Count == 0)
                throw new NotSupportedException("SonnetDB cannot write relational tables without a primary key. See https://github.com/IoTSharp/SonnetDB/issues/253.");
            if (typeDescriptor.PrimitivePropertyDescriptors.Any(a => a.HasSequence()))
                throw new NotSupportedException("SonnetDB does not support database sequences. See https://github.com/IoTSharp/SonnetDB/issues/231.");
            foreach (var property in typeDescriptor.PrimitivePropertyDescriptors)
                Utils.EnsureSupportedMappingType(property.PropertyType);
        }

        protected override string GetSelectLastInsertIdClause()
        {
            throw new NotSupportedException("SonnetDB identity values must be retrieved with INSERT RETURNING.");
        }
        protected override async Task InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table, bool @async)
        {
            PublicHelper.CheckNull(entities);
            if (entities.Count == 0)
                return;

            int countPerBatch = batchSize ?? this.Options.DefaultBatchSizeForInsertRange;
            if (countPerBatch <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize));

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            EnsureInsertMappingSupported(typeDescriptor);
            List<PrimitivePropertyDescriptor> properties = typeDescriptor.PrimitivePropertyDescriptors.Where(a => !a.IsAutoIncrement).ToList();
            if (properties.Count > this.Options.MaxNumberOfParameters)
                throw new NotSupportedException("The entity has more mapped columns than MaxNumberOfParameters.");

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            string sqlTemplate = AppendInsertRangeSqlTemplate(dbTable, properties);
            if (properties.Count > 0)
                countPerBatch = Math.Min(countPerBatch, this.Options.MaxNumberOfParameters / properties.Count);

            Func<Task> insertAction = async () =>
            {
                foreach (var interceptor in this.Context.Butler.DbContextInterceptors)
                    interceptor.InsertRangeExecuting(this.Context, entities);

                for (int offset = 0; offset < entities.Count; offset += countPerBatch)
                {
                    if (properties.Count == 0)
                    {
                        for (int i = offset; i < Math.Min(offset + countPerBatch, entities.Count); i++)
                            await this.Session.ExecuteNonQuery(sqlTemplate, new DbParam[0], @async);
                        continue;
                    }

                    StringBuilder sql = new StringBuilder(sqlTemplate);
                    List<DbParam> parameters = new List<DbParam>();
                    for (int i = offset; i < Math.Min(offset + countPerBatch, entities.Count); i++)
                    {
                        if (i > offset)
                            sql.Append(",");
                        sql.Append("(");
                        for (int j = 0; j < properties.Count; j++)
                        {
                            if (j > 0)
                                sql.Append(",");
                            PrimitivePropertyDescriptor property = properties[j];
                            object value = property.GetValue(entities[i]);
                            Utils.EnsureSupportedValue(value);
                            value = Utils.NormalizeParameterValue(value);
                            PublicHelper.NotNullCheck(property, value);
                            PublicHelper.EnsurePrimaryKeyNotNull(property, value);
                            Type valueType = property.PropertyType;
                            if (value != null && value.GetType().IsEnum)
                                value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
                            DbType? dbType = property.Column.DbType;
                            Type underlyingType = Nullable.GetUnderlyingType(valueType) ?? valueType;
                            if (underlyingType.IsEnum)
                                underlyingType = Enum.GetUnderlyingType(underlyingType);
                            if (underlyingType == typeof(sbyte) || underlyingType == typeof(ushort) || underlyingType == typeof(uint))
                            {
                                if (value != null && value != DBNull.Value)
                                    value = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                                valueType = typeof(long);
                                dbType = DbType.Int64;
                            }
                            if (value is decimal || value is ulong)
                            {
                                value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                                valueType = typeof(string);
                                dbType = DbType.String;
                            }
                            string parameterName = UtilConstants.ParameterNamePrefix + parameters.Count.ToString();
                            DbParam parameter = DbParam.Create(parameterName, value, valueType);
                            if (dbType != null)
                                parameter.DbType = dbType;
                            parameters.Add(parameter);
                            sql.Append(parameterName);
                        }
                        sql.Append(")");
                    }
                    await this.Session.ExecuteNonQuery(sql.ToString(), parameters.ToArray(), @async);
                }
            };

            if (this.Session.IsInTransaction)
            {
                await insertAction();
                return;
            }

            this.Session.BeginTransaction();
            try
            {
                await insertAction();
                this.Session.CommitTransaction();
            }
            catch (Exception insertException)
            {
                try
                {
                    this.Session.RollbackTransaction();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("SonnetDB batch insertion failed and rolling back the transaction also failed.", insertException, rollbackException);
                }
                throw;
            }
        }
        static string AppendInsertRangeSqlTemplate(DbTable dbTable, List<PrimitivePropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("INSERT INTO ");
            sqlBuilder.Append(AppendTableName(dbTable));
            if (mappingPropertyDescriptors.Count == 0)
                return sqlBuilder.Append(" DEFAULT VALUES").ToString();

            sqlBuilder.Append("(");

            for (int i = 0; i < mappingPropertyDescriptors.Count; i++)
            {
                PrimitivePropertyDescriptor mappingPropertyDescriptor = mappingPropertyDescriptors[i];
                if (i > 0)
                    sqlBuilder.Append(",");
                sqlBuilder.Append(Utils.QuoteName(mappingPropertyDescriptor.Column.Name));
            }

            sqlBuilder.Append(") VALUES");

            string sqlTemplate = sqlBuilder.ToString();
            return sqlTemplate;
        }

        static string AppendTableName(DbTable table)
        {
            if (!string.IsNullOrEmpty(table.Schema))
                throw new NotSupportedException("SonnetDB does not support database schemas. See https://github.com/IoTSharp/SonnetDB/issues/232.");

            return Utils.QuoteName(table.Name);
        }
    }
}
