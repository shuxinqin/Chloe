using Chloe.DbExpressions;

namespace Chloe.MySql
{
    public class MySqlDbUpdateExpression : DbUpdateExpression
    {
        public MySqlDbUpdateExpression(DbTable table) : this(table, null)
        {
        }
        public MySqlDbUpdateExpression(DbTable table, DbExpression condition) : base(table, condition)
        {
        }

        public MySqlDbUpdateExpression(DbTable table, DbExpression condition, int updateColumnCount, int returnCount) : base(table, condition, updateColumnCount, returnCount)
        {

        }

        public int Limits { get; set; }
    }
}
