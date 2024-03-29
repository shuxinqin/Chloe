﻿using Chloe.Annotations;
using Chloe.Entity;
using Chloe.Oracle;
using Chloe.SqlServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ChloeDemo
{
    public enum Gender
    {
        Male = 1,
        Female = 2
    }

    public interface IEntity
    {
        int Id { get; set; }
    }

    public class EntityBase : IEntity
    {
        /// <summary>
        /// 实体id
        /// </summary>
        [Column(IsPrimaryKey = true)]
        [AutoIncrement]
        public virtual int Id { get; set; }
    }

    //如果使用 fluentmapping，就可以不用打特性了
    [TableAttribute("Person")]
    public class Person : EntityBase
    {
        /* 1:1，注：PersonEx 那边也要做相应的配置 */
        [Navigation("Id")]
        public PersonEx Ex { get; set; }

        /* 1:1，注：City 那边也要做相应的配置 */
        [Chloe.Annotations.NavigationAttribute("CityId")]
        public City City { get; set; }

        /// <summary>
        /// 姓名
        /// </summary>
        [Column(DbType = DbType.String)]
        public string Name { get; set; }
        [Column(DbType = DbType.Int32)]
        public Gender? Gender { get; set; }
        public int? Age { get; set; }
        public int CityId { get; set; }

        /// <summary>
        /// 更新实体时不更新此字段
        /// </summary>
        [UpdateIgnoreAttribute]
        public DateTime CreateTime { get; set; }
        public DateTime? EditTime { get; set; }

        /// <summary>
        /// 行版本
        /// </summary>
        [Column(IsRowVersion = true)]
        public int RowVersion { get; set; }

        [NotMapped]
        public string NotMapped { get; set; }
    }

    public class PersonEx
    {
        /* 1:1，注：Person 那边也要做相应的配置 */
        [Navigation("Id")]
        public Person Owner { get; set; }  /* 1:1 */

        [Column(IsPrimaryKey = true)]
        [NonAutoIncrement]
        public int Id { get; set; }
        public string IdNumber { get; set; }
        public DateTime? BirthDay { get; set; }
    }

    public class City : EntityBase
    {
        public string Name { get; set; }
        public int ProvinceId { get; set; }

        /* 1:1，注：Province 那边也要做相应的配置 */
        [Chloe.Annotations.NavigationAttribute("ProvinceId")]
        public Province Province { get; set; }

        /* 1:N，注：Person 那边也要做相应的配置 */
        [Chloe.Annotations.NavigationAttribute]
        public List<Person> Persons { get; set; } = new List<Person>();
    }

    public class Province : EntityBase
    {
        public string Name { get; set; }

        /* 1:N，注：City 那边也要做相应的配置 */
        [Chloe.Annotations.NavigationAttribute]
        public List<City> Cities { get; set; } = new List<City>();
    }
}
