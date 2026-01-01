using Chloe.DbExpressions;
using System.Linq.Expressions;

namespace Chloe.Infrastructure.Interception
{
    public interface IDbContextInterceptor
    {
        IQuery<TEntity> QueryCreated<TEntity>(IDbContext dbContext, IQuery<TEntity> query);

        DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbInsertExpression insertExpression);
        DbInsertExpression InsertExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity>> content, DbInsertExpression insertExpression);
        void InsertRangeExecuting<TEntity>(IDbContext dbContext, List<TEntity> entities);

        DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbUpdateExpression updateExpression);
        DbUpdateExpression UpdateExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, Expression<Func<TEntity, TEntity>> content, DbUpdateExpression updateExpression);

        DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, TEntity entity, DbDeleteExpression deleteExpression);
        DbDeleteExpression DeleteExecuting<TEntity>(IDbContext dbContext, Expression<Func<TEntity, bool>> condition, DbDeleteExpression deleteExpression);
    }
}
