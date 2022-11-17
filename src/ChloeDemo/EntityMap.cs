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
        public EntityMapBase() : base(new EntityNameConfig(true))
        {
            this.Property(a => a.Id).IsAutoIncrement().IsPrimaryKey();
        }
    }

    public class PersonMap : EntityMapBase<Person>
    {
        public PersonMap()
        {
            this.MapTo("Person");
            this.Property(a => a.Gender).HasDbType(DbType.Int32);
            this.Property(a => a.CreateTime).UpdateIgnore(); //更新实体时不更新此字段

            this.HasOne(a => a.Ex).WithForeignKey(a => a.Id);
            this.HasOne(a => a.City).WithForeignKey(a => a.CityId);
            this.Ignore(a => a.NotMapped);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -1);

            this.ConfigDataType();
        }

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

    public class PersonExMap : EntityTypeBuilder<PersonEx>
    {
        public PersonExMap() : base(new EntityNameConfig(true))
        {
            this.MapTo("PersonEx");
            this.Property(a => a.Id).IsPrimaryKey().IsAutoIncrement(false);

            this.HasOne(a => a.Owner).WithForeignKey(a => a.Id);

            /* global filter */
            this.HasQueryFilter(a => a.Id > -1);
        }
    }

    public class CityMap : EntityMapBase<City>
    {
        public CityMap()
        {
            this.HasMany(a => a.Persons);
            this.HasOne(a => a.Province).WithForeignKey(a => a.ProvinceId);

            this.HasQueryFilter(a => a.Id > -2);
        }
    }

    public class ProvinceMap : EntityMapBase<Province>
    {
        public ProvinceMap()
        {
            this.HasMany(a => a.Cities);

            this.HasQueryFilter(a => a.Id > -3);
        }
    }

    public class TestEntityMap : EntityTypeBuilder<TestEntity>
    {
        public TestEntityMap() : base(new EntityNameConfig(true, true, "sys_", "col_"))
        {
            this.Property(a => a.Id).IsAutoIncrement().IsPrimaryKey();
            this.HasQueryFilter(a => a.Id > 0);
        }
    }

    public class OracleTestEntityMap : TestEntityMap
    {
        public OracleTestEntityMap()
        {
            //oralce 暂时不支持 guid
            this.Ignore(a => a.F_Guid);

            //可以指定序列名
            //this.Property(a => a.Id).HasSequence("TestEntity_AutoId", null);
        }
    }
}
