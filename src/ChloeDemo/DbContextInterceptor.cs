using Chloe;
using Chloe.DbExpressions;
using Chloe.Infrastructure.Interception;
using Chloe.MySql;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ChloeDemo
{
    /// <summary>
    /// 处理租户id
    /// </summary>
    public class DbContextInterceptor : IDbContextInterceptor
    {
        int _tenantId;

        public const string TenantIdColumnName = "TenantId";

        public DbContextInterceptor(int tenantId)
        {
            this._tenantId = tenantId;
        }

        public IQuery<TEntity> QueryCreated<TEntity>(IDbContext dbContext, IQuery<TEntity> query)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                var p = Expression.Parameter(typeof(TEntity));
                var m = Expression.MakeMemberAccess(p, typeof(TEntity).GetProperty("TenantId"));
                var tenantId = Expression.Constant(this._tenantId);
                var e = Expression.Equal(m, tenantId);
                var lambda = Expression.Lambda<Func<TEntity, bool>>(e, p);
                query = query.Where(lambda);
            }

            return query;
        }

        public DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbInsertExpression insertExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return this.MakeNewDbInsertExpression(insertExpression);
            }

            return insertExpression;
        }

        public DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity>> content, DbInsertExpression insertExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return this.MakeNewDbInsertExpression(insertExpression);
            }

            return insertExpression;
        }

        public void InsertRangeExecuting<TEntity>(IDbContext dbContext, List<TEntity> entities)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                Type type = typeof(TEntity);
                var property = type.GetProperty(TenantIdColumnName);
                foreach (var entity in entities)
                {
                    property.FastSetMemberValue(entity, this._tenantId);
                }
            }
        }

        public DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbUpdateExpression updateExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return MakeNewDbUpdateExpression(updateExpression);
            }

            return updateExpression;
        }

        public DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, DbUpdateExpression updateExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return MakeNewDbUpdateExpression(updateExpression);
            }

            return updateExpression;
        }

        public DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbDeleteExpression deleteExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return this.MakeNewDbDeleteExpression(deleteExpression);
            }

            return deleteExpression;
        }

        public DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, DbDeleteExpression deleteExpression)
        {
            if (IsTenantEntity(typeof(TEntity)))
            {
                return this.MakeNewDbDeleteExpression(deleteExpression);
            }

            return deleteExpression;
        }

        DbInsertExpression MakeNewDbInsertExpression(DbInsertExpression insertExpression)
        {
            DbInsertExpression ret = new DbInsertExpression(insertExpression.Table, insertExpression.InsertColumns.Count, insertExpression.Returns.Count);

            ret.Returns.AddRange(insertExpression.Returns);

            //处理租户id
            bool existsTenantId = false;
            for (int i = 0; i < insertExpression.InsertColumns.Count; i++)
            {
                DbColumnValuePair dbColumnValuePair = insertExpression.InsertColumns[i];
                if (dbColumnValuePair.Column.Name == TenantIdColumnName)
                {
                    DbParameterExpression valExp = new DbParameterExpression(this._tenantId, this._tenantId.GetType());
                    dbColumnValuePair = new DbColumnValuePair(dbColumnValuePair.Column, valExp);
                    existsTenantId = true;
                }

                ret.InsertColumns.Add(dbColumnValuePair);
            }

            if (!existsTenantId)
            {
                DbColumn tenantIdColumn = new DbColumn(TenantIdColumnName, this._tenantId.GetType());
                DbParameterExpression valExp = new DbParameterExpression(this._tenantId, this._tenantId.GetType());
                DbColumnValuePair tenantIdColumnValuePair = new DbColumnValuePair(tenantIdColumn, valExp);
                ret.InsertColumns.Add(tenantIdColumnValuePair);
            }

            return ret;
        }

        DbEqualExpression BuildTenantIdCondition(DbTable table)
        {
            DbColumn dbColumn = new DbColumn(TenantIdColumnName, this._tenantId.GetType());
            DbExpression left = new DbColumnAccessExpression(table, dbColumn);
            DbExpression right = new DbParameterExpression(this._tenantId);
            DbEqualExpression equalExp = new DbEqualExpression(left, right);

            return equalExp;
        }

        DbUpdateExpression MakeNewDbUpdateExpression(DbUpdateExpression updateExpression)
        {
            DbEqualExpression tenantIdCondition = this.BuildTenantIdCondition(updateExpression.Table);
            DbExpression condition = updateExpression.Condition.And(tenantIdCondition);
            DbUpdateExpression ret;
            if (updateExpression is MySqlDbUpdateExpression)
            {
                MySqlDbUpdateExpression mySqlDbUpdateExpression = new MySqlDbUpdateExpression(updateExpression.Table, condition, updateExpression.UpdateColumns.Count, updateExpression.Returns.Count);
                mySqlDbUpdateExpression.Limits = (updateExpression as MySqlDbUpdateExpression).Limits;
                ret = mySqlDbUpdateExpression;
            }
            else
            {
                ret = new DbUpdateExpression(updateExpression.Table, condition, updateExpression.UpdateColumns.Count, updateExpression.Returns.Count);
            }

            ret.Returns.AddRange(updateExpression.Returns);
            for (int i = 0; i < updateExpression.UpdateColumns.Count; i++)
            {
                DbColumnValuePair dbColumnValuePair = updateExpression.UpdateColumns[i];
                if (dbColumnValuePair.Column.Name == TenantIdColumnName)
                {
                    continue;
                }

                ret.UpdateColumns.Add(dbColumnValuePair);
            }

            return ret;
        }

        DbDeleteExpression MakeNewDbDeleteExpression(DbDeleteExpression deleteExpression)
        {
            DbEqualExpression tenantIdCondition = this.BuildTenantIdCondition(deleteExpression.Table);
            DbDeleteExpression ret;
            if (deleteExpression is MySqlDbDeleteExpression)
            {
                MySqlDbDeleteExpression mySqlDbDeleteExpression = new MySqlDbDeleteExpression(deleteExpression.Table, deleteExpression.Condition.And(tenantIdCondition));
                mySqlDbDeleteExpression.Limits = (deleteExpression as MySqlDbDeleteExpression).Limits;
                ret = mySqlDbDeleteExpression;
            }
            else
            {
                ret = new DbDeleteExpression(deleteExpression.Table, deleteExpression.Condition.And(tenantIdCondition));
            }

            return ret;
        }

        static bool IsTenantEntity(Type type)
        {
            bool hasTenantId = type.GetProperty(TenantIdColumnName) != null;
            return hasTenantId;
        }
    }
}
