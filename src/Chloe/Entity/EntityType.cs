using Chloe.DbExpressions;
using Chloe.Exceptions;
using Chloe.Infrastructure;
using Chloe.Reflection;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.Entity
{
    public class EntityType
    {
        public EntityType(Type type) : this(type, EntityNameConfig.Of(type))
        {
        }

        public EntityType(Type type, EntityNameConfig config)
        {
            this.Type = type;
            this.TableName = config.MapCamelCaseToUnderscore && config.MapTableName ? Utils.TranslateCamelCaseToUnderscore(type.Name, config.TablePrefix) : type.Name;

            PropertyInfo[] properties = this.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(a => a.GetSetMethod() != null && a.GetGetMethod() != null).ToArray();

            foreach (PropertyInfo property in properties)
            {
                MappingType mappingType;
                if (!MappingTypeSystem.IsMappingType(property.PropertyType, out mappingType))
                    continue;

                PrimitiveProperty primitiveProperty = new PrimitiveProperty(property, config);
                primitiveProperty.DbType = mappingType.DbType;
                primitiveProperty.IsNullable = property.PropertyType.CanNull();

                if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    /* 默认为主键 */
                    primitiveProperty.IsPrimaryKey = true;

                    if (Utils.IsAutoIncrementType(property.PropertyType))
                        primitiveProperty.IsAutoIncrement = true;

                    if (property.PropertyType == typeof(string))
                    {
                        //如果主键是 string 类型，默认为 AnsiString
                        primitiveProperty.DbType = System.Data.DbType.AnsiString;
                    }
                }

                this.PrimitiveProperties.Add(primitiveProperty);
            }
        }

        public Type Type { get; private set; }
        public string TableName { get; set; }
        public string SchemaName { get; set; }
        public List<PrimitiveProperty> PrimitiveProperties { get; private set; } = new List<PrimitiveProperty>();
        public List<ComplexProperty> ComplexProperties { get; private set; } = new List<ComplexProperty>();
        public List<CollectionProperty> CollectionProperties { get; private set; } = new List<CollectionProperty>();
        public List<LambdaExpression> Filters { get; private set; } = new List<LambdaExpression>();
        public List<object> Annotations { get; private set; } = new List<object>();

        public virtual TypeDefinition MakeDefinition()
        {
            List<PrimitivePropertyDefinition> primitiveProperties = this.PrimitiveProperties.Select(a => a.MakeDefinition()).ToList();
            var autoIncrementProperties = primitiveProperties.Where(a => a.IsAutoIncrement).ToList();
            if (autoIncrementProperties.Count > 1)
            {
                /* 一个实体不能有多个自增成员 */
                throw new NotSupportedException(string.Format("The entity type '{0}' can not define multiple auto increment members.", this.Type.FullName));
            }
            if (autoIncrementProperties.Exists(a => !Utils.IsAutoIncrementType(a.Property.PropertyType)))
            {
                /* 限定自增类型 */
                throw new ChloeException("Auto increment member type must be Int16, Int32 or Int64.");
            }

            var primaryKeys = primitiveProperties.Where(a => a.IsPrimaryKey).ToList();
            if (primaryKeys.Count > 1 && primaryKeys.Exists(a => a.IsAutoIncrement))
            {
                /* 自增列不能作为联合主键 */
                throw new ChloeException("Auto increment member can not be union key.");
            }

            var rowVersionProperties = primitiveProperties.Where(a => a.IsRowVersion).ToList();
            if (rowVersionProperties.Count > 1)
            {
                throw new NotSupportedException(string.Format("The entity type '{0}' can not define multiple row version members.", this.Type.FullName));
            }
            else if (rowVersionProperties.Count == 1)
            {
                var rowVersionProperty = rowVersionProperties.First().Property;
                if (rowVersionProperty.PropertyType != PublicConstants.TypeOfInt32 && rowVersionProperty.PropertyType != PublicConstants.TypeOfInt64 && rowVersionProperty.PropertyType != PublicConstants.TypeOfByteArray)
                {
                    throw new ChloeException("Row version member type must be Int32, Int64 or Byte[].");
                }
            }

            List<ComplexPropertyDefinition> complexProperties = this.ComplexProperties.Select(a => a.MakeDefinition()).ToList();
            List<CollectionPropertyDefinition> collectionProperties = this.CollectionProperties.Select(a => a.MakeDefinition()).ToList();

            TypeDefinition definition = new TypeDefinition(this.Type, new DbTable(this.TableName, this.SchemaName), primitiveProperties, complexProperties, collectionProperties, this.Filters, this.Annotations);
            return definition;
        }
    }

}
