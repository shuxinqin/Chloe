using Chloe.Core.Visitors;
using Chloe.DbExpressions;
using Chloe.Entity;
using Chloe.Query.Visitors;
using Chloe.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;

namespace Chloe.Descriptors
{
    public class TypeDescriptor
    {
        Dictionary<MemberInfo, MappingMemberDescriptor> _mappingMemberDescriptors;
        Dictionary<MemberInfo, NavigationMemberDescriptor> _navigationMemberDescriptors;
        Dictionary<MemberInfo, DbColumnAccessExpression> _memberColumnMap;
        MappingMemberDescriptor _primaryKey = null;

        DefaultExpressionVisitor _visitor = null;

        TypeDescriptor(Type t)
        {
            this.EntityType = t;
            this.Init();
        }

        void Init()
        {
            this.InitTableInfo();
            this.InitMemberInfo();
            this.InitMemberColumnMap();
        }
        void InitTableInfo()
        {
            Type t = this.EntityType;
            var tableFlags = t.GetCustomAttributes(typeof(TableAttribute), false);

            string tableName;
            if (tableFlags.Length > 0)
            {
                TableAttribute tableFlag = (TableAttribute)tableFlags.First();
                if (tableFlag.Name != null)
                    tableName = tableFlag.Name;
                else
                    tableName = t.Name;
            }
            else
                tableName = t.Name;

            this.Table = new DbTable(tableName);
        }
        void InitMemberInfo()
        {
            Type t = this.EntityType;
            var members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance);

            Dictionary<MemberInfo, MappingMemberDescriptor> mappingMemberDescriptors = new Dictionary<MemberInfo, MappingMemberDescriptor>();
            Dictionary<MemberInfo, NavigationMemberDescriptor> navigationMemberDescriptors = new Dictionary<MemberInfo, NavigationMemberDescriptor>();

            foreach (var member in members)
            {
                var ignoreFlags = member.GetCustomAttributes(typeof(NotMappedAttribute), false);
                if (ignoreFlags.Length > 0)
                    continue;

                Type memberType = null;
                PropertyInfo prop = null;
                FieldInfo field = null;

                if ((prop = member as PropertyInfo) != null)
                {
                    if (prop.GetSetMethod() == null)
                        continue;//对于没有公共的 setter 直接跳过
                    memberType = prop.PropertyType;
                }
                else if ((field = member as FieldInfo) != null)
                {
                    memberType = field.FieldType;
                }
                else
                    continue;//只支持公共属性和字段

                if (Utils.IsMapType(memberType))
                {
                    MappingMemberDescriptor memberDescriptor = this.ConstructDbFieldDescriptor(member);
                    mappingMemberDescriptors.Add(member, memberDescriptor);
                }
                else
                {
                    var associationFlags = member.GetCustomAttributes(typeof(AssociationAttribute), true);
                    if (associationFlags.Length > 0)
                    {
                        AssociationAttribute associationFlag = (AssociationAttribute)associationFlags.First();
                        NavigationMemberDescriptor navigationMemberDescriptor = null;
                        if (member.MemberType == MemberTypes.Property)
                        {
                            navigationMemberDescriptor = new NavigationPropertyDescriptor(prop, this, associationFlag.ThisKey, associationFlag.AssociatingKey);
                        }
                        else if (member.MemberType == MemberTypes.Field)
                        {
                            navigationMemberDescriptor = new NavigationFieldDescriptor(field, this, associationFlag.ThisKey, associationFlag.AssociatingKey);
                        }
                        else
                            continue;

                        navigationMemberDescriptors.Add(member, navigationMemberDescriptor);
                    }

                    continue;
                }
            }

            this._mappingMemberDescriptors = Utils.Clone(mappingMemberDescriptors);
            this._navigationMemberDescriptors = Utils.Clone(navigationMemberDescriptors);
        }
        void InitMemberColumnMap()
        {
            Dictionary<MemberInfo, DbColumnAccessExpression> memberColumnMap = new Dictionary<MemberInfo, DbColumnAccessExpression>(this._mappingMemberDescriptors.Count);
            foreach (var kv in this._mappingMemberDescriptors)
            {
                memberColumnMap.Add(kv.Key, new DbColumnAccessExpression(this.Table, kv.Value.Column));
            }

            this._memberColumnMap = memberColumnMap;
        }

        MappingMemberDescriptor ConstructDbFieldDescriptor(MemberInfo member)
        {
            string columnName = null;
            bool isPrimaryKey = false;

            var columnFlags = member.GetCustomAttributes(typeof(ColumnAttribute), true);
            if (columnFlags.Length > 0)
            {
                ColumnAttribute columnFlag = (ColumnAttribute)columnFlags.First();
                if (columnFlag.Name != null)
                    columnName = columnFlag.Name;
                else
                    columnName = member.Name;

                if (columnFlag.IsPrimaryKey)
                {
                    isPrimaryKey = true;
                }
            }
            else
                columnName = member.Name;


            MappingMemberDescriptor memberDescriptor = null;
            PropertyInfo propertyInfo = member as PropertyInfo;
            if (propertyInfo != null)
            {
                memberDescriptor = new PropertyDescriptor(propertyInfo, this, columnName);
            }
            else
            {
                memberDescriptor = new FieldDescriptor((FieldInfo)member, this, columnName);
            }

            memberDescriptor.IsPrimaryKey = isPrimaryKey;

            if (memberDescriptor.IsPrimaryKey)
            {
                if (this._primaryKey != null)
                {
                    throw new NotSupportedException(string.Format("Mapping type '{0}' can not define multiple primary keys.", this.EntityType.FullName));
                }

                this._primaryKey = memberDescriptor;
            }

            return memberDescriptor;
        }

        public Type EntityType { get; private set; }
        public DbTable Table { get; private set; }

        public MappingMemberDescriptor PrimaryKey { get { return this._primaryKey; } }
        public DefaultExpressionVisitor Visitor
        {
            get
            {
                if (this._visitor == null)
                    this._visitor = new DefaultExpressionVisitor(this);

                return this._visitor;
            }
        }

        public Dictionary<MemberInfo, MappingMemberDescriptor> MappingMemberDescriptors { get { return this._mappingMemberDescriptors; } }
        public Dictionary<MemberInfo, DbColumnAccessExpression> MemberColumnMap { get { return this._memberColumnMap; } }

        public bool HasPrimaryKey()
        {
            return this._primaryKey != null;
        }
        public MappingMemberDescriptor TryGetMappingMemberDescriptor(MemberInfo memberInfo)
        {
            MappingMemberDescriptor memberDescriptor;
            if (!this._mappingMemberDescriptors.TryGetValue(memberInfo, out memberDescriptor))
            {
                return null;
            }

            return memberDescriptor;
        }
        public NavigationMemberDescriptor TryGetNavigationMemberDescriptor(MemberInfo memberInfo)
        {
            NavigationMemberDescriptor memberDescriptor;
            if (!this._navigationMemberDescriptors.TryGetValue(memberInfo, out memberDescriptor))
            {
                return null;
            }
            return memberDescriptor;
        }

        static readonly ConcurrentDictionary<Type, TypeDescriptor> InstanceCache = new ConcurrentDictionary<Type, TypeDescriptor>();

        public static TypeDescriptor GetDescriptor(Type type)
        {
            return InstanceCache.GetOrAdd(type, (t) => new TypeDescriptor(t));
        }
    }
}
