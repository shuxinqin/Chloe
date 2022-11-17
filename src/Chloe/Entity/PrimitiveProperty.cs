using Chloe.DbExpressions;
using System.Data;
using System.Reflection;

namespace Chloe.Entity
{
    public class PrimitiveProperty : PropertyBase
    {
        public PrimitiveProperty(PropertyInfo property, EntityNameConfig config) : base(property)
        {
            this.ColumnName = config.MapCamelCaseToUnderscore ? Utils.TranslateCamelCaseToUnderscore(property.Name, config.ColumnPrefix) : property.Name;
        }

        public string ColumnName { get; set; }
        public string SequenceName { get; set; }
        public string SequenceSchema { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; }
        public bool IsNullable { get; set; }
        public bool IsRowVersion { get; set; }
        public bool IsUniqueIndex { get; set; }
        public DbType DbType { get; set; }
        public int? Size { get; set; }
        public byte? Scale { get; set; }
        public byte? Precision { get; set; }
        public bool UpdateIgnore { get; set; }

        public PrimitivePropertyDefinition MakeDefinition()
        {
            PrimitivePropertyDefinition definition = new PrimitivePropertyDefinition(this.Property, new DbColumn(this.ColumnName, this.Property.PropertyType, this.DbType, this.Size, this.Scale, this.Precision), this.IsPrimaryKey, this.IsAutoIncrement, this.IsNullable, this.IsRowVersion, this.IsUniqueIndex, this.SequenceName, this.SequenceSchema, this.UpdateIgnore, this.Annotations);
            return definition;
        }
    }
}
