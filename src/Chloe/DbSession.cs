using Chloe.Infrastructure.Interception;
using System.Data;

namespace Chloe
{
    internal class DbSession : IDbSession
    {
        DbContext _dbContext;

        public DbSession(DbContext dbContext)
        {
            this._dbContext = dbContext;
        }

        IDbSessionProvider SessionProvider { get { return this._dbContext.DefaultDbContextProvider.Session; } }
        DbContextButler DbContextButler { get { return this._dbContext.Butler; } }

        public IDbContext DbContext { get { return this._dbContext; } }
        public IDbConnection CurrentConnection { get { return this.SessionProvider.CurrentConnection; } }

        public IDbTransaction CurrentTransaction { get { return this.SessionProvider.CurrentTransaction; } }
        public bool IsInTransaction { get { return this.DbContextButler.IsInTransaction; } }
        public int CommandTimeout { get { return this.DbContextButler.CommandTimeout; } set { this.DbContextButler.CommandTimeout = value; } }


        public void AddInterceptor(IDbCommandInterceptor interceptor)
        {
            this.DbContextButler.AddDbCommandInterceptor(interceptor);
        }
        public void RemoveInterceptor(IDbCommandInterceptor interceptor)
        {
            this.DbContextButler.RemoveDbCommandInterceptor(interceptor);
        }


        public int ExecuteNonQuery(string cmdText, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteNonQuery(cmdText, parameters);
        }
        public int ExecuteNonQuery(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteNonQuery(cmdText, cmdType, parameters);
        }
        public int ExecuteNonQuery(string cmdText, object parameter)
        {
            return this.SessionProvider.ExecuteNonQuery(cmdText, parameter);
        }
        public int ExecuteNonQuery(string cmdText, CommandType cmdType, object parameter)
        {
            return this.SessionProvider.ExecuteNonQuery(cmdText, cmdType, parameter);
        }

        public async Task<int> ExecuteNonQueryAsync(string cmdText, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteNonQueryAsync(cmdText, parameters);
        }
        public async Task<int> ExecuteNonQueryAsync(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteNonQueryAsync(cmdText, cmdType, parameters);
        }
        public async Task<int> ExecuteNonQueryAsync(string cmdText, object parameter)
        {
            return await this.SessionProvider.ExecuteNonQueryAsync(cmdText, parameter);
        }
        public async Task<int> ExecuteNonQueryAsync(string cmdText, CommandType cmdType, object parameter)
        {
            return await this.SessionProvider.ExecuteNonQueryAsync(cmdText, cmdType, parameter);
        }

        public object ExecuteScalar(string cmdText, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteScalar(cmdText, parameters);
        }
        public object ExecuteScalar(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteScalar(cmdText, cmdType, parameters);
        }
        public object ExecuteScalar(string cmdText, object parameter)
        {
            return this.SessionProvider.ExecuteScalar(cmdText, parameter);
        }
        public object ExecuteScalar(string cmdText, CommandType cmdType, object parameter)
        {
            return this.SessionProvider.ExecuteScalar(cmdText, cmdType, parameter);
        }

        public async Task<object> ExecuteScalarAsync(string cmdText, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteScalarAsync(cmdText, CommandType.Text, parameters);
        }
        public async Task<object> ExecuteScalarAsync(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteScalarAsync(cmdText, cmdType, parameters);
        }
        public async Task<object> ExecuteScalarAsync(string cmdText, object parameter)
        {
            return await this.SessionProvider.ExecuteScalarAsync(cmdText, parameter);
        }
        public async Task<object> ExecuteScalarAsync(string cmdText, CommandType cmdType, object parameter)
        {
            return await this.SessionProvider.ExecuteScalarAsync(cmdText, cmdType, parameter);
        }

        public IDataReader ExecuteReader(string cmdText, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteReader(cmdText, parameters);
        }
        public IDataReader ExecuteReader(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return this.SessionProvider.ExecuteReader(cmdText, cmdType, parameters);
        }
        public IDataReader ExecuteReader(string cmdText, object parameter)
        {
            return this.SessionProvider.ExecuteReader(cmdText, parameter);
        }
        public IDataReader ExecuteReader(string cmdText, CommandType cmdType, object parameter)
        {
            return this.SessionProvider.ExecuteReader(cmdText, cmdType, parameter);
        }

        public async Task<IDataReader> ExecuteReaderAsync(string cmdText, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteReaderAsync(cmdText, parameters);
        }
        public async Task<IDataReader> ExecuteReaderAsync(string cmdText, CommandType cmdType, params DbParam[] parameters)
        {
            return await this.SessionProvider.ExecuteReaderAsync(cmdText, cmdType, parameters);
        }
        public async Task<IDataReader> ExecuteReaderAsync(string cmdText, object parameter)
        {
            return await this.SessionProvider.ExecuteReaderAsync(cmdText, parameter);
        }
        public async Task<IDataReader> ExecuteReaderAsync(string cmdText, CommandType cmdType, object parameter)
        {
            return await this.SessionProvider.ExecuteReaderAsync(cmdText, cmdType, parameter);
        }

        public void UseTransaction(IDbTransaction dbTransaction)
        {
            this.DbContextButler.UseTransaction(dbTransaction);
        }
        public void BeginTransaction()
        {
            this.DbContextButler.BeginTransaction(null);
        }
        public void BeginTransaction(IsolationLevel il)
        {
            this.DbContextButler.BeginTransaction(il);
        }
        public void CommitTransaction()
        {
            this.DbContextButler.CommitTransaction();
        }
        public void RollbackTransaction()
        {
            this.DbContextButler.RollbackTransaction();
        }

        public void Dispose()
        {
            this._dbContext.Dispose();
        }
    }
}
