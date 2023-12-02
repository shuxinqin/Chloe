using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.Entity
{
    public class EntityTypeBuilder : IEntityTypeBuilder
    {
        public EntityTypeBuilder(Type type)
        {
            this.EntityType = new EntityType(type);
        }

        public EntityTypeBuilder(EntityNameConfig config)
        {
            this.EntityType = new EntityType(typeof(TEntity), config);
        }

        public EntityType EntityType { get; private set; }

        public IEntityTypeBuilder MapTo(string table)
        {
            this.EntityType.TableName = table;
            return this;
        }

        public IEntityTypeBuilder HasSchema(string schema)
        {
            this.EntityType.SchemaName = schema;
            return this;
        }

        public IEntityTypeBuilder HasAnnotation(object value)
        {
            if (value == null)
                throw new ArgumentNullException();

            this.EntityType.Annotations.Add(value);
            return this;
        }

        public IEntityTypeBuilder Ignore(string property)
        {
            this.EntityType.PrimitiveProperties.RemoveAll(a => a.Property.Name == property);
            return this;
        }

        public IEntityTypeBuilder HasQueryFilter(LambdaExpression filter)
        {
            this.EntityType.Filters.Add(filter);
            return this;
        }

        public virtual IPrimitivePropertyBuilder Property(string property)
        {
            PrimitiveProperty entityProperty = this.GetPrimitiveProperty(property);

            IPrimitivePropertyBuilder propertyBuilder = new PrimitivePropertyBuilder(entityProperty, this);
            return propertyBuilder;
        }

        public virtual IComplexPropertyBuilder HasOne(string property)
        {
            ComplexProperty complexProperty = this.GetComplexProperty(property);

            IComplexPropertyBuilder propertyBuilder = new ComplexPropertyBuilder(complexProperty, this);
            return propertyBuilder;
        }
        public virtual IComplexPropertyBuilder HasOne(string property, string foreignKey)
        {
            return this.HasOne(property).WithForeignKey(foreignKey);
        }

        public virtual ICollectionPropertyBuilder HasMany(string property)
        {
            CollectionProperty collectionProperty = this.GetCollectionProperty(property);

            ICollectionPropertyBuilder propertyBuilder = new CollectionPropertyBuilder(collectionProperty, this);
            return propertyBuilder;
        }

        internal PrimitiveProperty GetPrimitiveProperty(string property)
        {
            PrimitiveProperty entityProperty = this.EntityType.PrimitiveProperties.FirstOrDefault(a => a.Property.Name == property);

            if (entityProperty == null)
                throw new ArgumentException($"The mapping property list doesn't contain property named '{property}'.");

            return entityProperty;
        }
        internal ComplexProperty GetComplexProperty(string property)
        {
            ComplexProperty complexProperty = this.EntityType.ComplexProperties.Where(a => a.Property.Name == property).FirstOrDefault();

            if (complexProperty == null)
            {
                PropertyInfo propertyInfo = this.GetProperty(property);
                complexProperty = new ComplexProperty(propertyInfo);
                this.EntityType.ComplexProperties.Add(complexProperty);
            }

            return complexProperty;
        }
        internal CollectionProperty GetCollectionProperty(string property)
        {
            CollectionProperty collectionProperty = this.EntityType.CollectionProperties.Where(a => a.Property.Name == property).FirstOrDefault();

            if (collectionProperty == null)
            {
                PropertyInfo propertyInfo = this.GetProperty(property);
                collectionProperty = new CollectionProperty(propertyInfo);
                this.EntityType.CollectionProperties.Add(collectionProperty);
            }

            return collectionProperty;
        }


        internal PropertyInfo GetProperty(string property)
        {
            PropertyInfo entityProperty = this.EntityType.Type.GetProperty(property);

            if (entityProperty == null)
                throw new ArgumentException($"Cannot find property named '{property}'.");

            return entityProperty;
        }
    }
}
