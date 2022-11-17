using Chloe.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChloeDemo.Sharding
{
    [CamelCaseToUnderscore(false)]
    public class Order
    {
        [Column(IsPrimaryKey = true, Size = 50)]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [Column(Size = 50)]
        public string UserId { get; set; } = "chloe";
        public decimal Amount { get; set; }
        public int CreateMonth { get; set; }
        public int CreateYear { get; set; }
        public int CreateDate { get; set; }
        public DateTime CreateTime { get; set; }

        public void SetCreateTime(DateTime createTime)
        {
            this.CreateTime = createTime;
            this.CreateDate = int.Parse(this.CreateTime.ToString("yyyyMMdd"));
            this.CreateYear = int.Parse(this.CreateTime.ToString("yyyy"));
            this.CreateMonth = int.Parse(this.CreateTime.ToString("MM"));
            this.Id = this.CreateTime.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
