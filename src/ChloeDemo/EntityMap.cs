using Chloe.Entity;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Chloe.MySql;
using Chloe.Oracle;
using Chloe.PostgreSQL;
using Chloe.SQLite;
using Chloe.SqlServer;

namespace ChloeDemo
{
    public class EntityMapBase<TEntity> : EntityTypeBuilder<TEntity> where TEntity : EntityBase
    {
        public EntityMapBase()
        {
            this.Property(a => a.Id).IsAutoIncrement().IsPrimaryKey();
            this.Property(a => a.TenantId).UpdateIgnore();
        }
    }

    public class PersonMap : EntityMapBase<Person>
    {
        public PersonMap()
        {
            this.MapTo("Person");
            this.Property(a => a.Gender).HasDbType(DbType.Int32);

            this.Property(a => a.RowVersion).IsRowVersion();  //配置行版本

            this.Property(a => a.CreateTime).UpdateIgnore(); //更新实体时不更新此字段

            /* 配置导航属性关系 */
            this.HasOne(a => a.Profile, a => a.Id, a => a.Id);
            this.HasOne(a => a.City, a => a.CityId, a => a.Id);

            this.Ignore(a => a.NotMapped);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -1);

            this.ConfigDataType();
        }

        /// <summary>
        /// 配置不同数据库下对应的映射字段类型
        /// </summary>
        void ConfigDataType()
        {
            this.Property(a => a.Name)
                .HasMySqlDataType("varchar(50)")
                .HasOracleDataType("NVARCHAR2(50)")
                .HasPostgreSQLDataType("varchar(50)")
                .HasSQLiteDataType("NVARCHAR(50)")
                .HasSqlServerDataType("NVARCHAR(50)");
        }
    }

    public class PersonProfileMap : EntityTypeBuilder<PersonProfile>
    {
        public PersonProfileMap()
        {
            this.MapTo("PersonProfile");
            this.Property(a => a.Id).IsPrimaryKey().IsAutoIncrement(false);

            /* 配置导航属性关系 */
            this.HasOne(a => a.Person).WithForeignKey(a => a.Id);
            this.HasMany(a => a.Annexes);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -1);
        }
    }

    public class ProfileAnnexMap : EntityTypeBuilder<ProfileAnnex>
    {
        public ProfileAnnexMap()
        {
            this.MapTo("ProfileAnnex");
            this.Property(a => a.Id).IsPrimaryKey();

            /* 配置导航属性关系 */
            this.HasOne(a => a.Owner).WithForeignKey(a => a.ProfileId);
        }
    }

    public class CityMap : EntityMapBase<City>
    {
        public CityMap()
        {
            this.Property(a => a.Id).IsPrimaryKey().IsAutoIncrement(false);

            /* 配置导航属性关系 */
            this.HasMany(a => a.Persons);
            this.HasOne(a => a.Province).WithForeignKey(a => a.ProvinceId);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -2);
        }
    }

    public class ProvinceMap : EntityMapBase<Province>
    {
        public ProvinceMap()
        {
            this.Property(a => a.Id).IsPrimaryKey().IsAutoIncrement(false);

            /* 配置导航属性关系 */
            this.HasMany(a => a.Cities);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -3);
        }
    }

    public class TestEntityMap : EntityTypeBuilder<TestEntity>
    {
        public TestEntityMap()
        {
            this.Property(a => a.Id).IsAutoIncrement().IsPrimaryKey();

            //oralce 暂时不支持 guid
            this.Ignore(a => a.F_Guid);

            /* global filter */
            this.HasQueryFilter(a => a.Id > 0);
        }
    }

    public class OracleTestEntityMap : TestEntityMap
    {
        public OracleTestEntityMap()
        {


            //可以指定序列名
            //this.Property(a => a.Id).HasSequence("TestEntity_AutoId", null);
        }
    }
}
