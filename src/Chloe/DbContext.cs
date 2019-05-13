using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using Chloe.Query;
using Chloe.Core;
using Chloe.Infrastructure;
using Chloe.Descriptors;
using Chloe.DbExpressions;
using Chloe.Query.Internals;
using Chloe.Core.Visitors;
using Chloe.Exceptions;
using System.Data;
using Chloe.InternalExtensions;
using Chloe.Extensions;
using Chloe.Utility;

namespace Chloe
{
    public abstract partial class DbContext : IDbContext, IDisposable
    {
        bool _disposed = false;
        InternalAdoSession _adoSession;
        DbSession _session;

        Dictionary<Type, TrackEntityCollection> _trackingEntityContainer;

        Dictionary<Type, TrackEntityCollection> TrackingEntityContainer
        {
            get
            {
                if (this._trackingEntityContainer == null)
                {
                    this._trackingEntityContainer = new Dictionary<Type, TrackEntityCollection>();
                }

                return this._trackingEntityContainer;
            }
        }

        internal InternalAdoSession AdoSession
        {
            get
            {
                this.CheckDisposed();
                if (this._adoSession == null)
                    this._adoSession = new InternalAdoSession(this.DatabaseProvider.CreateConnection());
                return this._adoSession;
            }
        }
        public abstract IDatabaseProvider DatabaseProvider { get; }

        protected DbContext()
        {
            this._session = new DbSession(this);
        }

        public IDbSession Session { get { return this._session; } }


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
            return new Query<TEntity>(this, table, @lock);
        }

        public virtual TEntity QueryByKey<TEntity>(object key, bool tracking = false)
        {
            return this.QueryByKey<TEntity>(key, null, tracking);
        }
        public virtual TEntity QueryByKey<TEntity>(object key, string table, bool tracking = false)
        {
            return this.QueryByKey<TEntity>(key, table, LockType.Unspecified, tracking);
        }
        public virtual TEntity QueryByKey<TEntity>(object key, string table, LockType @lock, bool tracking = false)
        {
            Expression<Func<TEntity, bool>> condition = PrimaryKeyHelper.BuildCondition<TEntity>(key);
            var q = this.Query<TEntity>(table, @lock).Where(condition);

            if (tracking)
                q = q.AsTracking();

            return q.FirstOrDefault();
        }

        public virtual IJoiningQuery<T1, T2> JoinQuery<T1, T2>(Expression<Func<T1, T2, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value);

            return ret;
        }
        public virtual IJoiningQuery<T1, T2, T3> JoinQuery<T1, T2, T3>(Expression<Func<T1, T2, T3, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value);

            return ret;
        }
        public virtual IJoiningQuery<T1, T2, T3, T4> JoinQuery<T1, T2, T3, T4>(Expression<Func<T1, T2, T3, T4, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value)
                .Join<T4>(joinInfos[2].Key, (Expression<Func<T1, T2, T3, T4, bool>>)joinInfos[2].Value);

            return ret;
        }
        public virtual IJoiningQuery<T1, T2, T3, T4, T5> JoinQuery<T1, T2, T3, T4, T5>(Expression<Func<T1, T2, T3, T4, T5, object[]>> joinInfo)
        {
            KeyValuePairList<JoinType, Expression> joinInfos = ResolveJoinInfo(joinInfo);
            var ret = this.Query<T1>()
                .Join<T2>(joinInfos[0].Key, (Expression<Func<T1, T2, bool>>)joinInfos[0].Value)
                .Join<T3>(joinInfos[1].Key, (Expression<Func<T1, T2, T3, bool>>)joinInfos[1].Value)
                .Join<T4>(joinInfos[2].Key, (Expression<Func<T1, T2, T3, T4, bool>>)joinInfos[2].Value)
                .Join<T5>(joinInfos[3].Key, (Expression<Func<T1, T2, T3, T4, T5, bool>>)joinInfos[3].Value);

            return ret;
        }

        public virtual IEnumerable<T> SqlQuery<T>(string sql, params DbParam[] parameters)
        {
            return this.SqlQuery<T>(sql, CommandType.Text, parameters);
        }
        public virtual IEnumerable<T> SqlQuery<T>(string sql, CommandType cmdType, params DbParam[] parameters)
        {
            Utils.CheckNull(sql, "sql");
            return new InternalSqlQuery<T>(this, sql, cmdType, parameters);
        }
        public IEnumerable<T> SqlQuery<T>(string sql, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", new { Id = 1 }).ToList();
             */

            return this.SqlQuery<T>(sql, PublicHelper.BuildParams(this, parameter));
        }
        public IEnumerable<T> SqlQuery<T>(string sql, CommandType cmdType, object parameter)
        {
            /*
             * Usage:
             * dbContext.SqlQuery<User>("select * from Users where Id=@Id", CommandType.Text, new { Id = 1 }).ToList();
             */

            return this.SqlQuery<T>(sql, cmdType, PublicHelper.BuildParams(this, parameter));
        }


        public virtual TEntity Insert<TEntity>(TEntity entity)
        {
            return this.Insert(entity, null);
        }
        public virtual TEntity Insert<TEntity>(TEntity entity, string table)
        {
            Utils.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            Dictionary<PropertyDescriptor, object> keyValueMap = PrimaryKeyHelper.CreateKeyValueMap(typeDescriptor);

            Dictionary<PropertyDescriptor, DbExpression> insertColumns = new Dictionary<PropertyDescriptor, DbExpression>();
            foreach (PropertyDescriptor propertyDescriptor in typeDescriptor.PropertyDescriptors)
            {
                if (propertyDescriptor.IsAutoIncrement)
                    continue;

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
            DbInsertExpression e = new DbInsertExpression(dbTable);

            foreach (var kv in insertColumns)
            {
                e.InsertColumns.Add(kv.Key.Column, kv.Value);
            }

            PropertyDescriptor autoIncrementPropertyDescriptor = typeDescriptor.AutoIncrement;
            if (autoIncrementPropertyDescriptor == null)
            {
                this.ExecuteNonQuery(e);
                return entity;
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(e, out parameters);

            sql = string.Concat(sql, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = this.Session.ExecuteScalar(sql, parameters.ToArray());

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjType(retIdentity, autoIncrementPropertyDescriptor.PropertyType);
            autoIncrementPropertyDescriptor.SetValue(entity, retIdentity);
            return entity;
        }
        public virtual object Insert<TEntity>(Expression<Func<TEntity>> content)
        {
            return this.Insert(content, null);
        }
        public virtual object Insert<TEntity>(Expression<Func<TEntity>> content, string table)
        {
            Utils.CheckNull(content);

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
            DbInsertExpression e = new DbInsertExpression(explicitDbTable ?? typeDescriptor.Table);

            object keyVal = null;

            foreach (var kv in insertColumns)
            {
                MemberInfo key = kv.Key;
                PropertyDescriptor propertyDescriptor = typeDescriptor.TryGetPropertyDescriptor(key);

                if (propertyDescriptor == null)
                    throw new ChloeException(string.Format("The member '{0}' does not map any column.", key.Name));

                if (propertyDescriptor.IsAutoIncrement)
                    throw new ChloeException(string.Format("Could not insert value into the identity column '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsPrimaryKey)
                {
                    object val = ExpressionEvaluator.Evaluate(kv.Value);
                    if (val == null)
                        throw new ChloeException(string.Format("The primary key '{0}' could not be null.", propertyDescriptor.Property.Name));
                    else
                    {
                        keyVal = val;
                        e.InsertColumns.Add(propertyDescriptor.Column, DbExpression.Parameter(keyVal, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType));
                        continue;
                    }
                }

                e.InsertColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            if (keyPropertyDescriptor != null)
            {
                //主键为空并且主键又不是自增列
                if (keyVal == null && !keyPropertyDescriptor.IsAutoIncrement)
                {
                    throw new ChloeException(string.Format("The primary key '{0}' could not be null.", keyPropertyDescriptor.Property.Name));
                }
            }

            if (keyPropertyDescriptor == null || !keyPropertyDescriptor.IsAutoIncrement)
            {
                this.ExecuteNonQuery(e);
                return keyVal; /* It will return null if an entity does not define primary key. */
            }

            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string sql = translator.Translate(e, out parameters);
            sql = string.Concat(sql, ";", this.GetSelectLastInsertIdClause());

            //SELECT @@IDENTITY 返回的是 decimal 类型
            object retIdentity = this.Session.ExecuteScalar(sql, parameters.ToArray());

            if (retIdentity == null || retIdentity == DBNull.Value)
            {
                throw new ChloeException("Unable to get the identity value.");
            }

            retIdentity = PublicHelper.ConvertObjType(retIdentity, typeDescriptor.AutoIncrement.PropertyType);
            return retIdentity;
        }
        [Obsolete("This method will be removed in future versions.")]
        public virtual void InsertRange<TEntity>(List<TEntity> entities, bool keepIdentity = false, string table = null)
        {
            throw new NotImplementedException();
        }
        public virtual void InsertRange<TEntity>(List<TEntity> entities)
        {
            this.InsertRange(entities, null);
        }
        public virtual void InsertRange<TEntity>(List<TEntity> entities, string table)
        {
            throw new NotImplementedException();
        }

        public virtual int Update<TEntity>(TEntity entity)
        {
            return this.Update(entity, null);
        }
        public virtual int Update<TEntity>(TEntity entity, string table)
        {
            Utils.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            Dictionary<PropertyDescriptor, object> keyValueMap = PrimaryKeyHelper.CreateKeyValueMap(typeDescriptor);

            IEntityState entityState = this.TryGetTrackedEntityState(entity);
            Dictionary<PropertyDescriptor, DbExpression> updateColumns = new Dictionary<PropertyDescriptor, DbExpression>();
            foreach (PropertyDescriptor propertyDescriptor in typeDescriptor.PropertyDescriptors)
            {
                if (propertyDescriptor.IsPrimaryKey)
                {
                    keyValueMap[propertyDescriptor] = propertyDescriptor.GetValue(entity);
                    continue;
                }

                if (propertyDescriptor.IsAutoIncrement || propertyDescriptor.HasSequence())
                    continue;

                object val = propertyDescriptor.GetValue(entity);

                if (entityState != null && !entityState.HasChanged(propertyDescriptor, val))
                    continue;

                DbExpression valExp = DbExpression.Parameter(val, propertyDescriptor.PropertyType, propertyDescriptor.Column.DbType);
                updateColumns.Add(propertyDescriptor, valExp);
            }

            if (updateColumns.Count == 0)
                return 0;

            DbTable dbTable = table == null ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            DbExpression conditionExp = PrimaryKeyHelper.MakeCondition(keyValueMap, dbTable);
            DbUpdateExpression e = new DbUpdateExpression(dbTable, conditionExp);

            foreach (var item in updateColumns)
            {
                e.UpdateColumns.Add(item.Key.Column, item.Value);
            }

            int ret = this.ExecuteNonQuery(e);
            if (entityState != null)
                entityState.Refresh();
            return ret;
        }
        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content)
        {
            return this.Update(condition, content, null);
        }
        public virtual int Update<TEntity>(Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, string table)
        {
            Utils.CheckNull(condition);
            Utils.CheckNull(content);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            Dictionary<MemberInfo, Expression> updateColumns = InitMemberExtractor.Extract(content);

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);

            DbExpression conditionExp = expressionParser.ParseFilterPredicate(condition);

            DbUpdateExpression e = new DbUpdateExpression(explicitDbTable ?? typeDescriptor.Table, conditionExp);

            foreach (var kv in updateColumns)
            {
                MemberInfo key = kv.Key;
                PropertyDescriptor propertyDescriptor = typeDescriptor.TryGetPropertyDescriptor(key);

                if (propertyDescriptor == null)
                    throw new ChloeException(string.Format("The member '{0}' does not map any column.", key.Name));

                if (propertyDescriptor.IsPrimaryKey)
                    throw new ChloeException(string.Format("Could not update the primary key '{0}'.", propertyDescriptor.Column.Name));

                if (propertyDescriptor.IsAutoIncrement || propertyDescriptor.HasSequence())
                    throw new ChloeException(string.Format("Could not update the column '{0}', because it's mapping member is auto increment or has define a sequence.", propertyDescriptor.Column.Name));

                e.UpdateColumns.Add(propertyDescriptor.Column, expressionParser.Parse(kv.Value));
            }

            if (e.UpdateColumns.Count == 0)
                return 0;

            return this.ExecuteNonQuery(e);
        }

        public virtual int UpdateRange<TEntity>(List<TEntity> entities, string table = null)
        {
            throw new NotImplementedException();
        }

        public virtual int Delete<TEntity>(TEntity entity)
        {
            return this.Delete(entity, null);
        }
        public virtual int Delete<TEntity>(TEntity entity, string table)
        {
            PublicHelper.CheckNull(entity);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));
            PublicHelper.EnsureHasPrimaryKey(typeDescriptor);

            Dictionary<PropertyDescriptor, object> keyValueMap = new Dictionary<PropertyDescriptor, object>(typeDescriptor.PrimaryKeys.Count);

            foreach (PropertyDescriptor keyPropertyDescriptor in typeDescriptor.PrimaryKeys)
            {
                object keyVal = keyPropertyDescriptor.GetValue(entity);
                keyValueMap.Add(keyPropertyDescriptor, keyVal);
            }

            DbTable dbTable = table == null ? typeDescriptor.Table : new DbTable(table, typeDescriptor.Table.Schema);
            DbExpression conditionExp = PrimaryKeyHelper.MakeCondition(keyValueMap, dbTable);
            DbDeleteExpression e = new DbDeleteExpression(dbTable, conditionExp);
            return this.ExecuteNonQuery(e);
        }
        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition)
        {
            return this.Delete(condition, null);
        }
        public virtual int Delete<TEntity>(Expression<Func<TEntity, bool>> condition, string table)
        {
            Utils.CheckNull(condition);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(typeof(TEntity));

            DbTable explicitDbTable = null;
            if (table != null)
                explicitDbTable = new DbTable(table, typeDescriptor.Table.Schema);
            DefaultExpressionParser expressionParser = typeDescriptor.GetExpressionParser(explicitDbTable);
            DbExpression conditionExp = expressionParser.ParseFilterPredicate(condition);

            DbDeleteExpression e = new DbDeleteExpression(explicitDbTable ?? typeDescriptor.Table, conditionExp);

            return this.ExecuteNonQuery(e);
        }
        public virtual int DeleteByKey<TEntity>(object key)
        {
            return this.DeleteByKey<TEntity>(key, null);
        }
        public virtual int DeleteByKey<TEntity>(object key, string table)
        {
            Expression<Func<TEntity, bool>> condition = PrimaryKeyHelper.BuildCondition<TEntity>(key);
            return this.Delete<TEntity>(condition, table);
        }

        public void UseTransaction(Action action)
        {
            /*
             * dbContext.UseTransaction(() =>
             * {
             *     dbContext.Insert()...
             *     dbContext.Update()...
             *     dbContext.Delete()...
             * });
             */

            Utils.CheckNull(action);
            this.Session.BeginTransaction();
            ExecuteAction(this, action);
        }
        public void UseTransaction(Action action, IsolationLevel il)
        {
            Utils.CheckNull(action);
            this.Session.BeginTransaction(il);
            ExecuteAction(this, action);
        }
        static void ExecuteAction(IDbContext dbContext, Action action)
        {
            try
            {
                action();
                dbContext.Session.CommitTransaction();
            }
            catch
            {
                if (dbContext.Session.IsInTransaction)
                    dbContext.Session.RollbackTransaction();
                throw;
            }
        }


        public virtual void TrackEntity(object entity)
        {
            Utils.CheckNull(entity);
            Type entityType = entity.GetType();

            if (ReflectionExtension.IsAnonymousType(entityType))
                return;

            Dictionary<Type, TrackEntityCollection> entityContainer = this.TrackingEntityContainer;

            TrackEntityCollection collection;
            if (!entityContainer.TryGetValue(entityType, out collection))
            {
                TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);

                if (!typeDescriptor.HasPrimaryKey())
                    return;

                collection = new TrackEntityCollection(typeDescriptor);
                entityContainer.Add(entityType, collection);
            }

            collection.TryAddEntity(entity);
        }
        protected virtual string GetSelectLastInsertIdClause()
        {
            return "SELECT @@IDENTITY";
        }
        protected virtual IEntityState TryGetTrackedEntityState(object entity)
        {
            Utils.CheckNull(entity);
            Type entityType = entity.GetType();
            Dictionary<Type, TrackEntityCollection> entityContainer = this._trackingEntityContainer;

            if (entityContainer == null)
                return null;

            TrackEntityCollection collection;
            if (!entityContainer.TryGetValue(entityType, out collection))
            {
                return null;
            }

            IEntityState ret = collection.TryGetEntityState(entity);
            return ret;
        }

        protected int ExecuteNonQuery(DbExpression e)
        {
            List<DbParam> parameters;
            return this.ExecuteNonQuery(e, out parameters);
        }
        protected int ExecuteNonQuery(DbExpression e, out List<DbParam> parameters)
        {
            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            string cmdText = translator.Translate(e, out parameters);

            int rowsAffected = this.Session.ExecuteNonQuery(cmdText, parameters.ToArray());
            return rowsAffected;
        }
        protected IDataReader ExecuteReader(DbExpression e)
        {
            IDbExpressionTranslator translator = this.DatabaseProvider.CreateDbExpressionTranslator();
            List<DbParam> parameters;
            string cmdText = translator.Translate(e, out parameters);
            IDataReader dataReader = this.Session.ExecuteReader(cmdText, parameters.ToArray());
            return dataReader;
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

        class TrackEntityCollection
        {
            public TrackEntityCollection(TypeDescriptor typeDescriptor)
            {
                this.TypeDescriptor = typeDescriptor;
                this.Entities = new Dictionary<object, IEntityState>(1);
            }
            public TypeDescriptor TypeDescriptor { get; private set; }
            public Dictionary<object, IEntityState> Entities { get; private set; }
            public bool TryAddEntity(object entity)
            {
                if (this.Entities.ContainsKey(entity))
                {
                    return false;
                }

                IEntityState entityState = new EntityState(this.TypeDescriptor, entity);
                this.Entities.Add(entity, entityState);

                return true;
            }
            public IEntityState TryGetEntityState(object entity)
            {
                IEntityState ret;
                if (!this.Entities.TryGetValue(entity, out ret))
                    ret = null;

                return ret;
            }
        }
    }
}
