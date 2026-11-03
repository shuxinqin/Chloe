using Chloe.Entity;

namespace Chloe.SonnetDB
{
    public static class PrimitivePropertyBuilderExtension
    {
        public static IPrimitivePropertyBuilder HasSonnetDBDataType(this IPrimitivePropertyBuilder propertyBuilder, string name)
        {
            DataTypeAttribute dataTypeAttribute = new DataTypeAttribute(name);
            propertyBuilder.HasAnnotation(dataTypeAttribute);
            return propertyBuilder;
        }

        public static IPrimitivePropertyBuilder<TProperty, TEntity> HasSonnetDBDataType<TProperty, TEntity>(this IPrimitivePropertyBuilder<TProperty, TEntity> propertyBuilder, string name)
        {
            (propertyBuilder as IPrimitivePropertyBuilder).HasSonnetDBDataType(name);
            return propertyBuilder;
        }
    }
}
