using Chloe;
using Chloe.Sharding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ChloeDemo.Sharding
{
    public abstract class ShardingTest
    {
        protected ShardingTest()
        {

        }

        public async Task Run()
        {
            /*
             * 注：
             * 运行此测试前请手动创建好四个数据库（order2018、order2019、order2020、order2021，不用建表，程序会生成相应的表并初始化数据），然后修改 ShardingTestImpl.cs 里的数据库连接字符串
             */

            await InitData();

            ShardingQueryTest queryTest = new ShardingQueryTest(this);
            await queryTest.Run();  //运行查询测试


            //增删查改测试
            ShardingConfigBuilder<Order> shardingConfigBuilder = new ShardingConfigBuilder<Order>();
            shardingConfigBuilder.HasShardingKey(a => a.CreateTime);     //配置分片字段
            shardingConfigBuilder.HasRoute(new OrderShardingRoute(this, new List<int>() { 2020, 2021 })); //设置分片路由。此测试使用 2020, 2021 两个分库

            ShardingConfigContainer.Add(shardingConfigBuilder.Build());     //注册分片配置信息

            await this.CrudTest();
            await this.InsertRangeTest();
            await this.UpdateByLambdaTest();
            await this.DeleteByLambdaTest();

            Console.WriteLine("over...");
            Console.ReadKey();
        }

        /// <summary>
        /// 根据年份，获取分库的 DbContext 对象
        /// </summary>
        /// <param name="year"></param>
        /// <returns></returns>
        public abstract DbContext CreateInitDataDbContext(int year);

        /// <summary>
        /// 创建数据源
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        public abstract IDbContextProvider CreateDbContextProvider(DbContext dbContext, int year);

        /// <summary>
        /// 根据表名生成分表
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="dbContext"></param>
        /// <param name="table"></param>
        public abstract void CreateTable<TEntity>(DbContext dbContext, string table);

        public IDbContext CreateDbContext()
        {
            /*
             * 创建一个普通的 DbContext。
             * 注：实际应用中应该使用 MsSqlContext、MySqlContext、OracleContext 等上下文类，但因为这只是演示 sharding 功能，操作的全是分表，
             * 由于最终创建分表数据库连接以及分表操作（增删查改）调用的是 OrderShardingRoute.cs 里 AllTables 集合中的 RouteTable 对象的 RouteTable.DataSource.DbContextProviderFactory 工厂创建出来的 IDbContextProvider 对象，
             * 所以只使用普通的 DbContext（如果有非分表，请使用 MsSqlContext、MySqlContext、OracleContext 等上下文类）
             */
            DbContext dbContext = new DbContext();  //实际应用中应该使用 MsSqlContext、MySqlContext、OracleContext 等上下文类
            dbContext.ShardingOptions.MaxConnectionsPerDataSource = 6;
            return dbContext;
        }

        /// <summary>
        /// 初始化测试数据
        /// </summary>
        /// <returns></returns>
        public async Task InitData()
        {
            /*
             * 按年分库，按月分表
             * 每天两条数据
             * 注：运行程序前请手动创建好四个数据库（order2018、order2019、order2020、order2021），然后修改 ShardingTestImpl.cs 里的数据库连接字符串
             */

            await InitData(2018);
            await InitData(2019);
            await InitData(2020);
            await InitData(2021);
            Console.WriteLine("InitData over");
        }

        /// <summary>
        /// 初始化分库测试数据。根据年份，初始化指定分库中的各个分表数据
        /// </summary>
        /// <param name="year"></param>
        /// <returns></returns>
        public async Task InitData(int year)
        {
            /*
             * 按月分表
             * 每天两条数据
             * 注：需要手动建库
             */
            using DbContext dbContext = this.CreateInitDataDbContext(year);
            dbContext.ShardingEnabled = false;


            for (int month = 1; month <= 12; month++)
            {
                string table = BuildTableName(month);

                this.CreateTable<Order>(dbContext, table);

                /*
                 * 初始化分表数据
                 */

                bool hasData = dbContext.Query<Order>(table).Any();
                if (hasData)
                {
                    continue;
                }
                //Console.WriteLine($"insert year:{year} table:{table}");
                //Console.ReadKey();
                DateTime firstDate = new DateTime(year, month, 1);

                int day = 0;
                while (true)
                {
                    DateTime date = firstDate.AddDays(day);

                    if (date.Month != month)
                    {
                        break;
                    }

                    Order order1 = new Order();
                    order1.UserId = "chloe";
                    order1.Amount = 10;
                    order1.SetCreateTime(date.AddHours(10));

                    Order order2 = new Order();
                    order2.UserId = "shuxin";
                    order2.Amount = 20;
                    order2.SetCreateTime(date.AddHours(12));

                    dbContext.Insert(order1, table);
                    dbContext.Insert(order2, table);

                    day++;
                }
            }
        }

        /// <summary>
        /// 根据月份构造表名。此 demo 分表表名规则为 order+月份，你可以根据你自己喜好，拼接适合你自己的表名
        /// </summary>
        /// <param name="month"></param>
        /// <returns></returns>
        public static string BuildTableName(int month)
        {
            string suffix = month.ToString("00");
            string table = $"order{suffix}";
            return table;
        }



        async Task CrudTest()
        {
            /*
             * 增删查改
             */
            using IDbContext dbContext = this.CreateDbContext();

            int rowsAffected = 0;
            DateTime createTime = new DateTime(2021, 1, 1, 1, 1, 1);

            Order order = new Order();
            order.UserId = "chloe";
            order.Amount = 10;
            order.SetCreateTime(createTime);

            rowsAffected = await dbContext.DeleteAsync(order);
            Console.WriteLine($"deleted: {rowsAffected}");

            await dbContext.InsertAsync(order);

            var q = dbContext.Query<Order>();

            Order entity = null;
            entity = await q.Where(a => a.Id == order.Id).FirstOrDefaultAsync();
            Debug.Assert(entity.Id == order.Id);

            //加上分片字段，则会精准定位到所在的表
            entity = await q.Where(a => a.Id == order.Id && a.CreateTime == order.CreateTime).FirstOrDefaultAsync();
            Debug.Assert(entity.Id == order.Id);

            entity.Amount = 100;
            rowsAffected = await dbContext.UpdateAsync(entity);

            Debug.Assert(rowsAffected == 1);

            entity = await q.Where(a => a.Id == entity.Id).FirstOrDefaultAsync();
            Debug.Assert(entity.Amount == 100);

            rowsAffected = await dbContext.DeleteAsync(entity);
            Debug.Assert(rowsAffected == 1);

            entity = await q.Where(a => a.Id == entity.Id).FirstOrDefaultAsync();
            Debug.Assert(entity == null);

            Helpers.PrintSplitLine();
        }

        async Task InsertRangeTest()
        {
            using IDbContext dbContext = this.CreateDbContext();

            List<Order> orders = this.CreateOrders(2020).Concat(this.CreateOrders(2021)).ToList();

            //批量插入
            await dbContext.InsertRangeAsync(orders);

            List<string> ids = orders.Select(a => a.Id).ToList();

            //从分表中查出刚刚插入的数据
            List<Order> orders1 = await dbContext.Query<Order>(a => ids.Contains(a.Id)).ToListAsync();

            orders1 = orders1.OrderBy(a => a.Id).ToList();
            orders = orders.OrderBy(a => a.Id).ToList();

            Debug.Assert(orders1.Count == orders.Count);

            for (int i = 0; i < orders1.Count; i++)
            {
                Debug.Assert(orders1[i].Id == orders[i].Id);
            }

            //删除刚刚插入的数据
            int rowsAffected = await dbContext.DeleteAsync<Order>(a => ids.Contains(a.Id));

            Debug.Assert(rowsAffected == orders.Count);

            Helpers.PrintSplitLine();
        }

        async Task UpdateByLambdaTest()
        {
            /*
             * 按条件更新
             */
            using IDbContext dbContext = this.CreateDbContext();

            await InitData(2021);

            int rowsAffected = 0;

            List<Order> orders = await dbContext.Query<Order>(a => a.CreateYear == 2021).ToListAsync();

            string newUserId = "chloe2021";
            rowsAffected = await dbContext.UpdateAsync<Order>(a => a.CreateYear == 2021, a => new Order()
            {
                UserId = newUserId,
            });

            Debug.Assert(rowsAffected == orders.Count);

            orders = await dbContext.Query<Order>().Where(a => a.CreateYear == 2021).ToListAsync();
            Debug.Assert(orders.All(a => a.UserId == newUserId));

            orders = await dbContext.Query<Order>().Where(a => a.CreateYear == 2021 && a.CreateMonth == 12).ToListAsync();
            rowsAffected = await dbContext.UpdateAsync<Order>(a => a.CreateYear == 2021 && a.CreateMonth == 12, a => new Order()
            {
                UserId = "chloe2021",
            });

            Debug.Assert(rowsAffected == orders.Count);

            Helpers.PrintSplitLine();
        }

        async Task DeleteByLambdaTest()
        {
            /*
             * 按条件删除
             */
            using IDbContext dbContext = this.CreateDbContext();

            int rowsAffected = 0;

            List<Order> orders = await dbContext.Query<Order>().Where(a => a.CreateMonth == 1 || a.CreateMonth == 2).ToListAsync();

            Debug.Assert(orders.Count > 0);

            rowsAffected = await dbContext.DeleteAsync<Order>(a => a.CreateMonth == 1 || a.CreateMonth == 2);

            Debug.Assert(rowsAffected == orders.Count);

            Helpers.PrintSplitLine();
        }

        List<Order> CreateOrders(int year)
        {
            List<Order> orders = new List<Order>();

            for (int month = 1; month <= 12; month++)
            {
                DateTime firstDate = new DateTime(year, month, 1);

                int day = 0;
                while (true)
                {
                    DateTime date = firstDate.AddDays(day);

                    if (date.Month != month)
                    {
                        break;
                    }

                    Order order1 = new Order();
                    order1.UserId = "chloe";
                    order1.Amount = 10;
                    order1.SetCreateTime(date.AddHours(10));

                    Order order2 = new Order();
                    order2.UserId = "shuxin";
                    order2.Amount = 20;
                    order2.SetCreateTime(date.AddHours(12));

                    orders.Add(order1);
                    orders.Add(order2);

                    day++;
                }
            }

            return orders;
        }
    }
}
