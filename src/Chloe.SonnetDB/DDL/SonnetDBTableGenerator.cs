using Chloe.RDBMS.DDL;
using Chloe.Descriptors;
using Chloe.Reflection;

namespace Chloe.SonnetDB.DDL
{
    public class SonnetDBTableGenerator : TableGenerator
    {
        public SonnetDBTableGenerator(IDbContext dbContext) : base(dbContext)
        {
        }

        public SonnetDBTableGenerator(IDbContext dbContext, TableGenerateOptions options) : base(dbContext, options)
        {
        }

        public override List<string> GenCreateTableScript(TypeDescriptor typeDescriptor, string tableName, TableCreateMode createMode = TableCreateMode.CreateIfNotExists)
        {
            foreach (var property in typeDescriptor.PrimitivePropertyDescriptors)
            {
                if (property.Column.Precision != null || property.Column.Scale != null)
                    throw new NotSupportedException("SonnetDB does not enforce declared DECIMAL precision and scale constraints. See https://github.com/IoTSharp/SonnetDB/issues/264.");

                if (property.TryGetAnnotation(typeof(DataTypeAttribute), out var annotation) && annotation is DataTypeAttribute dataType)
                {
                    int leftParenthesis = dataType.Name?.IndexOf('(') ?? -1;
                    if (leftParenthesis >= 0)
                    {
                        string typeName = dataType.Name.Substring(0, leftParenthesis).Trim();
                        if (typeName.Equals("DECIMAL", StringComparison.OrdinalIgnoreCase) || typeName.Equals("NUMERIC", StringComparison.OrdinalIgnoreCase))
                            throw new NotSupportedException("SonnetDB does not enforce declared DECIMAL precision and scale constraints. See https://github.com/IoTSharp/SonnetDB/issues/264.");
                    }
                }
            }

            if (!string.IsNullOrEmpty(typeDescriptor.Table.Schema))
                throw new NotSupportedException("SonnetDB does not support database schemas. See https://github.com/IoTSharp/SonnetDB/issues/232.");
            if (typeDescriptor.PrimitivePropertyDescriptors.Any(a => a.HasSequence()))
                throw new NotSupportedException("SonnetDB does not support database sequences. See https://github.com/IoTSharp/SonnetDB/issues/231.");
            if (typeDescriptor.PrimaryKeys.Count == 0)
                throw new NotSupportedException("SonnetDB cannot write relational tables without a primary key. See https://github.com/IoTSharp/SonnetDB/issues/253.");

            tableName = string.IsNullOrEmpty(tableName) ? typeDescriptor.Table.Name : tableName;
            string quotedTableName = Utils.QuoteName(tableName);
            List<string> scripts = new List<string>();
            if (createMode == TableCreateMode.CreateNew)
                scripts.Add($"DROP TABLE IF EXISTS {quotedTableName};");

            StringBuilder sql = new StringBuilder("CREATE TABLE ");
            if (createMode == TableCreateMode.CreateIfNotExists)
                sql.Append("IF NOT EXISTS ");
            sql.Append(quotedTableName).AppendLine("(");

            string separator = "";
            foreach (var property in typeDescriptor.PrimitivePropertyDescriptors.OrderBy(a => GetTypeInheritLayer(a.Property.DeclaringType)))
            {
                sql.Append(separator).Append("  ").Append(Utils.QuoteName(property.Column.Name)).Append(" ").Append(this.GetDataTypeName(property));
                if (property.IsAutoIncrement)
                {
                    Type type = property.PropertyType.GetUnderlyingType();
                    if (type != typeof(int) && type != typeof(long) && type != typeof(short) && type != typeof(byte))
                        throw new NotSupportedException("SonnetDB AUTO_INCREMENT columns require an integer property.");
                    sql.Append(" AUTO_INCREMENT");
                }
                if (!property.IsNullable || property.IsPrimaryKey || property.IsAutoIncrement)
                    sql.Append(" NOT NULL");
                separator = "," + Environment.NewLine;
            }

            if (typeDescriptor.PrimaryKeys.Count > 0)
            {
                sql.Append(separator).Append("  PRIMARY KEY(");
                sql.Append(string.Join(",", typeDescriptor.PrimaryKeys.Select(a => Utils.QuoteName(a.Column.Name))));
                sql.Append(")");
            }
            sql.AppendLine().Append(");");
            scripts.Add(sql.ToString());
            return scripts;
        }

        string GetDataTypeName(PrimitivePropertyDescriptor property)
        {
            Utils.EnsureSupportedMappingType(property.PropertyType);
            if (property.PropertyType.GetUnderlyingType() == typeof(string) && property.Column.Size != null)
                throw new NotSupportedException("SonnetDB STRING columns do not enforce a maximum length. See https://github.com/IoTSharp/SonnetDB/issues/240.");

            if (property.TryGetAnnotation(typeof(DataTypeAttribute), out var annotation))
                return ((DataTypeAttribute)annotation).Name;

            Type type = property.PropertyType.GetUnderlyingType();
            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();
            if (type == typeof(string) || type == typeof(Guid))
                return "STRING";
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long))
                return "INT";
            if (type == typeof(ulong))
                return "DECIMAL";
            if (type == typeof(float) || type == typeof(double))
                return "FLOAT";
            if (type == typeof(decimal))
                return "DECIMAL";
            if (type == typeof(bool))
                return "BOOL";
            if (type == typeof(DateTime))
                return "DATETIME";
            if (type == typeof(byte[]))
                return "BLOB";

            throw new NotSupportedException(type.FullName);
        }
    }
}
