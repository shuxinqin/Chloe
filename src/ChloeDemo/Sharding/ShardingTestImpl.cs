using Chloe;
using Chloe.Dameng;
using Chloe.Dameng.DDL;
using Chloe.KingbaseES;
using Chloe.KingbaseES.DDL;
using Chloe.MySql;
using Chloe.MySql.DDL;
using Chloe.Oracle;
using Chloe.Oracle.DDL;
using Chloe.PostgreSQL;
using Chloe.PostgreSQL.DDL;
using Chloe.SqlServer;
using Chloe.SqlServer.DDL;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChloeDemo.Sharding
{
    internal class MsSqlShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"Data Source =.;Initial Catalog = order{year};Integrated Security = SSPI;;TrustServerCertificate=true";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new MsSqlContext(connString);
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new MsSqlContextProvider(connString, dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new SqlServerTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }

    internal class MySqlShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"Server=localhost;Port=3306;Database=order{year};Uid=root;Password=sasa;Charset=utf8; Pooling=True; Max Pool Size=200;Allow User Variables=True;SslMode=none;allowPublicKeyRetrieval=true";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new MySqlContext(new MySqlConnectionFactory(connString));
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new MySqlContextProvider(new MySqlConnectionFactory(connString), dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new MySqlTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }

    internal class OracleShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"Data Source=localhost/order{year};User ID=system;Password=sasa;";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new OracleContext(new OracleConnectionFactory(connString));
            dbContext.Options.DefaultBatchSizeForInsertRange = 200;
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new OracleContextProvider(new OracleConnectionFactory(connString), dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new OracleTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }

    internal class PostgreSQLShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"User ID=postgres;Password=sasa;Host=localhost;Port=5432;Database=order{year};Pooling=true;";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new PostgreSQLContext(new PostgreSQLConnectionFactory(connString));
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new PostgreSQLContextProvider(new PostgreSQLConnectionFactory(connString), dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new PostgreSQLTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }

    internal class DamengShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"Server=localhost:{year};User Id=SYSDBA; PWD=dm12345678;";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new DamengContext(new DamengConnectionFactory(connString));
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new DamengContextProvider(new DamengConnectionFactory(connString),dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new DamengTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }

    internal class KingbaseESShardingTest : ShardingTest
    {
        string GetConnString(int year)
        {
            string connString = $"Server=localhost;User Id=system;Password=sa;Port=54321;Database=order{year};Pooling=true;";
            return connString;
        }

        public override DbContext CreateInitDataDbContext(int year)
        {
            string connString = this.GetConnString(year);
            DbContext dbContext = new KingbaseESContext(new KingbaseESConnectionFactory(connString));
            return dbContext;
        }

        public override IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year)
        {
            string connString = this.GetConnString(year);
            var dbContextProvider = new KingbaseESContextProvider(new KingbaseESConnectionFactory(connString),dbContext);
            return dbContextProvider;
        }

        public override void CreateTable<TEntity>(DbContext dbContext, string table)
        {
            var tableGenerator = new KingbaseESTableGenerator(dbContext);
            tableGenerator.CreateTable(typeof(TEntity), table);
        }
    }
}
