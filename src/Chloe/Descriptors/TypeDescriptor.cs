﻿using Chloe.Visitors;
using Chloe.DbExpressions;
using Chloe.Entity;
using Chloe.Exceptions;
using Chloe.Query;
using Chloe.Reflection;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.Concurrent;

namespace Chloe.Descriptors
{
    public class TypeDescriptor
    {
        Dictionary<MemberInfo, PropertyDescriptor> _propertyDescriptorMap;
        Dictionary<MemberInfo, PrimitivePropertyDescriptor> _primitivePropertyDescriptorMap;
        Dictionary<MemberInfo, DbColumnAccessExpression> _primitivePropertyColumnMap;
        DefaultExpressionParser _expressionParser = null;

        public TypeDescriptor(TypeDefinition definition)
        {
            this.Definition = definition;
            this.PrimitivePropertyDescriptors = this.Definition.PrimitiveProperties.Select(a => new PrimitivePropertyDescriptor(a, this)).ToList().AsReadOnly();
            this.ComplexPropertyDescriptors = this.Definition.ComplexProperties.Select(a => new ComplexPropertyDescriptor(a, this)).ToList().AsReadOnly();
            this.CollectionPropertyDescriptors = this.Definition.CollectionProperties.Select(a => new CollectionPropertyDescriptor(a, this)).ToList().AsReadOnly();
            this.NavigationPropertyDescriptors = this.ComplexPropertyDescriptors.Cast<PropertyDescriptor>().Concat(this.CollectionPropertyDescriptors.Cast<PropertyDescriptor>()).ToList().AsReadOnly();

            var allPropertyDescriptors = this.PrimitivePropertyDescriptors.AsEnumerable<PropertyDescriptor>().Concat(this.ComplexPropertyDescriptors.AsEnumerable<PropertyDescriptor>()).Concat(this.CollectionPropertyDescriptors.AsEnumerable<PropertyDescriptor>());
            this._propertyDescriptorMap = PublicHelper.Clone(allPropertyDescriptors.ToDictionary(a => (MemberInfo)a.Definition.Property, a => a));

            this.PrimaryKeys = this.PrimitivePropertyDescriptors.Where(a => a.Definition.IsPrimaryKey).ToList().AsReadOnly();
            this.UniqueIndexs = this.PrimitivePropertyDescriptors.Where(a => a.Definition.IsUniqueIndex).ToList().AsReadOnly();
            this.AutoIncrement = this.PrimitivePropertyDescriptors.Where(a => a.Definition.IsAutoIncrement).FirstOrDefault();
            this.RowVersion = this.PrimitivePropertyDescriptors.Where(a => a.Definition.IsRowVersion).FirstOrDefault();

            this._primitivePropertyDescriptorMap = PublicHelper.Clone(this.PrimitivePropertyDescriptors.ToDictionary(a => (MemberInfo)a.Definition.Property, a => a));
            this._primitivePropertyColumnMap = PublicHelper.Clone(this.PrimitivePropertyDescriptors.ToDictionary(a => (MemberInfo)a.Definition.Property, a => new DbColumnAccessExpression(this.Definition.Table, a.Definition.Column)));
        }

        /// <summary>
        /// 同名缓存，重用
        /// </summary>
        ConcurrentDictionary<DbTableKey, List<Tuple<PrimitivePropertyDescriptor, DbColumnAccessExpression>>> CachedDbColumns { get; set; } = new ConcurrentDictionary<DbTableKey, List<Tuple<PrimitivePropertyDescriptor, DbColumnAccessExpression>>>();

        public TypeDefinition Definition { get; private set; }
        public ReadOnlyCollection<PrimitivePropertyDescriptor> PrimitivePropertyDescriptors { get; private set; }
        public ReadOnlyCollection<ComplexPropertyDescriptor> ComplexPropertyDescriptors { get; private set; }
        public ReadOnlyCollection<CollectionPropertyDescriptor> CollectionPropertyDescriptors { get; private set; }
        public ReadOnlyCollection<PropertyDescriptor> NavigationPropertyDescriptors { get; private set; }

        public ReadOnlyCollection<PrimitivePropertyDescriptor> PrimaryKeys { get; private set; }
        public ReadOnlyCollection<PrimitivePropertyDescriptor> UniqueIndexs { get; private set; }

        /* It will return null if an entity has no auto increment member. */
        public PrimitivePropertyDescriptor AutoIncrement { get; private set; }

        public PrimitivePropertyDescriptor RowVersion { get; private set; }

        public DbTable Table { get { return this.Definition.Table; } }
        public Type EntityType { get { return this.Definition.Type; } }

        public DefaultExpressionParser GetExpressionParser(DbTable explicitDbTable)
        {
            DbTable dbTable = explicitDbTable ?? this.Table;

            if (dbTable == this.Table)
            {
                if (this._expressionParser == null)
                    this._expressionParser = new DefaultExpressionParser(this, null);

                return this._expressionParser;
            }

            return new DefaultExpressionParser(this, dbTable);
        }

        public UpdateColumnExpressionParser GetUpdateColumnExpressionParser(DbTable dbTable, ParameterExpression parameterExp, QueryContext queryContext)
        {
            return new UpdateColumnExpressionParser(this, dbTable, parameterExp, queryContext);
        }

        public bool HasPrimaryKey()
        {
            return this.PrimaryKeys.Count > 0;
        }
        public bool HasRowVersion()
        {
            return this.RowVersion != null;
        }
        public bool IsPrimaryKey(MemberInfo member)
        {
            member = member.AsReflectedMemberOf(this.Definition.Type);
            return this.PrimaryKeys.Any(a => a.Definition.Property == member);
        }
        public bool IsUniqueIndex(MemberInfo member)
        {
            member = member.AsReflectedMemberOf(this.Definition.Type);
            return this.UniqueIndexs.Any(a => a.Definition.Property == member);
        }
        public bool IsMappingMember(MemberInfo member)
        {
            return this.FindPrimitivePropertyDescriptor(member) != null;
        }

        public PrimitivePropertyDescriptor FindPrimitivePropertyDescriptor(MemberInfo member)
        {
            member = member.AsReflectedMemberOf(this.Definition.Type);
            PrimitivePropertyDescriptor propertyDescriptor = this._primitivePropertyDescriptorMap.FindValue(member);
            return propertyDescriptor;
        }
        public PrimitivePropertyDescriptor GetPrimitivePropertyDescriptor(MemberInfo member)
        {
            PrimitivePropertyDescriptor propertyDescriptor = this.FindPrimitivePropertyDescriptor(member);
            if (propertyDescriptor == null)
                throw new ChloeException(string.Format("The member '{0}' does not map any column.", member.Name));

            return propertyDescriptor;
        }
        public PropertyDescriptor FindPropertyDescriptor(MemberInfo member)
        {
            member = member.AsReflectedMemberOf(this.Definition.Type);
            PropertyDescriptor propertyDescriptor = this._propertyDescriptorMap.FindValue(member);
            return propertyDescriptor;
        }
        public PropertyDescriptor GetPropertyDescriptor(MemberInfo member)
        {
            PropertyDescriptor propertyDescriptor = this.FindPropertyDescriptor(member);
            if (propertyDescriptor == null)
                throw new ChloeException($"Cannot find property descriptor which named '{member.Name}'.");

            return propertyDescriptor;
        }
        public DbColumnAccessExpression FindColumnAccessExpression(MemberInfo member)
        {
            member = member.AsReflectedMemberOf(this.Definition.Type);
            DbColumnAccessExpression dbColumnAccessExpression = this._primitivePropertyColumnMap.FindValue(member);
            return dbColumnAccessExpression;
        }
        public DbColumnAccessExpression GetColumnAccessExpression(MemberInfo member)
        {
            DbColumnAccessExpression dbColumnAccessExpression = this.FindColumnAccessExpression(member);
            if (dbColumnAccessExpression == null)
                throw new ChloeException(string.Format("The member '{0}' does not map any column.", member.Name));

            return dbColumnAccessExpression;
        }

        internal ComplexObjectModel GenObjectModel(DbTable table, QueryContext queryContext, QueryOptions queryOptions)
        {
            List<Tuple<PrimitivePropertyDescriptor, DbColumnAccessExpression>> columns;
            DbTableKey key = new DbTableKey(table);
            if (!this.CachedDbColumns.TryGetValue(key, out columns))
            {
                columns = new List<Tuple<PrimitivePropertyDescriptor, DbColumnAccessExpression>>(this.PrimitivePropertyDescriptors.Count);
                for (int i = 0; i < this.PrimitivePropertyDescriptors.Count; i++)
                {
                    PrimitivePropertyDescriptor propertyDescriptor = this.PrimitivePropertyDescriptors[i];
                    DbColumnAccessExpression columnAccessExpression = new DbColumnAccessExpression(table, propertyDescriptor.Column);
                    columns.Add(new Tuple<PrimitivePropertyDescriptor, DbColumnAccessExpression>(propertyDescriptor, columnAccessExpression));
                }

                this.CachedDbColumns[key] = columns;
            }

            ComplexObjectModel model = new ComplexObjectModel(queryContext, queryOptions, this.Definition.Type, this.PrimitivePropertyDescriptors.Count);

            for (int i = 0; i < columns.Count; i++)
            {
                var tuple = columns[i];
                var propertyDescriptor = tuple.Item1;
                var column = tuple.Item2;
                model.AddPrimitiveMember(tuple.Item1.Property, column);

                if (propertyDescriptor.IsPrimaryKey)
                    model.PrimaryKey = column;
            }

            return model;
        }

        internal ComplexObjectModel GenObjectModel(string tableName, QueryContext queryContext, QueryOptions queryOptions)
        {
            return GenObjectModel(new DbTable(tableName), queryContext, queryOptions);
        }

        internal DbTable GenDbTable(string explicitTableName)
        {
            DbTable dbTable = this.Table;
            if (!string.IsNullOrEmpty(explicitTableName))
                dbTable = new DbTable(explicitTableName, dbTable.Schema);

            return dbTable;
        }

        /// <summary>
        /// 根据传入的类型，获取对应的导航属性
        /// </summary>
        /// <param name="propertyType"></param>
        /// <returns></returns>
        /// <exception cref="ChloeException"></exception>
        internal ComplexPropertyDescriptor GetComplexPropertyDescriptorByPropertyType(Type propertyType)
        {
            ComplexPropertyDescriptor complexPropertyDescriptor = this.ComplexPropertyDescriptors.Where(a => a.PropertyType == propertyType).FirstOrDefault();

            if (complexPropertyDescriptor == null)
            {
                throw new ChloeException($"You have to define a navigation property which type is '{propertyType.FullName}' on class '{this.EntityType.FullName}'.");
            }

            return complexPropertyDescriptor;
        }
    }
}
