using Chloe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ChloeDemo
{
    abstract class DemoBase : IDisposable
    {
        object _result;
        public DemoBase()
        {
            this.DbContext = this.CreateDbContext();

            DbContextInterceptor dbContextInterceptor = new DbContextInterceptor(1);
            this.DbContext.AddInterceptor(dbContextInterceptor);

            /* context filter，仅对当前 DbContext 对象有效 */
            this.DbContext.HasQueryFilter<Person>(a => a.Id > -100);

            this.OnDbContextCreated();
        }

        /* WARNING: DbContext 是非线程安全的，不能设置为 static，并且用完务必要调用 Dispose 方法销毁对象 */
        public IDbContext DbContext { get; private set; }

        protected abstract IDbContext CreateDbContext();

        protected virtual void OnDbContextCreated()
        {

        }

        public virtual void Dispose()
        {
            this.DbContext.Dispose();
        }

        public virtual void Run()
        {
            this.InitDatabase();
            this.InitData();

            this.Crud();
            this.CrudAsync().GetAwaiter().GetResult();

            this.BasicQuery();
            this.JoinQuery();
            this.ExcludeFieldQuery();
            this.AggregateQuery();
            this.GroupQuery();
            this.ComplexQuery();
            this.QueryWithNavigation();
            this.SplitQuery();
            this.Insert();
            this.Update();
            this.Delete();
            this.Method();
            this.ExecuteCommandText();
            this.DoWithTransaction();
            this.OtherTest();

            ConsoleHelper.WriteLineAndReadKey("Run over...");
        }

        public virtual void InitDatabase()
        {

        }

        public virtual void InitData()
        {
            this.DbContext.Delete<Province>(a => true);
            this.DbContext.Delete<City>(a => true);
            this.DbContext.Delete<Person>(a => true);
            this.DbContext.Delete<PersonProfile>(a => true);
            this.DbContext.Delete<ProfileAnnex>(a => true);

            List<Province> provinces = TestData.GetMockProvinces();
            foreach (var province in provinces)
            {
                this.DbContext.Save(province);
            }

            provinces = this.DbContext.Query<Province>().IncludeAll().ToList();

            //清空旧数据
            this.DbContext.Delete<TestEntity>(a => true);

            List<TestEntity> testEntities = new List<TestEntity>();
            for (int i = 0; i < 1.2 * 10000; i++)
            {
                TestEntity testEntity = new TestEntity()
                {
                    F_Byte = (byte)(i % 10),
                    F_Int16 = (Int16)(16 + i),
                    F_Int32 = 32 + i,
                    F_Int64 = 64 + i,
                    F_Double = 100.01 + i,
                    F_Float = 10.01f + i,
                    F_Decimal = 1000.01M + i,
                    F_Bool = (i % 2) == 0,
                    F_DateTime = DateTime.Now.AddMinutes(i),
                    F_Guid = Guid.NewGuid(),
                    F_String = "Chloe.ORM-" + i,
                    F_Enum = Gender.Male
                };

                testEntities.Add(testEntity);
            }

            this.DbContext.InsertRange(testEntities);

            testEntities = this.DbContext.Query<TestEntity>().ToList();

            ConsoleHelper.WriteLineAndReadKey("InitData over...");
        }


        public virtual void Crud()
        {
            Province province = TestData.CreateProvince("北京", "朝阳区", "海淀区");
            //保存数据到数据库，会将导航属性一起保存
            this.DbContext.Save(province);

            //查询数据并包含关联的导航属性一并查出
            province = this.DbContext.Query<Province>().IncludeAll().Where(a => a.Id == province.Id).First();

            Person person = null;
            var q = this.DbContext.Query<Person>();

            this._result = q.Where(a => a.Id > 0).ToList();
            this._result = q.Count();
            this._result = q.LongCount();
            this._result = q.Max(a => a.Id);
            this._result = q.Min(a => a.Id);
            this._result = q.Sum(a => a.Id);
            this._result = q.Average(a => a.Age);

            person = new Person() { Name = "chloe", Age = 18, Gender = Gender.Female, CityId = 1, CreateTime = DateTime.Now };
            person.Profile = new PersonProfile() { IdNumber = Guid.NewGuid().ToString(), BirthDay = DateTime.Now };

            //插入
            person = this.DbContext.Insert(person);
            person.Profile.Id = person.Id;
            this.DbContext.Insert(person.Profile);

            //lambda 表达式更新
            this._result = this.DbContext.Update<Person>(a => a.Id == person.Id, a => new Person() { Age = a.Age + 1, EditTime = DateTime.Now });

            //查询
            person = this.DbContext.QueryByKey<Person>(person.Id);
            person.Age = person.Age + 1;
            person.EditTime = DateTime.Now;

            //更新
            this._result = this.DbContext.Update(person);

            //删除
            this._result = this.DbContext.Delete<Person>(person);

            //lambda 表达式插入，返回主键
            var insertedId = this.DbContext.Insert(() => new Person() { Name = "chloe", Age = 18, Gender = Gender.Female, CityId = 1, CreateTime = DateTime.Now, RowVersion = 0 });

            //根据主键删除
            this._result = this.DbContext.DeleteByKey<Person>(insertedId);

            ConsoleHelper.WriteLineAndReadKey("Crud over...");
        }
        public virtual async Task CrudAsync()
        {
            Province province = TestData.CreateProvince("上海", "黄浦区", "徐汇区");
            //保存数据到数据库，会将导航属性一起保存
            await this.DbContext.SaveAsync(province);

            //查询数据并包含关联的导航属性一并查出
            province = await this.DbContext.Query<Province>().IncludeAll().Where(a => a.Id == province.Id).FirstAsync();

            Person person = null;
            var q = this.DbContext.Query<Person>();

            this._result = await q.Where(a => a.Id > 0).ToListAsync();
            this._result = await q.CountAsync();
            this._result = await q.LongCountAsync();
            this._result = await q.MaxAsync(a => a.Id);
            this._result = await q.MinAsync(a => a.Id);
            this._result = await q.SumAsync(a => a.Id);
            this._result = await q.AverageAsync(a => a.Age);

            person = new Person() { Name = "Chloe", Age = 18, Gender = Gender.Female, CityId = 1, CreateTime = DateTime.Now };
            person.Profile = new PersonProfile() { IdNumber = Guid.NewGuid().ToString(), BirthDay = DateTime.Now };

            //插入
            person = await this.DbContext.InsertAsync(person);
            person.Profile.Id = person.Id;
            this.DbContext.Insert(person.Profile);

            //lambda 表达式更新
            this._result = await this.DbContext.UpdateAsync<Person>(a => a.Id == person.Id, a => new Person() { Age = a.Age + 1, EditTime = DateTime.Now });

            //查询
            person = await this.DbContext.QueryByKeyAsync<Person>(person.Id);
            person.Age = person.Age + 1;
            person.EditTime = DateTime.Now;

            //更新
            this._result = await this.DbContext.UpdateAsync(person);

            //删除
            this._result = await this.DbContext.DeleteAsync<Person>(person);

            //lambda 表达式插入，返回主键
            var insertedId = await this.DbContext.InsertAsync(() => new Person() { Name = "Chloe", Age = 18, Gender = Gender.Female, CityId = 1, CreateTime = DateTime.Now, RowVersion = 0 });

            //根据主键删除
            this._result = await this.DbContext.DeleteByKeyAsync<Person>(insertedId);

            ConsoleHelper.WriteLineAndReadKey("CrudAsync over...");
        }

        /// <summary>
        /// 基础查询
        /// </summary>
        public virtual void BasicQuery()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            Person person = q.First();

            string name = "Chloe";
            this._result = q.Where(a => a.Name == name).ToList();

            this._result = q.Where(a => a.Id == person.Id).FirstOrDefault();
            /*
             * SELECT [Person].[Id] AS [Id],[Person].[Name] AS [Name],[Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime]
             * FROM [Person] AS [Person] WHERE [Person].[Id] = @P_0 LIMIT 1 OFFSET 0
             */


            //可以选取指定的字段
            this._result = q.Where(a => a.Id == person.Id).Select(a => new { a.Id, a.Name }).FirstOrDefault();
            /*
             * SELECT [Person].[Id] AS [Id],[Person].[Name] AS [Name] FROM [Person] AS [Person] WHERE [Person].[Id] = @P_0 LIMIT 1 OFFSET 0
             */


            //分页
            this._result = q.Where(a => a.Id > 0).OrderBy(a => a.Age).Skip(20).Take(10).ToList();
            /*
             * SELECT [Person].[Id] AS [Id],[Person].[Name] AS [Name],[Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime] FROM [Person] AS [Person] WHERE [Person].[Id] > 0 ORDER BY [Person].[Age] ASC LIMIT 10 OFFSET 20
             */


            /* like 查询 */
            this._result = q.Where(a => a.Name.Contains("Chloe") || a.Name.StartsWith("C") || a.Name.EndsWith("o")).ToList();
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name] 
             * FROM [Person] AS [Person] 
             * WHERE ([Person].[Name] LIKE '%' || 'Chloe' || '%' OR [Person].[Name] LIKE 'C' || '%' OR [Person].[Name] LIKE '%' || 'o')
             */


            /* in 一个数组 */
            List<Person> persons = null;
            List<int> personIds = new List<int>() { 1, 2, 3 };
            persons = q.Where(a => personIds.Contains(a.Id)).ToList(); /* list.Contains() 方法组合就会生成 in一个数组 sql 语句 */
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name]
             * FROM [Person] AS [Person] 
             * WHERE [Person].[Id] IN (1,2,3)
             */


            /* in 子查询 */
            persons = q.Where(a => this.DbContext.Query<City>().Select(c => c.Id).ToList().Contains((int)a.CityId)).ToList(); /* IQuery<T>.ToList().Contains() 方法组合就会生成 in 子查询 sql 语句 */
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name]
             * FROM [Person] AS [Person] 
             * WHERE [Person].[CityId] IN (SELECT [City].[Id] AS [C] FROM [City] AS [City])
             */


            /* distinct 查询 */
            q.Select(a => new { a.Name }).Distinct().ToList();
            /*
             * SELECT DISTINCT [Person].[Name] AS [Name] FROM [Person] AS [Person]
             */

            ConsoleHelper.WriteLineAndReadKey("BasicQuery over...");
        }

        /// <summary>
        /// 连接查询
        /// </summary>
        public virtual void JoinQuery()
        {
            var person_city_province = this.DbContext.Query<Person>()
                                     .InnerJoin<City>((person, city) => person.CityId == city.Id)
                                     .InnerJoin<Province>((person, city, province) => city.ProvinceId == province.Id);

            //查出一个用户及其隶属的城市和省份的所有信息
            var view = person_city_province.Select((person, city, province) => new { Person = person, City = city, Province = province }).Where(a => a.Person.Id > 1).ToList();
            /*
             * SELECT [Person].[Id] AS [Id],[Person].[Name] AS [Name],[Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[City].[Id] AS [Id0],[City].[Name] AS [Name0],[City].[ProvinceId] AS [ProvinceId],[Province].[Id] AS [Id1],[Province].[Name] AS [Name1] FROM [Person] AS [Person] INNER JOIN [City] AS [City] ON [Person].[CityId] = [City].[Id] INNER JOIN [Province] AS [Province] ON [City].[ProvinceId] = [Province].[Id] WHERE [Person].[Id] > 1
             */

            //也可以只获取指定的字段信息：PersonId,PersonName,CityName,ProvinceName
            person_city_province.Select((person, city, province) => new { PersonId = person.Id, PersonName = person.Name, CityName = city.Name, ProvinceName = province.Name }).Where(a => a.PersonId > 1).ToList();
            /*
             * SELECT [Person].[Id] AS [PersonId],[Person].[Name] AS [PersonName],[City].[Name] AS [CityName],[Province].[Name] AS [ProvinceName] FROM [Person] AS [Person] INNER JOIN [City] AS [City] ON [Person].[CityId] = [City].[Id] INNER JOIN [Province] AS [Province] ON [City].[ProvinceId] = [Province].[Id] WHERE [Person].[Id] > 1
             */


            /* quick join and paging. */
            this.DbContext.JoinQuery<Person, City>((person, city) => new object[]
            {
                JoinType.LeftJoin, person.CityId == city.Id
            })
            .Select((person, city) => new { Person = person, City = city })
            .Where(a => a.Person.Id > -1)
            .OrderByDesc(a => a.Person.Age)
            .TakePage(1, 20)
            .ToList();

            this.DbContext.JoinQuery<Person, City, Province>((person, city, province) => new object[]
            {
                JoinType.LeftJoin, person.CityId == city.Id,          /* 表 Person 和 City 进行Left连接 */
                JoinType.LeftJoin, city.ProvinceId == province.Id   /* 表 City 和 Province 进行Left连接 */
            })
            .Select((person, city, province) => new { Person = person, City = city, Province = province })   /* 投影成匿名对象 */
            .Where(a => a.Person.Id > -1)     /* 进行条件过滤 */
            .OrderByDesc(a => a.Person.Age)   /* 排序 */
            .TakePage(1, 20)                /* 分页 */
            .ToList();

            ConsoleHelper.WriteLineAndReadKey("JoinQuery over...");
        }

        /// <summary>
        /// 排除字段查询
        /// </summary>
        public virtual void ExcludeFieldQuery()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            //排除指定的字段
            List<Person> persons = q.Exclude(a => a.Name).ToList();
            /*
             * 生成的 sql 语句中不包含 Name 字段
             * SELECT [Person].[Gender],[Person].[Age],[Person].[CityId],[Person].[CreateTime],[Person].[EditTime],[Person].[Id]
               FROM [Person] AS [Person] WHERE ([Person].[Id] > -100 AND [Person].[Id] > -1)
             */

            foreach (var person in persons)
            {
                Debug.Assert(person.Name == default(string));
            }


            //使用匿名类形式排除多个字段
            persons = q.Exclude(a => new { a.Name, a.Age, a.EditTime, a.CityId, a.CreateTime }).ToList();
            /*
             * SELECT [Person].[Gender],[Person].[Id] FROM [Person] AS [Person] WHERE ([Person].[Id] > -100 AND [Person].[Id] > -1)
             */
            foreach (var person in persons)
            {
                Debug.Assert(person.Name == default(string));
                Debug.Assert(person.Age == default(int?));
                Debug.Assert(person.EditTime == default(DateTime?));
                Debug.Assert(person.CityId == default(int));
                Debug.Assert(person.CreateTime == default(DateTime));
            }


            //使用 new object[] 形式排除多个字段
            persons = q.Exclude(a => new object[] { a.Name, a.Age, a.EditTime, a.CityId, a.CreateTime }).ToList();
            /*
             * SELECT [Person].[Gender],[Person].[Id] FROM [Person] AS [Person] WHERE ([Person].[Id] > -100 AND [Person].[Id] > -1)
             */

            foreach (var person in persons)
            {
                Debug.Assert(person.Name == default(string));
                Debug.Assert(person.Age == default(int?));
                Debug.Assert(person.EditTime == default(DateTime?));
                Debug.Assert(person.CityId == default(int));
                Debug.Assert(person.CreateTime == default(DateTime));
            }


            //在多表连接中排除指定的字段
            var joinResults = q.LeftJoin<City>((person, city) => person.CityId == city.Id).Select((person, city) => new { Person = person, City = city })
                .Exclude(a => new Object[] { a.Person.Name, a.City.Name }).ToList();
            /*
             * SELECT [Person].[Gender],[Person].[Age],[Person].[CityId],[Person].[CreateTime],[Person].[EditTime],[Person].[Id],[City].[ProvinceId],[City].[Id] AS [Id0] 
               FROM [Person] AS [Person] LEFT JOIN [City] AS [City] ON ([Person].[CityId] = [City].[Id] AND [City].[Id] > -2) 
               WHERE ([Person].[Id] > -100 AND [Person].[Id] > -1)
             */

            foreach (var joinResult in joinResults)
            {
                Debug.Assert(joinResult.Person.Name == default(string));
                Debug.Assert(joinResult.City.Name == default(string));
            }


            //在导航属性中排除指定的字段
            persons = q.Exclude(a => a.Name)
                .Include(a => a.City).ExcludeField(a => a.Name)
                .ThenInclude(a => a.Province).ExcludeField(a => a.Name)
                .ToList();
            /*
             * 生成的 sql 语句中不包含 Name 字段
             * SELECT [Person].[Gender],[Person].[Age],[Person].[CityId],[Person].[CreateTime],[Person].[EditTime],[Person].[Id],[City].[ProvinceId],[City].[Id] AS [Id0],[Province].[Id] AS [Id1] 
               FROM [Person] AS [Person] 
               INNER JOIN [City] AS [City] ON ([Person].[CityId] = [City].[Id] AND [City].[Id] > -2) 
               INNER JOIN [Province] AS [Province] ON ([City].[ProvinceId] = [Province].[Id] AND [Province].[Id] > -3) 
               WHERE ([Person].[Id] > -100 AND [Person].[Id] > -1)
             */

            foreach (var person in persons)
            {
                Debug.Assert(person.Name == default(string));
                Debug.Assert(person.City.Name == default(string));
                Debug.Assert(person.City.Province.Name == default(string));
            }


            var cities = this.DbContext.Query<City>().Exclude(a => a.Name)
                  .IncludeMany(a => a.Persons).ExcludeField(a => a.Name)
                  .Include(a => a.Province).ExcludeField(a => a.Name)
                  .ToList();
            /*
             * 生成的 sql 语句中不包含 Name 字段
             * SELECT [City].[ProvinceId],[City].[Id],[Province].[Id] AS [Id0],[Person].[Gender],[Person].[Age],[Person].[CityId],[Person].[CreateTime],[Person].[EditTime],[Person].[Id] AS [Id1] 
               FROM [City] AS [City] 
               INNER JOIN [Province] AS [Province] ON ([City].[ProvinceId] = [Province].[Id] AND [Province].[Id] > -3) 
               LEFT JOIN [Person] AS [Person] ON ([City].[Id] = [Person].[CityId] AND [Person].[Id] > -100 AND [Person].[Id] > -1) 
               WHERE [City].[Id] > -2 ORDER BY [City].[Id] ASC
             */

            foreach (var city in cities)
            {
                Debug.Assert(city.Name == default(string));
                Debug.Assert(city.Province.Name == default(string));

                foreach (var person in city.Persons)
                {
                    Debug.Assert(person.Name == default(string));
                }
            }

            ConsoleHelper.WriteLineAndReadKey("ExcludeFieldQuery over...");
        }

        /// <summary>
        /// 聚合查询
        /// </summary>
        public virtual void AggregateQuery()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            //注：一定要 ToList()
            q.Select(a => Sql.Count()).ToList().First();
            /*
             * SELECT COUNT(1) AS [C] FROM [Person] AS [Person]
             */

            q.Select(a => new { Count = Sql.Count(), LongCount = Sql.LongCount(), Sum = Sql.Sum(a.Age), Max = Sql.Max(a.Age), Min = Sql.Min(a.Age), Average = Sql.Average(a.Age) }).ToList().First();
            /*
             * SELECT COUNT(1) AS [Count],COUNT(1) AS [LongCount],CAST(SUM([Person].[Age]) AS INTEGER) AS [Sum],MAX([Person].[Age]) AS [Max],MIN([Person].[Age]) AS [Min],CAST(AVG([Person].[Age]) AS REAL) AS [Average] FROM [Person] AS [Person]
             */

            var count = q.Count();
            /*
             * SELECT COUNT(1) AS [C] FROM [Person] AS [Person]
             */

            var longCount = q.LongCount();
            /*
             * SELECT COUNT(1) AS [C] FROM [Person] AS [Person]
             */

            var sum = q.Sum(a => a.Age);
            /*
             * SELECT CAST(SUM([Person].[Age]) AS INTEGER) AS [C] FROM [Person] AS [Person]
             */

            var max = q.Max(a => a.Age);
            /*
             * SELECT MAX([Person].[Age]) AS [C] FROM [Person] AS [Person]
             */

            var min = q.Min(a => a.Age);
            /*
             * SELECT MIN([Person].[Age]) AS [C] FROM [Person] AS [Person]
             */

            var avg = q.Average(a => a.Age);
            /*
             * SELECT CAST(AVG([Person].[Age]) AS REAL) AS [C] FROM [Person] AS [Person]
             */

            ConsoleHelper.WriteLineAndReadKey("AggregateQuery over...");
        }

        /// <summary>
        /// 分组查询
        /// </summary>
        public virtual void GroupQuery()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            //用法1
            IGroupingQuery<Person> g = q.Where(a => a.Id > 0).GroupBy(a => a.Age);
            // g = g.AndBy(a => a.Id);  //多个字段分组

            g = g.Having(a => a.Age > 1 && Sql.Count() > 0);

            g.Select(a => new { a.Age, Count = Sql.Count(), Sum = Sql.Sum(a.Age), Max = Sql.Max(a.Age), Min = Sql.Min(a.Age), Avg = Sql.Average(a.Age) }).ToList();
            /*
             * SELECT [Person].[Age] AS [Age],COUNT(1) AS [Count],CAST(SUM([Person].[Age]) AS INTEGER) AS [Sum],CAST(MAX([Person].[Age]) AS INTEGER) AS [Max],CAST(MIN([Person].[Age]) AS INTEGER) AS [Min],CAST(AVG([Person].[Age]) AS REAL) AS [Avg] 
             * FROM [Person] AS [Person] WHERE [Person].[Id] > 0 
             * GROUP BY [Person].[Age] HAVING ([Person].[Age] > 1 AND COUNT(1) > 0)
             */


            //用法2
            g = q.Where(a => a.Id > 0).GroupBy(a => new { a.Age, a.Id });
            g = g.Having(a => a.Age > 1 && Sql.Count() > 0);
            g.Select(a => new { a.Age, Count = Sql.Count(), Sum = Sql.Sum(a.Age), Max = Sql.Max(a.Age), Min = Sql.Min(a.Age), Avg = Sql.Average(a.Age) }).ToList();
            /*
             * SELECT [Person].[Age] AS [Age],COUNT(1) AS [Count],CAST(SUM([Person].[Age]) AS INTEGER) AS [Sum],MAX([Person].[Age]) AS [Max],MIN([Person].[Age]) AS [Min],CAST(AVG([Person].[Age]) AS REAL) AS [Avg] 
             * FROM [Person] AS [Person] WHERE [Person].[Id] > 0
             * GROUP BY [Person].[Age],[Person].[Id] HAVING ([Person].[Age] > 1 AND COUNT(1) > 0)
             */

            ConsoleHelper.WriteLineAndReadKey("GroupQuery over...");
        }

        /// <summary>
        /// 复杂查询
        /// </summary>
        public virtual void ComplexQuery()
        {
            /*
             * 支持 select * from Person where CityId in (1,2,3)    --in一个数组
             * 支持 select * from Person where CityId in (select Id from City)    --in子查询
             * 支持 select * from Person exists (select 1 from City where City.Id=Person.CityId)    --exists查询
             * 支持 select (select top 1 CityName from City where Person.CityId==City.Id) as CityName, Person.Id, Person.Name from Person    --select子查询
             * 支持 select 
             *            (select count(*) from Person where Person.CityId=City.Id) as PersonCount,     --总数
             *            (select max(Person.Age) from Person where Person.CityId=City.Id) as MaxAge,  --最大年龄
             *            (select avg(Person.Age) from Person where Person.CityId=City.Id) as AvgAge   --平均年龄
             *      from City
             *      --统计查询
             */

            IQuery<Person> personQuery = this.DbContext.Query<Person>();
            IQuery<City> cityQuery = this.DbContext.Query<City>();

            List<Person> persons = null;

            /* in 一个数组 */
            List<int> personIds = new List<int>() { 1, 2, 3 };
            persons = personQuery.Where(a => personIds.Contains(a.Id)).ToList();  /* list.Contains() 方法组合就会生成 in一个数组 sql 语句 */
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name] 
             * FROM [Person] AS [Person] 
             * WHERE [Person].[Id] IN (1,2,3)
             */


            /* in 子查询 */
            persons = personQuery.Where(a => cityQuery.Select(c => c.Id).ToList().Contains((int)a.CityId)).ToList();  /* IQuery<T>.ToList().Contains() 方法组合就会生成 in 子查询 sql 语句 */
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name] 
             * FROM [Person] AS [Person] 
             * WHERE [Person].[CityId] IN (SELECT [City].[Id] AS [C] FROM [City] AS [City])
             */


            /* IQuery<T>.Any() 方法组合就会生成 exists 子查询 sql 语句 */
            persons = personQuery.Where(a => cityQuery.Where(c => c.Id == a.CityId).Any()).ToList();
            /*
             * SELECT 
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name] 
             * FROM [Person] AS [Person] 
             * WHERE Exists (SELECT '1' AS [C] FROM [City] AS [City] WHERE [City].[Id] = [Person].[CityId])
             */


            /* select 子查询 */
            var result = personQuery.Select(a => new
            {
                CityName = cityQuery.Where(c => c.Id == a.CityId).First().Name,
                Person = a
            }).ToList();
            /*
             * SELECT 
             *      (SELECT [City].[Name] AS [C] FROM [City] AS [City] WHERE [City].[Id] = [Person].[CityId] LIMIT 1 OFFSET 0) AS [CityName],
             *      [Person].[Gender] AS [Gender],[Person].[Age] AS [Age],[Person].[CityId] AS [CityId],[Person].[EditTime] AS [EditTime],[Person].[Id] AS [Id],[Person].[Name] AS [Name] 
             * FROM [Person] AS [Person]
             */


            /* 统计 */
            var statisticsResult = cityQuery.Select(a => new
            {
                PersonCount = personQuery.Where(u => u.CityId == a.Id).Count(),
                MaxAge = personQuery.Where(u => u.CityId == a.Id).Max(c => c.Age),
                AvgAge = personQuery.Where(u => u.CityId == a.Id).Average(c => c.Age),
            }).ToList();
            /*
             * SELECT 
             *      (SELECT COUNT(1) AS [C] FROM [Person] AS [Person] WHERE [Person].[CityId] = [City].[Id]) AS [PersonCount],
             *      (SELECT MAX([Person].[Age]) AS [C] FROM [Person] AS [Person] WHERE [Person].[CityId] = [City].[Id]) AS [MaxAge],
             *      (SELECT CAST(AVG([Person].[Age]) AS REAL) AS [C] FROM [Person] AS [Person] WHERE [Person].[CityId] = [City].[Id]) AS [AvgAge] 
             * FROM [City] AS [City]
             */

            ConsoleHelper.WriteLineAndReadKey("ComplexQuery over...");
        }

        /// <summary>
        /// 导航属性查询
        /// </summary>
        public virtual void QueryWithNavigation()
        {
            object result = null;
            result = this.DbContext.Query<Person>().Include(a => a.City).ThenInclude(a => a.Province).ToList();

            //忽略所有过滤器
            result = this.DbContext.Query<Person>().IgnoreAllFilters().Include(a => a.City).ThenInclude(a => a.Province).ToList();

            List<City> cities = this.DbContext.Query<City>().Include(a => a.Province).IncludeMany(a => a.Persons).Filter(a => a.Age >= 18).ToList();

            cities.ForEach(city =>
            {
                city.Persons.ForEach(person =>
                {
                    Debug.Assert(person.Age >= 18);
                    Debug.Assert(person.City == null);
                });
            });

            List<Province> provinces = this.DbContext.Query<Province>().IncludeMany(a => a.Cities).ThenIncludeMany(a => a.Persons).ToList();

            provinces.ForEach(province =>
            {
                province.Cities.ForEach(city =>
                {
                    Debug.Assert(city.Province == null);

                    city.Persons.ForEach(person =>
                    {
                        Debug.Assert(person.City == null);
                    });
                });
            });

            provinces = this.DbContext.Query<Province>().IncludeMany(a => a.Cities).ThenIncludeMany(a => a.Persons).BindTwoWay().ToList(); //调用 BindTwoWay() 方法，子对象会将父对象设置到子对象对应的属性上

            provinces.ForEach(province =>
            {
                province.Cities.ForEach(city =>
                {
                    Debug.Assert(city.Province != null);

                    city.Persons.ForEach(person =>
                    {
                        Debug.Assert(person.City != null);
                    });
                });
            });

            //分页
            result = this.DbContext.Query<Province>().IncludeMany(a => a.Cities).ThenIncludeMany(a => a.Persons).Where(a => a.Id > 0).TakePage(1, 20).ToList();

            result = this.DbContext.Query<City>().IncludeMany(a => a.Persons).Filter(a => a.Age > 18).ToList();

            //未 Include a.City，但依然可以在表达式树中使用 a.City 相关的属性
            List<Person> persons = this.DbContext.Query<Person>().Where(a => a.City.Name == "广州").ToList();
            /*生成的 sql 中自动加上相关的 join 子句
             SELECT `T`.`Name`,`T`.`Gender`,`T`.`Age`,`T`.`CityId`,`T`.`CreateTime`,`T`.`EditTime`,`T`.`RowVersion`,`T`.`Id` FROM `Person` AS `T` INNER JOIN `City` AS `T0` ON `T`.`CityId` = `T0`.`Id` WHERE `T0`.`Name` = N'广州'
             */

            foreach (Person person in persons)
            {
                Debug.Assert(person.City == null);
            }

            ConsoleHelper.WriteLineAndReadKey("QueryWithNavigation over...");
        }

        /// <summary>
        /// 导航属性+拆分查询
        /// </summary>
        public virtual void SplitQuery()
        {
            IDbContext dbContext = this.DbContext;

            List<int> ids = new List<int>() { 1, 2 };

            var personQuery = dbContext.Query<Person>();
            var cityQuery = dbContext.Query<City>();
            var provinceQuery = dbContext.Query<Province>();

            //只会生成一条包含多个 left join 的 sql 语句
            List<Province> provinces1 = provinceQuery.IncludeAll().OrderBy(a => a.Id).ToList();
            /*
            SELECT `T`.`Id`,`T`.`Name`,`T0`.`Id` AS `Id0`,`T0`.`Name` AS `Name0`,`T0`.`ProvinceId`,`T1`.`Name` AS `Name1`,`T1`.`Gender`,`T1`.`Age`,`T1`.`CityId`,`T1`.`CreateTime`,`T1`.`EditTime`,`T1`.`RowVersion`,`T1`.`Id` AS `Id1`,`T2`.`Id` AS `Id2`,`T2`.`IdNumber`,`T2`.`BirthDay`,`T3`.`Id` AS `Id3`,`T3`.`ProfileId`,`T3`.`FilePath` 
            FROM `Province` AS `T` 
            LEFT JOIN `City` AS `T0` ON (`T`.`Id` = `T0`.`ProvinceId` AND `T0`.`Id` > -2) 
            LEFT JOIN `Person` AS `T1` ON (`T0`.`Id` = `T1`.`CityId` AND `T1`.`Id` > -100 AND `T1`.`Id` > -1) 
            INNER JOIN `PersonProfile` AS `T2` ON (`T1`.`Id` = `T2`.`Id` AND `T2`.`Id` > -1) 
            LEFT JOIN `ProfileAnnex` AS `T3` ON `T2`.`Id` = `T3`.`ProfileId` 
            WHERE `T`.`Id` > -3 ORDER BY `T`.`Id` ASC,`T0`.`Id` ASC,`T2`.`Id` ASC
            */

            //调用 SplitQuery 后会拆分成多条 sql 语句查询
            List<Province> provinces2 = provinceQuery.IncludeAll().SplitQuery().OrderBy(a => a.Id).ToList();
            /*少量数据则会使用 in 查询
            SELECT `T`.`Id`,`T`.`Name` FROM `Province` AS `T` WHERE `T`.`Id` > -3 ORDER BY `T`.`Id` ASC;

            SELECT `T`.`Id`,`T`.`Name`,`T`.`ProvinceId` FROM `City` AS `T` WHERE (`T`.`ProvinceId` IN (1,2,3,4,5) AND `T`.`Id` > -2);

            SELECT `T`.`Name`,`T`.`Gender`,`T`.`Age`,`T`.`CityId`,`T`.`CreateTime`,`T`.`EditTime`,`T`.`RowVersion`,`T`.`Id`,`T0`.`Id` AS `Id0`,`T0`.`IdNumber`,`T0`.`BirthDay` 
            FROM `Person` AS `T` 
            INNER JOIN `PersonProfile` AS `T0` ON (`T`.`Id` = `T0`.`Id` AND `T0`.`Id` > -1) 
            WHERE (`T`.`CityId` IN (1,2,3,4,5,6,7,8,9,10,11,12,13,14) AND `T`.`Id` > -1);

            SELECT `T`.`Id`,`T`.`ProfileId`,`T`.`FilePath` FROM `ProfileAnnex` AS `T` WHERE `T`.`ProfileId` IN (1,2,3,4,5,6,7,8,9,10,...);
            */

            /*如果查询的数据数量过多，会生成多表 inner join 查询，如下：
            SELECT `T`.`Id`,`T`.`Name` FROM `Province` AS `T` WHERE `T`.`Id` > -3 ORDER BY `T`.`Id` ASC;

            SELECT `T0`.`Id`,`T0`.`Name`,`T0`.`ProvinceId` FROM `Province` AS `T` INNER JOIN `City` AS `T0` ON (`T`.`Id` = `T0`.`ProvinceId` AND `T0`.`Id` > -2) WHERE `T`.`Id` > -3;

            SELECT `T1`.`Name`,`T1`.`Gender`,`T1`.`Age`,`T1`.`CityId`,`T1`.`CreateTime`,`T1`.`EditTime`,`T1`.`RowVersion`,`T1`.`Id`,`T2`.`Id` AS `Id0`,`T2`.`IdNumber`,`T2`.`BirthDay` 
            FROM `Province` AS `T` 
            INNER JOIN `City` AS `T0` ON (`T`.`Id` = `T0`.`ProvinceId` AND `T0`.`Id` > -2) 
            INNER JOIN `Person` AS `T1` ON (`T0`.`Id` = `T1`.`CityId` AND `T1`.`Id` > -100 AND `T1`.`Id` > -1) 
            INNER JOIN `PersonProfile` AS `T2` ON (`T1`.`Id` = `T2`.`Id` AND `T2`.`Id` > -1) 
            WHERE `T`.`Id` > -3;

            SELECT `T3`.`Id`,`T3`.`ProfileId`,`T3`.`FilePath` 
            FROM `Province` AS `T` 
            INNER JOIN `City` AS `T0` ON (`T`.`Id` = `T0`.`ProvinceId` AND `T0`.`Id` > -2) 
            INNER JOIN `Person` AS `T1` ON (`T0`.`Id` = `T1`.`CityId` AND `T1`.`Id` > -100 AND `T1`.`Id` > -1) 
            INNER JOIN `PersonProfile` AS `T2` ON (`T1`.`Id` = `T2`.`Id` AND `T2`.`Id` > -1) 
            INNER JOIN `ProfileAnnex` AS `T3` ON `T2`.`Id` = `T3`.`ProfileId` 
            WHERE `T`.`Id` > -3;
            */

            Debug.Assert(provinces2.Count == provinces1.Count);

            for (int i = 0; i < provinces2.Count; i++)
            {
                var province2 = provinces2[i];
                var province1 = provinces1[i];

                Debug.Assert(province2.Id == province1.Id);

                province2.Cities = province2.Cities.OrderBy(a => a.Id).ToList();
                province1.Cities = province1.Cities.OrderBy(a => a.Id).ToList();

                Debug.Assert(province2.Cities.Count == province1.Cities.Count);

                for (int j = 0; j < province2.Cities.Count; j++)
                {
                    var city2 = province2.Cities[j];
                    var city1 = province1.Cities[j];

                    Debug.Assert(city2.Id == city1.Id);

                    city2.Persons = city2.Persons.OrderBy(a => a.Id).ToList();
                    city1.Persons = city1.Persons.OrderBy(a => a.Id).ToList();

                    Debug.Assert(city2.Persons.Count == city1.Persons.Count);

                    for (int k = 0; k < city2.Persons.Count; k++)
                    {
                        var person2 = city2.Persons[k];
                        var person1 = city1.Persons[k];

                        Debug.Assert(person2.Id == person1.Id);
                        Debug.Assert(person2.Profile.Id == person1.Profile.Id);

                        Debug.Assert(person2.Profile.Annexes.Count == person1.Profile.Annexes.Count);

                        person2.Profile.Annexes = person2.Profile.Annexes.OrderBy(a => a.Id).ToList();
                        person1.Profile.Annexes = person2.Profile.Annexes.OrderBy(a => a.Id).ToList();

                        for (int i1 = 0; i1 < person2.Profile.Annexes.Count; i1++)
                        {
                            Debug.Assert(person2.Profile.Annexes[i1].Id == person1.Profile.Annexes[i1].Id);
                        }
                    }
                }
            }
            //city.Province, city.Persons
            var cityQuery1 = cityQuery.Include(a => a.Province).IncludeMany(a => a.Persons).OrderBy(a => a.Id);

            List<City> cities1 = cityQuery1.ToList();
            List<City> cities2 = cityQuery1.SplitQuery().BindTwoWay().ToList();
            List<City> cities3 = cityQuery1.SplitQuery().BindTwoWay().IgnoreAllFilters().AsTracking().ToList();

            for (int i = 0; i < cities1.Count; i++)
            {
                var city1 = cities1[i];
                var city2 = cities2[i];
                var city3 = cities3[i];

                city1.Persons = city1.Persons.OrderBy(a => a.Id).ToList();
                city2.Persons = city2.Persons.OrderBy(a => a.Id).ToList();
                city3.Persons = city3.Persons.OrderBy(a => a.Id).ToList();

                Debug.Assert(city1.Id == city2.Id);
                Debug.Assert(city1.Id == city2.Id);

                Debug.Assert(city1.Province.Id == city2.Province.Id);
                Debug.Assert(city1.Province.Id == city3.Province.Id);

                Debug.Assert(city1.Persons.Count == city2.Persons.Count);
                Debug.Assert(city1.Persons.Count == city3.Persons.Count);

                for (int j = 0; j < city1.Persons.Count; j++)
                {
                    Person person1 = city1.Persons[j];
                    Person person2 = city2.Persons[j];
                    Person person3 = city3.Persons[j];

                    Debug.Assert(person1.Id == person2.Id);
                    Debug.Assert(person1.Id == person3.Id);

                    Debug.Assert(person1.City == null);
                    Debug.Assert(person2.City != null);  //因为 person2 和 person3 查询使用了 BindTwoWay()，所以会反向赋值
                    Debug.Assert(person3.City != null);

                    Debug.Assert(person2.City.Id == person3.City.Id);
                }
            }

            //1:1:1
            var personQuery1 = personQuery.Include(a => a.City).ThenInclude(a => a.Province).OrderBy(a => a.Id);
            List<Person> persons1 = personQuery1.ToList();
            List<Person> persons2 = personQuery1.SplitQuery().ToList();
            List<Person> persons3 = personQuery1.SplitQuery().IgnoreAllFilters().AsTracking().ToList();

            Debug.Assert(persons1.Count == persons2.Count);
            Debug.Assert(persons1.Count == persons3.Count);

            for (int i = 0; i < persons1.Count; i++)
            {
                Person person1 = persons1[i];
                Person person2 = persons2[i];
                Person person3 = persons3[i];

                Debug.Assert(person1.Id == person2.Id);
                Debug.Assert(person1.Id == person3.Id);

                Debug.Assert(person1.City.Id == person2.City.Id);
                Debug.Assert(person1.City.Id == person3.City.Id);

                Debug.Assert(person1.City.Province.Id == person2.City.Province.Id);
                Debug.Assert(person1.City.Province.Id == person3.City.Province.Id);
            }

            //排除字段测试
            var cityQueryWithExcludeField = cityQuery.Exclude(a => a.Name)
                .Include(a => a.Province).ExcludeField(a => a.Name)
                .IncludeMany(a => a.Persons).ExcludeField(a => new { a.Age, a.Name })
                .OrderBy(a => a.Id).SplitQuery();

            var cities4 = cityQueryWithExcludeField.ToList();
            for (int i = 0; i < cities4.Count; i++)
            {
                var city = cities4[i];
                Debug.Assert(city.Name == null);
                Debug.Assert(city.Province.Name == null);
                Debug.Assert(city.Persons.Count > 0);
                foreach (var person in city.Persons)
                {
                    Debug.Assert(person.Age == null);
                    Debug.Assert(person.Name == null);
                }
            }

            //分页+拆分查询测试

            var pageQuery = cityQuery.IncludeAll().Where(a => a.Id > -9999).OrderBy(a => a.Id).TakePage(1, 2);

            //未使用拆分查询的查询结果
            var citiesWithoutSplitQuery = pageQuery.ToList();

            //使用拆分查询的查询结果
            var cityCount = pageQuery.SplitQuery().Count();
            var citiesWithSplitQuery = pageQuery.SplitQuery().ToList();

            Debug.Assert(citiesWithSplitQuery.Count > 0);
            Debug.Assert(citiesWithSplitQuery.Count == citiesWithoutSplitQuery.Count);
            Debug.Assert(cityCount == citiesWithSplitQuery.Count);

            for (int i = 0; i < citiesWithSplitQuery.Count; i++)
            {
                var city1 = citiesWithSplitQuery[i];
                var city2 = citiesWithoutSplitQuery[i];
                city1.Persons = city1.Persons.OrderBy(a => a.Id).ToList();
                city2.Persons = city2.Persons.OrderBy(a => a.Id).ToList();

                Debug.Assert(city1.Id == city2.Id);
                Debug.Assert(city1.Id == city2.Id);

                Debug.Assert(city1.Province.Id == city2.Province.Id);

                Debug.Assert(city1.Persons.Count == city2.Persons.Count);

                for (int j = 0; j < city1.Persons.Count; j++)
                {
                    Person person1 = city1.Persons[j];
                    Person person2 = city2.Persons[j];

                    Debug.Assert(person1.Id == person2.Id);
                }
            }

            ConsoleHelper.WriteLineAndReadKey("SplitQuery over...");
        }

        public virtual void Insert()
        {
            //lambda 插入
            //返回主键 Id
            int id = (int)this.DbContext.Insert<Person>(() => new Person() { Name = "Chloe", Age = 18, Gender = Gender.Female, CityId = 1, CreateTime = DateTime.Now, RowVersion = 0 });
            /*
             * INSERT INTO [Person]([Name],[Age],[Gender],[CityId],[CreateTime]) VALUES('Chloe',18,2,1,DATETIME('NOW','LOCALTIME'));SELECT LAST_INSERT_ROWID()
             */

            string idNumber = Guid.NewGuid().ToString();
            this.DbContext.Insert<PersonProfile>(() => new PersonProfile() { Id = id, IdNumber = idNumber, BirthDay = DateTime.Now });

            //实体插入
            Person person = new Person();
            person.Name = "Chloe";
            person.Age = 18;
            person.Gender = Gender.Female;
            person.CityId = 1;
            person.CreateTime = DateTime.Now;
            person.Profile = new PersonProfile() { IdNumber = Guid.NewGuid().ToString(), BirthDay = DateTime.Now };

            //会自动将自增 Id 设置到 person 的 Id 属性上
            person = this.DbContext.Insert(person);
            person.Profile.Id = person.Id;
            this.DbContext.Insert(person.Profile);
            /*
             * String @P_0 = 'Chloe';
               Gender @P_1 = Female;
               Int32 @P_2 = 18;
               Int32 @P_3 = 1;
               DateTime @P_4 = '2016/8/6 22:03:42';
               INSERT INTO [Person]([Name],[Gender],[Age],[CityId],[CreateTime]) VALUES(@P_0,@P_1,@P_2,@P_3,@P_4);SELECT LAST_INSERT_ROWID()
             */


            //实体插入
            person = new Person();
            person.Name = "";
            person.Age = 18;
            person.Gender = Gender.Female;
            person.CityId = 1;
            person.CreateTime = DateTime.Now;
            person.EditTime = null;
            person.Profile = new PersonProfile() { IdNumber = Guid.NewGuid().ToString(), BirthDay = DateTime.Now };

            //设置属性值为 null 和字符串空值不参与插入
            (this.DbContext as DbContext).Options.InsertStrategy = InsertStrategy.IgnoreNull | InsertStrategy.IgnoreEmptyString;

            //插入时 Name 和 EditTime 属性不会生成到 sql 语句中
            person = this.DbContext.Insert(person);
            person.Profile.Id = person.Id;
            this.DbContext.Insert(person.Profile);
            /*
             * Input Int32 @P_0 = 2;
               Input Int32 @P_1 = 18;
               Input Int32 @P_2 = 1;
               Input DateTime @P_3 = '2023/12/7 1:09:08';
               Input Int32 @P_4 = 0;
               INSERT INTO [Person]([Gender],[Age],[CityId],[CreateTime],[RowVersion]) VALUES(@P_0,@P_1,@P_2,@P_3,@P_4);SELECT LAST_INSERT_ROWID()
             */

            person = this.DbContext.Query<Person>().Where(a => a.Id == person.Id).First();
            Debug.Assert(person.Name == null && person.EditTime == null);

            ConsoleHelper.WriteLineAndReadKey("Insert over...");
        }
        public virtual void Update()
        {
            Person person = this.DbContext.Query<Person>().First();

            //lambda 更新
            this.DbContext.Update<Person>(a => a.Id == person.Id, a => new Person() { Name = a.Name, Age = a.Age + 1, Gender = Gender.Female, EditTime = DateTime.Now });
            /*
             * UPDATE [Person] SET [Name]=[Person].[Name],[Age]=([Person].[Age] + 1),[Gender]=1,[EditTime]=DATETIME('NOW','LOCALTIME') WHERE [Person].[Id] = 1
             */

            //批量更新
            //给所有女性年轻 1 岁
            this.DbContext.Update<Person>(a => a.Gender == Gender.Female, a => new Person() { Age = a.Age - 1, EditTime = DateTime.Now });
            /*
             * UPDATE [Person] SET [Age]=([Person].[Age] - 1),[EditTime]=DATETIME('NOW','LOCALTIME') WHERE [Person].[Gender] = 2
             */

            //复杂条件以及复杂 set 更新
            this.DbContext.Update<Person>(a => a.Id == person.Id && a.Id == this.DbContext.Query<City>().IgnoreAllFilters().Where(city => city.Id == a.Id).First().Id, a => new Person()
            {
                Name = this.DbContext.Query<City>().IgnoreAllFilters().Where(city => city.Id == a.Id).First().Name
            });
            /*
             * UPDATE [Person] 
               SET [Name]=(SELECT [City].[Name] AS [C] FROM [City] AS [City] WHERE [City].[Id] = [Person].[Id] LIMIT 1 OFFSET 0) 
               WHERE ([Person].[Id] = 1 AND [Person].[Id] = (SELECT [City].[Id] AS [C] FROM [City] AS [City] WHERE [City].[Id] = [Person].[Id] LIMIT 1 OFFSET 0))
             */

            //实体更新
            person = this.DbContext.Query<Person>().First();
            person.Name = "Chloe";
            person.Age = 28;
            person.Gender = Gender.Female;
            person.EditTime = DateTime.Now;

            this.DbContext.Update(person); //会更新所有映射的字段
            /*
             * Input String @P_0 = 'Chloe';
               Input Int32 @P_1 = 2;
               Input Int32 @P_2 = 28;
               Input Int32 @P_3 = 1;
               Input DateTime @P_4 = '2023/11/10 23:27:48';
               Input Int32 @P_5 = 0;
               UPDATE [Person] SET [Name]=@P_0,[Gender]=@P_1,[Age]=@P_2,[CityId]=@P_3,[EditTime]=@P_4,[RowVersion]=@P_3 WHERE ([Person].[Id] = @P_3 AND [Person].[RowVersion] = @P_5)
             */


            /*
             * 支持只更新属性值已变的属性
             */

            this.DbContext.TrackEntity(person);//在上下文中跟踪实体
            person.Name = person.Name + "1";
            this.DbContext.Update(person);//这时只会更新被修改的字段
            /*
             * Input String @P_0 = 'Chloe1';
               Input Int32 @P_1 = 2;
               Input Int32 @P_2 = 1;
               UPDATE [Person] SET [Name]=@P_0,[RowVersion]=@P_1 WHERE ([Person].[Id] = @P_2 AND [Person].[RowVersion] = @P_2)
             */

            ConsoleHelper.WriteLineAndReadKey("Update over...");
        }
        public virtual void Delete()
        {
            Person person = this.DbContext.Query<Person>().First();

            this.DbContext.Delete<Person>(a => a.Id == person.Id);
            /*
             * DELETE FROM [Person] WHERE [Person].[Id] = 1
             */

            //批量删除
            //根据条件删除
            this.DbContext.Delete<Person>(a => a.Id < 0);
            /*
             * DELETE FROM [Person] WHERE [Person].[Id] < 0
             */


            //复杂条件删除
            this.DbContext.Delete<Person>(a => !this.DbContext.Query<City>().IgnoreAllFilters().Select(a => a.Id).ToList().Contains((int)a.CityId));
            /*
             * DELETE FROM [Person] WHERE NOT ([Person].[CityId] IN (SELECT [City].[Id] AS [C] FROM [City] AS [City]))
             */

            //实体删除
            person = this.DbContext.Query<Person>().First();
            this.DbContext.Delete(person);
            /*
             * Input Int32 @P_0 = 2;
               Input Int32 @P_1 = 0;
               DELETE FROM [Person] WHERE ([Person].[Id] = @P_0 AND [Person].[RowVersion] = @P_1)
             */

            ConsoleHelper.WriteLineAndReadKey("Delete over...");
        }

        /// <summary>
        /// 常用方法使用
        /// </summary>
        public virtual void Method()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            var space = new char[] { ' ' };

            DateTime startTime = DateTime.Now;
            DateTime endTime = DateTime.Now.AddDays(1);

            var ret = q.Select(a => new
            {
                Id = a.Id,

                String_Length = (int?)a.Name.Length,//LENGTH([Person].[Name])
                Substring = a.Name.Substring(0),//SUBSTR([Person].[Name],0 + 1)
                Substring1 = a.Name.Substring(1),//SUBSTR([Person].[Name],1 + 1)
                Substring1_2 = a.Name.Substring(1, 2),//SUBSTR([Person].[Name],1 + 1,2)
                ToLower = a.Name.ToLower(),//LOWER([Person].[Name])
                ToUpper = a.Name.ToUpper(),//UPPER([Person].[Name])
                IsNullOrEmpty = string.IsNullOrEmpty(a.Name),//CASE WHEN ([Person].[Name] IS NULL OR [Person].[Name] = '') THEN 1 ELSE 0 END = 1
                Contains = (bool?)a.Name.Contains("s"),//[Person].[Name] LIKE '%' || 's' || '%'
                StartsWith = (bool?)a.Name.StartsWith("s"),//[Person].[Name] LIKE 's' || '%'
                EndsWith = (bool?)a.Name.EndsWith("s"),//[Person].[Name] LIKE '%' || 's'
                Trim = a.Name.Trim(),//TRIM([Person].[Name])
                TrimStart = a.Name.TrimStart(space),//LTRIM([Person].[Name])
                TrimEnd = a.Name.TrimEnd(space),//RTRIM([Person].[Name])
                Replace = a.Name.Replace("l", "L"),

                DiffYears = Sql.DiffYears(startTime, endTime),//(CAST(STRFTIME('%Y',@P_0) AS INTEGER) - CAST(STRFTIME('%Y',@P_1) AS INTEGER))
                DiffMonths = Sql.DiffMonths(startTime, endTime),//((CAST(STRFTIME('%Y',@P_0) AS INTEGER) - CAST(STRFTIME('%Y',@P_1) AS INTEGER)) * 12 + (CAST(STRFTIME('%m',@P_0) AS INTEGER) - CAST(STRFTIME('%m',@P_1) AS INTEGER)))
                DiffDays = Sql.DiffDays(startTime, endTime),//CAST((JULIANDAY(@P_0) - JULIANDAY(@P_1)) AS INTEGER)
                DiffHours = Sql.DiffHours(startTime, endTime),//CAST((JULIANDAY(@P_0) - JULIANDAY(@P_1)) * 24 AS INTEGER)
                DiffMinutes = Sql.DiffMinutes(startTime, endTime),//CAST((JULIANDAY(@P_0) - JULIANDAY(@P_1)) * 1440 AS INTEGER)
                DiffSeconds = Sql.DiffSeconds(startTime, endTime),//CAST((JULIANDAY(@P_0) - JULIANDAY(@P_1)) * 86400 AS INTEGER)
                                                                  //DiffMilliseconds = Sql.DiffMilliseconds(startTime, endTime),//不支持 Millisecond
                                                                  //DiffMicroseconds = Sql.DiffMicroseconds(startTime, endTime),//不支持 Microseconds

                AddYears = startTime.AddYears(1),//DATETIME(@P_0,'+' || 1 || ' years')
                AddMonths = startTime.AddMonths(1),//DATETIME(@P_0,'+' || 1 || ' months')
                AddDays = startTime.AddDays(1),//DATETIME(@P_0,'+' || 1 || ' days')
                AddHours = startTime.AddHours(1),//DATETIME(@P_0,'+' || 1 || ' hours')
                AddMinutes = startTime.AddMinutes(2),//DATETIME(@P_0,'+' || 2 || ' minutes')
                AddSeconds = startTime.AddSeconds(120),//DATETIME(@P_0,'+' || 120 || ' seconds')
                                                       //AddMilliseconds = startTime.AddMilliseconds(2000),//不支持

                Now = DateTime.Now,//DATETIME('NOW','LOCALTIME')
                UtcNow = DateTime.UtcNow,//DATETIME()
                Today = DateTime.Today,//DATE('NOW','LOCALTIME')
                Date = DateTime.Now.Date,//DATE('NOW','LOCALTIME')
                Year = DateTime.Now.Year,//CAST(STRFTIME('%Y',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Month = DateTime.Now.Month,//CAST(STRFTIME('%m',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Day = DateTime.Now.Day,//CAST(STRFTIME('%d',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Hour = DateTime.Now.Hour,//CAST(STRFTIME('%H',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Minute = DateTime.Now.Minute,//CAST(STRFTIME('%M',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Second = DateTime.Now.Second,//CAST(STRFTIME('%S',DATETIME('NOW','LOCALTIME')) AS INTEGER)
                Millisecond = DateTime.Now.Millisecond,//@P_2 直接计算 DateTime.Now.Millisecond 的值 
                DayOfWeek = DateTime.Now.DayOfWeek,//CAST(STRFTIME('%w',DATETIME('NOW','LOCALTIME')) AS INTEGER)

                Byte_Parse = byte.Parse("1"),//CAST('1' AS INTEGER)
                Int_Parse = int.Parse("1"),//CAST('1' AS INTEGER)
                Int16_Parse = Int16.Parse("11"),//CAST('11' AS INTEGER)
                Long_Parse = long.Parse("2"),//CAST('2' AS INTEGER)
                Double_Parse = double.Parse("3.1"),//CAST('3.1' AS REAL)
                Float_Parse = float.Parse("4.1"),//CAST('4.1' AS REAL)
                                                 //Decimal_Parse = decimal.Parse("5"),//不支持
                                                 //Guid_Parse = Guid.Parse("D544BC4C-739E-4CD3-A3D3-7BF803FCE179"),//不支持 'D544BC4C-739E-4CD3-A3D3-7BF803FCE179'

                Bool_Parse = bool.Parse("1"),//CAST('1' AS INTEGER)
                DateTime_Parse = DateTime.Parse("2014-01-01"),//DATETIME('2014-01-01')
                CaseWhen = Case.When(a.Id > 100).Then(1).Else(0) //case when
            }).ToList();

            ConsoleHelper.WriteLineAndReadKey("Method over...");
        }

        /// <summary>
        /// 执行原生sql
        /// </summary>
        public virtual void ExecuteCommandText()
        {
            List<Person> persons = this.DbContext.SqlQuery<Person>("select * from Person where Age > @age", DbParam.Create("@age", 1)).ToList();

            int rowsAffected = this.DbContext.Session.ExecuteNonQuery("update Person set name=@name where Id = 1", DbParam.Create("@name", "Chloe"));

            /* 
             * 执行存储过程:
             * Person person = this.DbContext.SqlQuery<Person>("Proc_GetPerson", CommandType.StoredProcedure, DbParam.Create("@id", 1)).FirstOrDefault();
             * rowsAffected = this.DbContext.Session.ExecuteNonQuery("Proc_UpdatePersonName", CommandType.StoredProcedure, DbParam.Create("@name", "Chloe"));
             */

            ConsoleHelper.WriteLineAndReadKey("ExecuteCommandText over...");
        }

        /// <summary>
        /// 事务
        /// </summary>
        public virtual void DoWithTransaction()
        {
            /*--------------1--------------*/
            using (ITransientTransaction tran = this.DbContext.BeginTransaction())
            {
                /* do some things here */
                this.DbContext.Update<Person>(a => a.Id == 1, a => new Person() { Name = a.Name, Age = a.Age + 1, Gender = Gender.Male, EditTime = DateTime.Now });
                this.DbContext.Delete<Person>(a => a.Id == 1024);

                tran.Commit();
            }


            /*--------------2--------------*/
            this.DbContext.UseTransaction(() =>
            {
                this.DbContext.Update<Person>(a => a.Id == 1, a => new Person() { Name = a.Name, Age = a.Age + 1, Gender = Gender.Male, EditTime = DateTime.Now });
                this.DbContext.Delete<Person>(a => a.Id == 1024);
            });


            /*--------------3--------------*/
            this.DbContext.Session.BeginTransaction();
            try
            {
                /* do some things here */
                this.DbContext.Update<Person>(a => a.Id == 1, a => new Person() { Name = a.Name, Age = a.Age + 1, Gender = Gender.Male, EditTime = DateTime.Now });
                this.DbContext.Delete<Person>(a => a.Id == 1024);

                this.DbContext.Session.CommitTransaction();
            }
            catch
            {
                if (this.DbContext.Session.IsInTransaction)
                    this.DbContext.Session.RollbackTransaction();
                throw;
            }

            ConsoleHelper.WriteLineAndReadKey("DoWithTransaction over...");
        }


        /// <summary>
        /// 其它测试
        /// </summary>
        public virtual void OtherTest()
        {
            var personQuery = this.DbContext.Query<Person>();
            var cityQuery = this.DbContext.Query<City>();


            personQuery.OrderBy(a => a.Id).Select(a => a.CityId).Distinct().Count();
            /*
             * SELECT COUNT(1) AS `C` FROM (SELECT DISTINCT `T`.`CityId` AS `C` FROM `Person` AS `T`) AS `T0`
             * 生成的 sql 语句中的子句不包含排序字段
             */

            personQuery.OrderBy(a => a.Id).Select(a => new { a.Id, a.CityId }).Distinct().Count();
            /*
             * SELECT COUNT(1) AS `C` FROM (SELECT DISTINCT `T`.`Id`,`T`.`CityId` FROM `Person` AS `T` WHERE ORDER BY `T`.`Id` ASC) AS `T0`
             * 生成的 sql 语句中的子句包含排序字段
             */


            //参数在左边测试
            int cityId = cityQuery.First().Id;
            var city1 = cityQuery.Where(a => cityId == a.Id).First();
            var city2 = cityQuery.Where(a => a.Id == cityId).First();
            Debug.Assert(city1.Id == cityId);
            Debug.Assert(city1.Id == city2.Id);

            int? cityId1 = null;

            //三元表达式测试
            var result1 = personQuery.Where(a => a.CityId == (cityId1 == null ? cityId : cityId1.Value)).Select(a => new { Id = a.Id, CityId = a.CityId, Age = a.Age, B = a.Age == null ? false : a.Age > 1 }).ToList();
            for (int i = 0; i < result1.Count; i++)
            {
                Debug.Assert(result1[i].CityId == (cityId1 == null ? cityId : cityId1.Value));
                Debug.Assert(result1[i].B == (result1[i].Age == null ? false : result1[i].Age > 1));
            }


            //二元表达式测试
            var result2 = personQuery.Where(a => a.CityId == (cityId1 ?? cityId)).Select(a => new { Id = a.Id, CityId = (cityId1 ?? cityId) }).ToList();
            var result3 = personQuery.Where(a => a.CityId == cityId).Select(a => new { Id = a.Id, CityId = cityId }).ToList();
            for (int i = 0; i < result2.Count; i++)
            {
                Debug.Assert(result2[i].CityId == cityId);
                Debug.Assert(result2[i].CityId == result3[i].CityId);
                Debug.Assert(result2[i].Id == result3[i].Id);
            }


            //表达式中多层嵌套 IQuery 对象测试
            //多层嵌套中使用 First()
            this.DbContext.Query<Person>().IgnoreAllFilters()
            .Where(a => this.DbContext
                        .Query<City>().IgnoreAllFilters()
                        .Where(c => this.DbContext
                                    .Query<Province>().IgnoreAllFilters()
                                    .Select(p => p.Id)
                                    .First() == c.ProvinceId
                              )
                        .Select(c => c.Id)
                        .First() == a.CityId
                  ).ToList();
            /*
             * SELECT [T].[Name],[T].[Gender],[T].[Age],[T].[CityId],[T].[CreateTime],[T].[EditTime],[T].[RowVersion],[T].[Id] 
             * FROM [Person] AS [T] 
             * WHERE 
             * (
             *   SELECT [T0].[Id] AS [C] 
             *   FROM [City] AS [T0] WHERE 
             *   (
             *     SELECT [T1].[Id] AS [C] FROM [Province] AS [T1] LIMIT 1 OFFSET 0
             *   ) = [T0].[ProvinceId] LIMIT 1 OFFSET 0
             * ) = [T].[CityId]
             */


            //多层嵌套中使用 ToList().Contains() 组合
            this.DbContext.Query<Person>().IgnoreAllFilters()
            .Where(a => this.DbContext
                        .Query<City>().IgnoreAllFilters()
                        .Where(c => this.DbContext
                                    .Query<Province>().IgnoreAllFilters()
                                    .Select(p => p.Id)
                                    .ToList().Contains(c.ProvinceId)
                              )
                        .Select(c => c.Id)
                        .ToList().Contains(a.CityId)
                  ).ToList();
            /*
             * SELECT [T].[Name],[T].[Gender],[T].[Age],[T].[CityId],[T].[CreateTime],[T].[EditTime],[T].[RowVersion],[T].[Id] 
             * FROM [Person] AS [T] 
             * WHERE [T].[CityId] IN 
             * (
             *   SELECT [T0].[Id] AS [C] 
             *   FROM [City] AS [T0] 
             *   WHERE [T0].[ProvinceId] IN 
             *   (
             *     SELECT [T1].[Id] AS [C] FROM [Province] AS [T1]
             *   )
             * )
             */


            //多层嵌套中使用 Any
            this.DbContext.Query<Person>().IgnoreAllFilters()
            .Where(a => this.DbContext.Query<City>().IgnoreAllFilters()
                        .Where(
                                  c => c.Name == a.Name && this.DbContext.Query<Province>().IgnoreAllFilters().Where(p => c.ProvinceId == p.Id).Any()
                              )
                        .Any()
                  ).ToList();
            /*
             * Input String @P_0 = '1';
             * SELECT [T].[Name],[T].[Gender],[T].[Age],[T].[CityId],[T].[CreateTime],[T].[EditTime],[T].[RowVersion],[T].[Id] 
             * FROM [Person] AS [T] 
             * WHERE Exists 
             * (
             *   SELECT @P_0 AS [C] 
             *   FROM [City] AS [T0] 
             *   WHERE 
             *   (
             *     (
             *       [T0].[Name] = [T].[Name] OR ([T0].[Name] IS NULL AND [T].[Name] IS NULL)
             *     ) 
             *     AND Exists 
             *     (
             *       SELECT @P_0 AS [C] FROM [Province] AS [T1] WHERE [T0].[ProvinceId] = [T1].[Id]
             *     )
             *   )
             * )
             */

            //复杂多层嵌套
            this.DbContext.Query<Person>().Where(c => this.DbContext.Query<Person>().IgnoreAllFilters().Where(a => this.DbContext.Query<Person>().Where(b => b.Name == "1").IgnoreAllFilters().Any()).Any()).IgnoreAllFilters().Count();

            //表达式树中使用变量的形式传入 IQuery 对象
            var qq = this.DbContext.Query<Person>().IgnoreAllFilters().Where(a => this.DbContext.Query<Person>().Where(b => b.Name == "1").IgnoreAllFilters().Any());
            this.DbContext.Query<Person>().Where(c => qq.Any()).IgnoreAllFilters().Count();
            /*
             * Input String ?P_0 = '1';
             * SELECT COUNT(1) AS `C` FROM `Person` AS `T` 
             * WHERE Exists 
             * (
             *   SELECT ?P_0 AS `C` FROM `Person` AS `T0` 
             *   WHERE Exists 
             *   (
             *     SELECT ?P_0 AS `C` FROM `Person` AS `T1` WHERE `T1`.`Name` = N'1'
             *   )
             * )
             */

            //以下两个查询生成的 sql 是一样的
            string sql1 = this.DbContext.Query<Person>().Where(c => this.DbContext.Query<Person>().IgnoreAllFilters().Where(a => this.DbContext.Query<Person>().Where(b => b.Name == "1").IgnoreAllFilters().Any()).Any()).IgnoreAllFilters().ToString();
            string sql2 = this.DbContext.Query<Person>().Where(c => qq.Any()).IgnoreAllFilters().ToString();

            Debug.Assert(sql1 == sql2);


            //表达式树中使用变量的形式将 lambda 传入表达式树中，如下中的 mm 变量
            System.Linq.Expressions.Expression<Func<Person, bool>> mm = a => this.DbContext.Query<Person>().IgnoreAllFilters().Where(b => b.Name == "1").Any();
            this.DbContext.Query<Person>().Where(c => this.DbContext.Query<Person>().IgnoreAllFilters().Where(mm).Any()).IgnoreAllFilters().Count();
            /*
             * Input String ?P_0 = '1';
             * SELECT COUNT(1) AS `C` FROM `Person` AS `T` 
             * WHERE Exists 
             * (
             *   SELECT ?P_0 AS `C` FROM `Person` AS `T0` 
             *   WHERE Exists 
             *   (
             *     SELECT ?P_0 AS `C` FROM `Person` AS `T1` WHERE `T1`.`Name` = N'1'
             *   )
             * )
             */


            //join 中使用子查询
            var query1 = this.DbContext.Query<Person>().Where(a => a.CreateTime >= this.DbContext.Query<Person>().Max(c => c.CreateTime));
            var zas = this.DbContext.Query<Person>()
            .LeftJoin(query1, (a, b) => a.Name == b.Name)
            .Select((a, b) => a).ToList();


            ConsoleHelper.WriteLineAndReadKey("OtherTest over...");
        }
    }
}
