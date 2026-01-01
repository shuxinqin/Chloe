using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.Infrastructure.Interception;
using Chloe.Query;
using Chloe.RDBMS;
using Chloe.Threading.Tasks;
using Chloe.Visitors;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Chloe.MySql
{
    public class MySqlContextProvider : DbContextProvider
    {
        DatabaseProvider _databaseProvider;

        public MySqlContextProvider(Func<IDbConnection> dbConnectionFactory, DbContext dbContext) : this(new DbConnectionFactory(dbConnectionFactory), dbContext)
        {

        }

        public MySqlContextProvider(IDbConnectionFactory dbConnectionFactory, DbContext dbContext) : this(new MySqlOptions() { DbConnectionFactory = dbConnectionFactory }, dbContext)
        {

        }

        public MySqlContextProvider(MySqlOptions options, DbContext dbContext) : base(options, dbContext)
        {
            this._databaseProvider = new DatabaseProvider(this);
        }

        public new MySqlOptions Options { get { return base.Options as MySqlOptions; } }


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

        public override IDatabaseProvider DatabaseProvider
        {
            get { return this._databaseProvider; }
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
                        sqlBuilder.Append(",");

                    sqlBuilder.Append("(");
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

        static string AppendInsertRangeSqlTemplate(DbTable table, List<PrimitivePropertyDescriptor> mappingPropertyDescriptors)
        {
            StringBuilder sqlBuilder = new StringBuilder();

            sqlBuilder.Append("INSERT INTO ");
            sqlBuilder.Append(AppendTableName(table));
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
            return Utils.QuoteName(table.Name);
        }

        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, int limits)
        {
            return this.Update<TEntity>(condition, content, null, limits);
        }
        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table, int limits)
        {
            return this.Update<TEntity>(condition, content, table, limits, false).GetResult();
        }
        public virtual async Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, int limits)
        {
            return await this.UpdateAsync<TEntity>(condition, content, null, limits);
        }
        public virtual async Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table, int limits)
        {
            return await this.Update<TEntity>(condition, content, table, limits, true);
        }
        protected virtual async Task<int> Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table, int limits, bool @async)
        {
            PublicHelper.CheckNull(condition);
            PublicHelper.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<KeyValuePair<MemberInfo, Expression>> updateColumns = InitMemberExtractor.Extract(QueryObjectExpressionTransformer.Transform(content));

            QueryContext queryContext = new QueryContext(this);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = FilterPredicateParser.Parse(queryContext, QueryObjectExpressionTransformer.Transform(condition), typeDescriptor, dbTable, null);

            MySqlDbUpdateExpression updateExpression = new MySqlDbUpdateExpression(dbTable, conditionExp);

            UpdateColumnExpressionParser expressionParser = typeDescriptor.GetUpdateColumnExpressionParser(dbTable, content.Parameters[0], queryContext);
            foreach (var kv in updateColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsPrimaryKey)
                    throw new ChloeException(string.Format("Could not update the primary key '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not update the identity column '{0}'.", propertyDescriptor.Column.Name));

                updateExpression.AppendUpdateColumn(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            updateExpression.Limits = limits;

            DbUpdateExpression execExp = updateExpression;

            execExp = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.UpdateExecuting<TEntity>(this.Context, condition, content, exp);
            }, execExp);

            if (execExp.UpdateColumns.Count == 0)
                return 0;

            return await this.ExecuteNonQuery(execExp, @async);
        }

        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition, int limits)
        {
            return this.Delete(condition, null, limits);
        }
        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table, int limits)
        {
            return this.Delete(condition, table, limits, false).GetResult();
        }
        public virtual async Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, bool>> condition, int limits)
        {
            return await this.DeleteAsync(condition, null, limits);
        }
        public virtual async Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, bool>> condition, string table, int limits)
        {
            return await this.Delete(condition, table, limits, true);
        }
        protected virtual async Task<int> Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table, int limits, bool @async)
        {
            PublicHelper.CheckNull(condition);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            QueryContext queryContext = new QueryContext(this);

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = FilterPredicateParser.Parse(queryContext, QueryObjectExpressionTransformer.Transform(condition), typeDescriptor, dbTable, null);

            MySqlDbDeleteExpression e = new MySqlDbDeleteExpression(dbTable, conditionExp);
            e.Limits = limits;

            DbDeleteExpression execExp = e;
            execExp = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.DeleteExecuting<TEntity>(this.Context, condition, exp);
            }, execExp);

            return await this.ExecuteNonQuery(execExp, @async);
        }
    }
}
