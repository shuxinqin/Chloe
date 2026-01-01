using Chloe.Core;
using Chloe.Data;
using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.Infrastructure.Interception;
using Chloe.Query;
using Chloe.Query.Internals;
using Chloe.Threading.Tasks;
using Chloe.Utility;
using Chloe.Visitors;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe
{
    public abstract partial class DbContextProvider : IDbContextProvider, IDisposable
    {
        bool _disposed = false;
        InnerAdoSession _adoSession;
        DbSessionProvider _session;
        Dictionary<Type, List<LambdaExpression>> _queryFilters = new Dictionary<Type, List<LambdaExpression>>();
        TrackingEntityContainer _trackingEntityContainer;

        protected DbContextProvider(DbOptions options, DbContext dbContext)
        {
            PublicHelper.CheckNull(options, nameof(options));

            this.Options = options;
            this.Context = dbContext;
            this._session = new DbSessionProvider(this);
        }

        public DbContext Context { get; private set; }

        public DbOptions Options { get; private set; }

        public IDbSessionProvider Session { get { return this._session; } }

        TrackingEntityContainer TrackingEntityContainer
        {
            get
            {
                if (this._trackingEntityContainer == null)
                {
                    this._trackingEntityContainer = new TrackingEntityContainer();
                }

                return this._trackingEntityContainer;
            }
        }

        internal Dictionary<Type, List<LambdaExpression>> QueryFilters { get { return this._queryFilters; } }
        internal List<LambdaExpression> GetQueryFilters(Type entityType)
        {
            List<LambdaExpression> filters = this._queryFilters.FindValue(entityType);
            return filters ?? new List<LambdaExpression>();
        }


        internal InnerAdoSession AdoSession
        {
            get
            {
                this.CheckDisposed();
                if (this._adoSession == null)
                    this._adoSession = new InnerAdoSession(this.DatabaseProvider.CreateConnection());
                return this._adoSession;
            }
        }

        public abstract IDatabaseProvider DatabaseProvider { get; }

        public void HasQueryFilter<TEntity>(Expression<Func<TEntity, bool>> filter)
        {
            Type entityType = typeof(TEntity);
            this.HasQueryFilter(entityType, filter);
        }
        public void HasQueryFilter(Type entityType, LambdaExpression filter)
        {
            PublicHelper.CheckNull(filter, nameof(filter));
            List<LambdaExpression> filters;
            if (!this._queryFilters.TryGetValue(entityType, out filters))
            {
                filters = new List<LambdaExpression>(1);
                this._queryFilters.Add(entityType, filters);
            }

            filters.Add(filter);
        }

        public virtual IQuery<TEntity> Query<TEntity>()
        {
            return this.Query<TEntity>(null);
        }
        public virtual IQuery<TEntity> Query<TEntity>(string table)
        {
            return this.Query<TEntity>(table, LockType.Unspecified);
        }
        public virtual IQuery<TEntity> Query<TEntity>(LockType @lock)
        {
            return this.Query<TEntity>(null, @lock);
        }
        public virtual IQuery<TEntity> Query<TEntity>(string table, LockType @lock)
        {
            IQuery<TEntity> query = new Query<TEntity>(this, table, @lock);
            for (int i = 0; i < this.Context.Butler.DbContextInterceptors.Count; i++)
            {
                query = this.Context.Butler.DbContextInterceptors[i].QueryCreated(this.Context, query);
            }

            return query;
        }

        public virtual List<T> SqlQuery<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            PublicHelper.CheckNull(sql, "sql");
            return new InternalSqlQuery<T>(new QueryContext(this), sql, cmdType, parameters).AsIEnumerable().ToList();
        }
        public virtual Task<List<T>> SqlQueryAsync<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            PublicHelper.CheckNull(sql, "sql");
            return new InternalSqlQuery<T>(new QueryContext(this), sql, cmdType, parameters).AsIAsyncEnumerable().ToListAsync().AsTask();
        }

        public List<T> SqlQuery<T>(string sql, CommandType cmdType, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", CommandType.Text, new { Id = 1 });
             */

            return this.SqlQuery<T>(sql, cmdType, PublicHelper.BuildParams(this, parameter));
        }
        public Task<List<T>> SqlQueryAsync<T>(string sql, CommandType cmdType, object parameter)
        {
            return this.SqlQueryAsync<T>(sql, cmdType, PublicHelper.BuildParams(this, parameter));
        }

        public TEntity Save<TEntity>(TEntity entity)
        {
            return Helpers.Save<TEntity>(this, entity, false).GetResult();
        }
        public Task<TEntity> SaveAsync<TEntity>(TEntity entity)
        {
            return Helpers.Save<TEntity>(this, entity, true);
        }

        public virtual TEntity Insert<TEntity>(TEntity entity, string table)
        {
            return this.Insert(entity, table, false).GetResult();
        }
        public virtual Task<TEntity> InsertAsync<TEntity>(TEntity entity, string table)
        {
            return this.Insert(entity, table, true);
        }
        protected virtual async Task<TEntity> Insert<TEntity>(TEntity entity, string table, bool @async)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

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

            dbCommandInfo.CommandText = string.Concat(dbCommandInfo.CommandText, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = await this.ExecuteScalar(dbCommandInfo, @async);

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, autoIncrementPropertyDescriptor.PropertyType);
            autoIncrementPropertyDescriptor.SetValue(entity, retIdentity);
            return entity;
        }

        public virtual object Insert<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            return this.Insert(content, table, false).GetResult();
        }
        public virtual Task<object> InsertAsync<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            return this.Insert(content, table, true);
        }
        protected virtual async Task<object> Insert<TEntity>(Expression<Func<TEntity>> content, string table, bool @async)
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
            dbCommandInfo.CommandText = string.Concat(dbCommandInfo.CommandText, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = await this.ExecuteScalar(dbCommandInfo, @async);
            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjectType(retIdentity, typeDescriptor.AutoIncrement.PropertyType);
            return retIdentity;
        }

        public virtual void InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table)
        {
            this.InsertRange(entities, batchSize, table, false).GetResult();
        }
        public virtual Task InsertRangeAsync<TEntity>(List<TEntity> entities, int? batchSize, string table)
        {
            return this.InsertRange(entities, batchSize, table, true);
        }
        protected virtual Task InsertRange<TEntity>(List<TEntity> entities, int? batchSize, string table, bool @async)
        {
            throw new NotImplementedException();
        }

        public virtual int Update<TEntity>(TEntity entity, string table)
        {
            return this.Update<TEntity>(entity, table, false).GetResult();
        }
        public virtual Task<int> UpdateAsync<TEntity>(TEntity entity, string table)
        {
            return this.Update<TEntity>(entity, table, true);
        }
        protected virtual async Task<int> Update<TEntity>(TEntity entity, string table, bool @async)
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


            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable);
            updateExpression.Condition = conditionExp;

            updateExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.UpdateExecuting<TEntity>(this.Context, entity, exp);
            }, updateExpression);

            if (updateExpression.UpdateColumns.Count == 0)
                return 0;

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

        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table)
        {
            return this.Update<TEntity>(condition, content, table, false).GetResult();
        }
        public virtual Task<int> UpdateAsync<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table)
        {
            return this.Update<TEntity>(condition, content, table, true);
        }
        protected virtual async Task<int> Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table, bool @async)
        {
            PublicHelper.CheckNull(condition);
            PublicHelper.CheckNull(content);

            QueryContext queryContext = new QueryContext(this);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            List<KeyValuePair<MemberInfo, Expression>> updateColumns = InitMemberExtractor.Extract(QueryObjectExpressionTransformer.Transform(content));

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbUpdateExpression updateExpression = new DbUpdateExpression(dbTable);
            DbExpression conditionExp = FilterPredicateParser.Parse(queryContext, QueryObjectExpressionTransformer.Transform(condition), typeDescriptor, dbTable, null);
            updateExpression.Condition = conditionExp;

            UpdateColumnExpressionParser expressionParser = typeDescriptor.GetUpdateColumnExpressionParser(dbTable, content.Parameters[0], queryContext);
            foreach (var kv in updateColumns)
            {
                MemberInfo key = kv.Key;
                PrimitivePropertyDescriptor propertyDescriptor = typeDescriptor.GetPrimitivePropertyDescriptor(key);

                if (propertyDescriptor.IsPrimaryKey)
                    throw new ChloeException(string.Format("Could not update the primary key '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsAutoIncrement || propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Could not update the column '{0}', because it's mapping member is auto increment or has define a sequence.", propertyDescriptor.Column.Name));

                updateExpression.AppendUpdateColumn(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            updateExpression = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.UpdateExecuting<TEntity>(this.Context, condition, content, exp);
            }, updateExpression);

            if (updateExpression.UpdateColumns.Count == 0)
                return 0;

            return await this.ExecuteNonQuery(updateExpression, @async);
        }

        public virtual int Delete<TEntity>(TEntity entity, string table)
        {
            return this.Delete<TEntity>(entity, table, false).GetResult();
        }
        public virtual Task<int> DeleteAsync<TEntity>(TEntity entity, string table)
        {
            return this.Delete<TEntity>(entity, table, true);
        }
        protected virtual async Task<int> Delete<TEntity>(TEntity entity, string table, bool @async)
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

            if (typeDescriptor.HasRowVersion())
            {
                var rowVersionValue = typeDescriptor.RowVersion.GetValue(entity);
                keyValues.Add(typeDescriptor.RowVersion, rowVersionValue);
            }

            DbTable dbTable = PublicHelper.CreateDbTable(typeDescriptor, table);
            DbExpression conditionExp = PublicHelper.MakeCondition(keyValues, dbTable);
            DbDeleteExpression e = new DbDeleteExpression(dbTable, conditionExp);

            e = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.DeleteExecuting<TEntity>(this.Context, entity, exp);
            }, e);

            int rowsAffected = await this.ExecuteNonQuery(e, @async);

            if (typeDescriptor.HasRowVersion())
            {
                PublicHelper.CauseErrorIfOptimisticUpdateFailed(rowsAffected);
            }

            return rowsAffected;
        }

        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table)
        {
            return this.Delete<TEntity>(condition, table, false).GetResult();
        }
        public virtual Task<int> DeleteAsync<TEntity>(Expression<Func<TEntity, bool>> condition, string table)
        {
            return this.Delete<TEntity>(condition, table, true);
        }
        protected virtual Task<int> Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table, bool @async)
        {
            PublicHelper.CheckNull(condition);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            DbTable dbTable = typeDescriptor.GenDbTable(table);
            DbExpression conditionExp = FilterPredicateParser.Parse(new QueryContext(this), QueryObjectExpressionTransformer.Transform(condition), typeDescriptor, dbTable, null);

            DbDeleteExpression e = new DbDeleteExpression(dbTable, conditionExp);

            e = this.ExecuteDbContextInterceptor((dbContextInterceptor, exp) =>
            {
                return dbContextInterceptor.DeleteExecuting<TEntity>(this.Context, condition, exp);
            }, e);

            return this.ExecuteNonQuery(e, @async);
        }

        public virtual void TrackEntity(object entity)
        {
            this.TrackingEntityContainer.Add(entity);
        }
        protected virtual string GetSelectLastInsertIdClause()
        {
            return "SELECT @@IDENTITY";
        }
        protected virtual IEntityState TryGetTrackedEntityState(object entity)
        {
            IEntityState ret = this.TrackingEntityContainer.GetEntityState(entity);
            return ret;
        }


        public void Dispose()
        {
            if (this._disposed)
                return;

            if (this._adoSession != null)
                this._adoSession.Dispose();
            this.Dispose(true);
            this._disposed = true;
        }
        protected virtual void Dispose(bool disposing)
        {

        }
        void CheckDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        protected TExp ExecuteDbContextInterceptor<TExp>(Func<IDbContextInterceptor, TExp, TExp> action, TExp exp)
        {
            for (int i = 0; i < this.Context.Butler.DbContextInterceptors.Count; i++)
            {
                exp = action(this.Context.Butler.DbContextInterceptors[i], exp);
            }

            return exp;
        }
    }
}
