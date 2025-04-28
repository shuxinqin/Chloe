﻿using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Exceptions;
using Chloe.Extensions;
using Chloe.Infrastructure;
using Chloe.Query.Mapping;
using Chloe.QueryExpressions;
using Chloe.Reflection;
using Chloe.Utility;
using System.Linq.Expressions;
using System.Reflection;
using Chloe.Visitors;

namespace Chloe.Query
{
    public class ComplexObjectModel : IObjectModel
    {
        QueryContext _queryContext;
        HashSet<MemberInfo> _excludedFields = null;

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, Type objectType) : this(queryContext, queryOptions, GetDefaultConstructor(objectType), 0)
        {
        }

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, Type objectType, int primitiveMemberCount) : this(queryContext, queryOptions, GetDefaultConstructor(objectType), primitiveMemberCount)
        {
        }

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, ConstructorInfo constructor) : this(queryContext, queryOptions, ConstructorDescriptor.GetInstance(constructor), 0)
        {
        }

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, ConstructorInfo constructor, int primitiveMemberCount) : this(queryContext, queryOptions, ConstructorDescriptor.GetInstance(constructor), primitiveMemberCount)
        {
        }

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, ConstructorDescriptor constructorDescriptor) : this(queryContext, queryOptions, constructorDescriptor, 0)
        {

        }

        public ComplexObjectModel(QueryContext queryContext, QueryOptions queryOptions, ConstructorDescriptor constructorDescriptor, int primitiveMemberCount)
        {
            this._queryContext = queryContext;
            this.QueryOptions = queryOptions;

            this.ObjectType = constructorDescriptor.ConstructorInfo.DeclaringType;
            this.ConstructorDescriptor = constructorDescriptor;
            this.PrimitiveConstructorParameters = new Dictionary<ParameterInfo, DbExpression>();
            this.ComplexConstructorParameters = new Dictionary<ParameterInfo, ComplexObjectModel>();
            this.PrimitiveMembers = new Dictionary<MemberInfo, DbExpression>(primitiveMemberCount);
            this.ComplexMembers = new Dictionary<MemberInfo, ComplexObjectModel>();
            this.CollectionMembers = new Dictionary<MemberInfo, CollectionObjectModel>();
        }

        static ConstructorInfo GetDefaultConstructor(Type type)
        {
            var ret = type.GetDefaultConstructor();
            if (ret == null)
            {
                throw new ArgumentException(string.Format("The type of '{0}' does't define a none parameter constructor.", type.FullName));
            }

            return ret;
        }

        public QueryOptions QueryOptions { get; private set; }
        public Type ObjectType { get; private set; }
        public TypeKind TypeKind { get { return TypeKind.Complex; } }
        public DbExpression PrimaryKey { get; set; }
        public DbExpression NullChecking { get; set; }

        /// <summary>
        /// 当前 model 相关的 table
        /// </summary>
        public DbMainTableExpression AssociatedTable { get; set; }
        /// <summary>
        /// 导航集合属性
        /// </summary>
        public List<NavigationNode> IncludeCollections { get; set; } = new List<NavigationNode>();
        public List<DbExpression> Filters { get; set; } = new List<DbExpression>();

        /// <summary>
        /// 返回类型
        /// </summary>
        public ConstructorDescriptor ConstructorDescriptor { get; private set; }
        public Dictionary<ParameterInfo, DbExpression> PrimitiveConstructorParameters { get; private set; }
        public Dictionary<ParameterInfo, ComplexObjectModel> ComplexConstructorParameters { get; private set; }
        public Dictionary<MemberInfo, DbExpression> PrimitiveMembers { get; protected set; }
        public Dictionary<MemberInfo, ComplexObjectModel> ComplexMembers { get; protected set; }
        public Dictionary<MemberInfo, CollectionObjectModel> CollectionMembers { get; protected set; }

        /// <summary>
        /// 在表达式树中引用到但没有 Include 的导航属性，如：query.Where(a => a.City.Name == "BeiJing")，其中 City 属性没有被 Include
        /// </summary>
        public Dictionary<MemberInfo, ComplexObjectModel> TouchedComplexMembers { get; protected set; } = new Dictionary<MemberInfo, ComplexObjectModel>();

        public void AddConstructorParameter(ParameterInfo p, DbExpression primitiveExp)
        {
            this.PrimitiveConstructorParameters.Add(p, primitiveExp);
        }
        public void AddConstructorParameter(ParameterInfo p, ComplexObjectModel complexModel)
        {
            this.ComplexConstructorParameters.Add(p, complexModel);
        }
        public void AddPrimitiveMember(MemberInfo memberInfo, DbExpression exp)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            this.PrimitiveMembers.Add(memberInfo, exp);
        }

        /// <summary>
        /// 考虑匿名函数构造函数参数和其只读属性的对应
        /// </summary>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public DbExpression GetPrimitiveMember(MemberInfo memberInfo)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            DbExpression ret = null;
            if (!this.PrimitiveMembers.TryGetValue(memberInfo, out ret))
            {
                ParameterInfo p = null;
                if (!this.ConstructorDescriptor.MemberParameterMap.TryGetValue(memberInfo, out p))
                {
                    return null;
                }

                if (!this.PrimitiveConstructorParameters.TryGetValue(p, out ret))
                {
                    return null;
                }
            }

            return ret;
        }

        public void AddComplexMember(MemberInfo memberInfo, ComplexObjectModel model)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            this.ComplexMembers.Add(memberInfo, model);
        }
        public ComplexObjectModel GetComplexMember(MemberInfo memberInfo)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            ComplexObjectModel ret = null;
            if (!this.ComplexMembers.TryGetValue(memberInfo, out ret))
            {
                //从构造函数中查
                ParameterInfo p = null;
                if (!this.ConstructorDescriptor.MemberParameterMap.TryGetValue(memberInfo, out p))
                {
                    return null;
                }

                if (!this.ComplexConstructorParameters.TryGetValue(p, out ret))
                {
                    return null;
                }
            }

            return ret as ComplexObjectModel;
        }

        public void AddCollectionMember(MemberInfo memberInfo, CollectionObjectModel model)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            this.CollectionMembers.Add(memberInfo, model);
        }
        public CollectionObjectModel GetCollectionMember(MemberInfo memberInfo)
        {
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);
            CollectionObjectModel ret = this.CollectionMembers.FindValue(memberInfo);

            return ret;
        }

        public DbExpression GetDbExpression(MemberExpression memberExpressionDeriveFromParameter, QueryModel? queryModel)
        {
            Stack<MemberExpression> memberExpressions = ExpressionExtension.Reverse(memberExpressionDeriveFromParameter);

            DbExpression ret = null;
            IObjectModel model = this;
            foreach (MemberExpression memberExpression in memberExpressions)
            {
                MemberInfo accessedMember = memberExpression.Member;

                if (model == null && ret != null)
                {
                    /* a.F_DateTime.Value.Date */
                    ret = new DbMemberAccessExpression(accessedMember, ret);
                    continue;
                }

                /* **.accessedMember */
                DbExpression e = model.GetPrimitiveMember(accessedMember);
                if (e == null)
                {
                    /* Indicate current accessed member is not mapping member, then try get complex member like 'a.Order' */
                    var objectModel = model.GetComplexMember(accessedMember);

                    if (objectModel != null)
                    {
                        model = objectModel;
                        continue;
                    }

                    if (ret != null)
                    {
                        /* Non mapping member is not found also, then convert linq MemberExpression to DbMemberExpression */
                        ret = new DbMemberAccessExpression(accessedMember, ret);
                        continue;
                    }

                    if (model is ComplexObjectModel complexObjectModel)
                    {
                        //try touch
                        objectModel = complexObjectModel.TryTouchComplexProperty(accessedMember, queryModel);
                        if (objectModel != null)
                        {
                            model = objectModel;
                            continue;
                        }
                    }

                    /*
                     * If run here,the member access expression must be like 'a.xx',
                     * and member 'xx' is neither mapping member nor complex member,in this case,we not supported.
                     */
                    throw new InvalidOperationException(memberExpressionDeriveFromParameter.ToString());
                }
                else
                {
                    if (ret != null)//Case: #110
                        throw new InvalidOperationException(memberExpressionDeriveFromParameter.ToString());

                    ret = e;
                    model = null;
                }
            }

            if (ret == null)
            {
                /*
                 * If run here,the input argument 'memberExpressionDeriveFromParameter' expression must be like 'a.xx','a.**.xx','a.**.**.xx' ...and so on,
                 * and the last accessed member 'xx' is not mapping member, in this case, we not supported too.
                 */
                throw new InvalidOperationException(memberExpressionDeriveFromParameter.ToString());
            }

            return ret;
        }
        public IObjectModel GetComplexMember(MemberExpression memberExpressionDeriveParameter)
        {
            Stack<MemberExpression> memberExpressions = ExpressionExtension.Reverse(memberExpressionDeriveParameter);

            if (memberExpressions.Count == 0)
                throw new Exception();

            IObjectModel ret = this;
            foreach (MemberExpression memberExpression in memberExpressions)
            {
                MemberInfo member = memberExpression.Member;

                ret = ret.GetComplexMember(member);
                if (ret == null)
                {
                    throw new NotSupportedException(memberExpressionDeriveParameter.ToString());
                }
            }

            return ret;
        }
        public IObjectActivatorCreator GenarateObjectActivatorCreator(List<DbColumnSegment> columns, HashSet<string> aliasSet)
        {
            ComplexObjectActivatorCreator activatorCreator = new ComplexObjectActivatorCreator(this.ConstructorDescriptor);

            foreach (var kv in this.PrimitiveConstructorParameters)
            {
                ParameterInfo pi = kv.Key;
                DbExpression exp = kv.Value;

                if (this.IsExcludedMember(pi))
                {
                    continue;
                }

                int ordinal = ObjectModelHelper.AddColumn(columns, aliasSet, exp, pi.Name);

                if (exp == this.NullChecking)
                    activatorCreator.CheckNullOrdinal = ordinal;

                activatorCreator.ConstructorParameters.Add(pi, ordinal);
            }

            foreach (var kv in this.ComplexConstructorParameters)
            {
                ParameterInfo pi = kv.Key;
                IObjectModel val = kv.Value;

                IObjectActivatorCreator complexMappingMember = val.GenarateObjectActivatorCreator(columns, aliasSet);
                activatorCreator.ConstructorComplexParameters.Add(pi, complexMappingMember);
            }

            foreach (var kv in this.PrimitiveMembers)
            {
                MemberInfo member = kv.Key;
                DbExpression exp = kv.Value;

                if (this.IsExcludedMember(member))
                {
                    continue;
                }

                int ordinal = ObjectModelHelper.AddColumn(columns, aliasSet, exp, member.Name);

                if (exp == this.NullChecking)
                    activatorCreator.CheckNullOrdinal = ordinal;

                activatorCreator.PrimitiveMembers.Add(member, ordinal);
            }

            foreach (var kv in this.ComplexMembers)
            {
                IObjectActivatorCreator complexMemberActivatorCreator = kv.Value.GenarateObjectActivatorCreator(columns, aliasSet);
                activatorCreator.ComplexMembers.Add(kv.Key, complexMemberActivatorCreator);
            }

            foreach (var kv in this.CollectionMembers)
            {
                IObjectActivatorCreator collectionMemberActivatorCreator = kv.Value.GenarateObjectActivatorCreator(columns, aliasSet);
                activatorCreator.CollectionMembers.Add(kv.Key, collectionMemberActivatorCreator);
            }

            if (this.NullChecking != null && activatorCreator.CheckNullOrdinal == null)
                activatorCreator.CheckNullOrdinal = ObjectModelHelper.AddColumn(columns, aliasSet, this.NullChecking);

            return activatorCreator;
        }

        public IObjectModel ToNewObjectModel(List<DbColumnSegment> columns, HashSet<string> aliasSet, DbTable table, DbMainTableExpression dependentTable)
        {
            ComplexObjectModel newModel = new ComplexObjectModel(this._queryContext, this.QueryOptions, this.ConstructorDescriptor, this.PrimitiveMembers.Count);
            newModel.AssociatedTable = dependentTable;
            newModel.IncludeCollections.AppendRange(this.IncludeCollections);

            if (!this.QueryOptions.IgnoreFilters)
            {
                this.SetupFilters();
            }

            foreach (var kv in this.PrimitiveConstructorParameters)
            {
                ParameterInfo pi = kv.Key;
                DbExpression exp = kv.Value;

                if (this.IsExcludedMember(pi))
                {
                    continue;
                }

                DbColumnAccessExpression cae = null;
                cae = ObjectModelHelper.ParseColumnAccessExpression(columns, aliasSet, table, exp, pi.Name);

                newModel.AddConstructorParameter(pi, cae);
            }

            foreach (var kv in this.ComplexConstructorParameters)
            {
                ParameterInfo pi = kv.Key;
                IObjectModel val = kv.Value;

                ComplexObjectModel complexMemberModel = val.ToNewObjectModel(columns, aliasSet, table, dependentTable) as ComplexObjectModel;
                newModel.AddConstructorParameter(pi, complexMemberModel);
            }

            foreach (var kv in this.PrimitiveMembers)
            {
                MemberInfo member = kv.Key;
                DbExpression exp = kv.Value;

                if (this.IsExcludedMember(member))
                {
                    continue;
                }

                DbColumnAccessExpression cae = ObjectModelHelper.ParseColumnAccessExpression(columns, aliasSet, table, exp, member.Name);

                newModel.AddPrimitiveMember(member, cae);

                if (exp == this.PrimaryKey)
                {
                    newModel.PrimaryKey = cae;
                    if (this.NullChecking == this.PrimaryKey)
                        newModel.NullChecking = cae;
                }
            }

            foreach (var kv in this.ComplexMembers)
            {
                MemberInfo member = kv.Key;
                IObjectModel val = kv.Value;

                ComplexObjectModel complexMemberModel = val.ToNewObjectModel(columns, aliasSet, table, dependentTable) as ComplexObjectModel;
                newModel.AddComplexMember(member, complexMemberModel);
            }

            if (this.NullChecking != null && newModel.NullChecking == null)
                newModel.NullChecking = ObjectModelHelper.AddNullCheckingColumn(columns, aliasSet, table, this.NullChecking);

            return newModel;
        }

        public void Include(NavigationNode navigationNode, QueryModel queryModel, bool handleCollection)
        {
            TypeDescriptor descriptor = EntityTypeContainer.GetDescriptor(this.ObjectType);
            PropertyDescriptor navigationDescriptor = descriptor.GetPropertyDescriptor(navigationNode.Property);

            if (navigationDescriptor.Definition.Kind == TypeKind.Primitive)
            {
                throw new NotSupportedException($"'{navigationNode.Property.Name}' is not navigation property.");
            }

            ComplexObjectModel objectModel = null;
            if (navigationDescriptor.Definition.Kind == TypeKind.Complex)
            {
                objectModel = this.GetComplexMember(navigationNode.Property);

                if (objectModel == null)
                {
                    //从 Touched 集合里拿
                    objectModel = this.TouchedComplexMembers.FindValue(navigationNode.Property);

                    if (objectModel == null)
                    {
                        objectModel = this.GenComplexObjectModel(navigationDescriptor as ComplexPropertyDescriptor, navigationNode, queryModel);
                    }
                    else
                    {
                        //如果在先 touched 后 Include 导航属性会运行到这里，如：query.Where(a => a.City.Name == "BeiJing").Include(a => a.City)
                        //替换 touched 解析的 filters。因为计算 QueryPlan 时计算键 hash 时是没有包含 touched 时候的解析的源 ContextFilters 和 GlobalFilters，防止 ContextFilters 和 GlobalFilters 有动态修改导致缓存的 QueryPlan 与实际运行结果不一致
                        objectModel.Filters.Clear();
                        objectModel.Filters.AddRange(navigationNode.ContextFilters.Concat(navigationNode.GlobalFilters).Select(a => this.ParseCondition(a, objectModel, queryModel.ScopeTables, queryModel)));
                        this.TouchedComplexMembers.Remove(navigationNode.Property);
                        queryModel.TouchedTables.Remove(objectModel.AssociatedTable); // 此时 touched table 已经被 include 了，不是 touched table，需要从 TouchedTables 中移除
                    }

                    this.AddComplexMember(navigationNode.Property, objectModel);
                }
            }
            else
            {
                if (!handleCollection)
                {
                    this.IncludeCollections.Add(navigationNode);
                    return;
                }

                CollectionObjectModel collectionModel = this.GetCollectionMember(navigationNode.Property);
                if (collectionModel == null)
                {
                    Type collectionType = navigationDescriptor.PropertyType;
                    TypeDescriptor elementTypeDescriptor = EntityTypeContainer.GetDescriptor(collectionType.GetGenericArguments()[0]);
                    ComplexObjectModel elementModel = this.GenCollectionElementObjectModel(elementTypeDescriptor, navigationNode, queryModel);
                    collectionModel = new CollectionObjectModel(this.QueryOptions, this.ObjectType, navigationNode.Property, elementModel);
                    this.AddCollectionMember(navigationNode.Property, collectionModel);
                }
                else
                {
                    DbExpression condition = this.ParseCondition(navigationNode.Condition, collectionModel.ElementModel, queryModel.ScopeTables, queryModel);

                    var joinTable = collectionModel.ElementModel.AssociatedTable as DbJoinTableExpression;
                    joinTable.AppendCondition(condition);
                }

                objectModel = collectionModel.ElementModel;
            }

            for (int i = 0; i < navigationNode.ExcludedFields.Count; i++)
            {
                List<LinkedNode<MemberInfo>> fields = ExcludeFieldExtractor.Extract(navigationNode.ExcludedFields[i]);
                objectModel.ExcludePrimitiveMembers(fields);
            }

            if (navigationNode.Next != null)
            {
                objectModel.Include(navigationNode.Next, queryModel, handleCollection);
            }
        }
        public void SetupCollection(QueryModel queryModel)
        {
            for (int i = 0; i < this.IncludeCollections.Count; i++)
            {
                NavigationNode navigationNode = this.IncludeCollections[i];
                this.Include(navigationNode, queryModel, true);
            }

            foreach (var kv in this.ComplexMembers)
            {
                var memberObjectModel = kv.Value as ComplexObjectModel;
                memberObjectModel.SetupCollection(queryModel);
            }
        }
        public void SetupFilters(bool ignoreFilters)
        {
            if (ignoreFilters)
                return;

            this.SetupFilters();

            foreach (var kv in this.ComplexConstructorParameters)
            {
                kv.Value.SetupFilters(ignoreFilters);
            }

            foreach (var kv in this.ComplexMembers)
            {
                kv.Value.SetupFilters(ignoreFilters);
            }

            foreach (var kv in this.CollectionMembers)
            {
                kv.Value.ElementModel.SetupFilters(ignoreFilters);
            }

            foreach (var kv in this.TouchedComplexMembers)
            {
                kv.Value.SetupFilters(ignoreFilters);
            }
        }

        public bool HasMany()
        {
            if (this.IncludeCollections.Count > 0)
                return true;

            foreach (var kv in this.ComplexMembers)
            {
                var memberObjectModel = kv.Value as ComplexObjectModel;
                if (memberObjectModel.HasMany())
                {
                    return true;
                }
            }

            return false;
        }

        public void ExcludePrimitiveMember(LinkedNode<MemberInfo> memberLink)
        {
            MemberInfo memberInfo = memberLink.Value;
            memberInfo = memberInfo.AsReflectedMemberOf(this.ObjectType);

            if (this.GetPrimitiveMember(memberInfo) != null)
            {
                if (memberLink.Next != null)
                {
                    //a.Name.Length, a.Age.Value ....
                    throw new NotSupportedException($"Not support exclude field '{memberLink.Value.Name}.{memberLink.Next.Value.Name}'.");
                }

                if (this._excludedFields == null)
                    this._excludedFields = new HashSet<MemberInfo>();

                this._excludedFields.Add(memberInfo);
                return;
            }

            if (memberLink.Next == null)
            {
                throw new NotSupportedException($"Not support exclude field '{memberLink.Value.Name}'.");
            }

            ComplexObjectModel complexMemberObjectModel = this.ComplexMembers.FindValue(memberInfo);
            if (complexMemberObjectModel != null)
            {
                //a.City.Name
                complexMemberObjectModel.ExcludePrimitiveMember(memberLink.Next);
                return;
            }

            if (memberInfo.DeclaringType == this.ObjectType && this.ObjectType.IsAnonymousType())
            {
                //(person, city) => new { Person = person, City = city }
                ComplexObjectModel complexConstructorParameterObjectModel = this.ComplexConstructorParameters.Where(a => a.Key.Name == memberInfo.Name).Select(a => a.Value).FirstOrDefault();
                if (complexConstructorParameterObjectModel != null)
                {
                    //a.Person.Name
                    complexConstructorParameterObjectModel.ExcludePrimitiveMember(memberLink.Next);
                    return;
                }
            }

            throw new NotSupportedException($"Not support exclude field '{memberLink.Value.Name}'.");
        }
        public void ExcludePrimitiveMembers(IEnumerable<LinkedNode<MemberInfo>> memberLinks)
        {
            foreach (var memberLink in memberLinks)
            {
                this.ExcludePrimitiveMember(memberLink);
            }
        }
        bool IsExcludedMember(MemberInfo memberInfo)
        {
            if (this._excludedFields == null)
                return false;

            return this._excludedFields.Contains(memberInfo);
        }
        bool IsExcludedMember(ParameterInfo parameterInfo)
        {
            foreach (var map in this.ConstructorDescriptor.MemberParameterMap)
            {
                if (map.Value != parameterInfo) continue;
                return IsExcludedMember(map.Key);
            }
            return false;
        }

        public ComplexObjectModel TryTouchComplexProperty(MemberInfo property, QueryModel queryModel)
        {
            //从 Touched 集合里拿
            ComplexObjectModel objectModel = this.TouchedComplexMembers.FindValue(property);

            if (objectModel != null)
                return objectModel;

            if (queryModel == null)
                return null;

            //创建并放到 TouchedComplexMembers 集合中
            TypeDescriptor descriptor = EntityTypeContainer.GetDescriptor(this.ObjectType);
            PropertyDescriptor navigationDescriptor = descriptor.GetPropertyDescriptor(property);
            if (navigationDescriptor.Definition.Kind != TypeKind.Complex)
            {
                return null;
            }

            //注：这里获取的 filters 未参与 QueryPlan 的存储键计算，所以，如果存在 Touched 的情况，不能缓存生成的 QueryPlan
            List<LambdaExpression> globalFilters = EntityTypeContainer.GetDescriptor(navigationDescriptor.PropertyType).Definition.Filters.ToList();
            List<LambdaExpression> contextFilters = this._queryContext.DbContextProvider.GetQueryFilters(navigationDescriptor.PropertyType);

            objectModel = this.GenComplexObjectModel(navigationDescriptor as ComplexPropertyDescriptor, globalFilters, contextFilters, queryModel);
            queryModel.TouchedTables.Add(objectModel.AssociatedTable); //将 touched table 记录
            this.TouchedComplexMembers.Add(property, objectModel);
            return objectModel;
        }
        ComplexObjectModel GenComplexObjectModel(ComplexPropertyDescriptor navigationDescriptor, NavigationNode navigationNode, QueryModel queryModel)
        {
            return this.GenComplexObjectModel(navigationDescriptor, navigationNode.GlobalFilters, navigationNode.ContextFilters, queryModel);
        }
        ComplexObjectModel GenComplexObjectModel(ComplexPropertyDescriptor navigationDescriptor, List<LambdaExpression> globalFilters, List<LambdaExpression> contextFilters, QueryModel queryModel)
        {
            TypeDescriptor navigationTypeDescriptor = EntityTypeContainer.GetDescriptor(navigationDescriptor.PropertyType);

            DbTable dbTable = navigationTypeDescriptor.Table;
            DbTableExpression tableExp = new DbTableExpression(dbTable);
            string alias = queryModel.GenerateUniqueTableAlias();
            DbTableSegment joinTableSeg = new DbTableSegment(tableExp, alias, queryModel.FromTable.Table.Lock);

            ComplexObjectModel navigationObjectModel = navigationTypeDescriptor.GenObjectModel(alias, this._queryContext, queryModel.Options);
            navigationObjectModel.NullChecking = navigationObjectModel.PrimaryKey;

            PrimitivePropertyDescriptor foreignKeyPropertyDescriptor = navigationDescriptor.ForeignKeyProperty;
            DbExpression foreignKeyColumn = this.GetPrimitiveMember(foreignKeyPropertyDescriptor.Property);

            DbExpression otherSideKeyExp = navigationObjectModel.GetPrimitiveMember(navigationDescriptor.GetOtherSideProperty(navigationTypeDescriptor));

            //a.ForeignKey == otherSide.AssociatedKey
            DbExpression joinCondition = new DbEqualExpression(foreignKeyColumn, otherSideKeyExp);

            DbJoinType joinType = DbJoinType.LeftJoin;
            if (!foreignKeyPropertyDescriptor.IsNullable)
            {
                //如果外键是不可空类型，在满足一定条件下使用 InnerJoin 连接
                if (this.AssociatedTable is DbFromTableExpression)
                {
                    joinType = DbJoinType.InnerJoin;
                }
                else
                {
                    DbJoinTableExpression prevJoinTable = (DbJoinTableExpression)this.AssociatedTable;
                    //前一个表是 inner join，才能用 inner join，否则有可能查不出数据
                    if (prevJoinTable.JoinType == DbJoinType.InnerJoin)
                    {
                        joinType = DbJoinType.InnerJoin;
                    }
                }
            }

            DbJoinTableExpression joinTableExp = new DbJoinTableExpression(joinType, joinTableSeg, joinCondition);
            joinTableExp.AppendTo(this.AssociatedTable);

            navigationObjectModel.AssociatedTable = joinTableExp;

            navigationObjectModel.Filters.AddRange(contextFilters.Concat(globalFilters).Select(a => this.ParseCondition(a, navigationObjectModel, queryModel.ScopeTables, queryModel)));

            return navigationObjectModel;
        }
        ComplexObjectModel GenCollectionElementObjectModel(TypeDescriptor elementTypeDescriptor, NavigationNode navigationNode, QueryModel queryModel)
        {
            DbTable dbTable = elementTypeDescriptor.Table;
            DbTableExpression tableExp = new DbTableExpression(dbTable);
            string alias = queryModel.GenerateUniqueTableAlias();
            DbTableSegment joinTableSeg = new DbTableSegment(tableExp, alias, queryModel.FromTable.Table.Lock);

            ComplexObjectModel elementObjectModel = elementTypeDescriptor.GenObjectModel(alias, this._queryContext, queryModel.Options);
            elementObjectModel.NullChecking = elementObjectModel.PrimaryKey;

            ComplexPropertyDescriptor navigationDescriptor = elementTypeDescriptor.GetComplexPropertyDescriptorByPropertyType(this.ObjectType);
            DbExpression elementForeignKeyColumn = elementObjectModel.GetPrimitiveMember(navigationDescriptor.ForeignKeyProperty.Property);

            TypeDescriptor thisSideTypeDescriptor = EntityTypeContainer.GetDescriptor(this.ObjectType);
            DbExpression associatedThisSideKeyExp = this.GetPrimitiveMember(navigationDescriptor.GetOtherSideProperty(thisSideTypeDescriptor));

            DbExpression joinCondition = new DbEqualExpression(associatedThisSideKeyExp, elementForeignKeyColumn);
            DbJoinTableExpression joinTableExp = new DbJoinTableExpression(DbJoinType.LeftJoin, joinTableSeg, joinCondition);
            joinTableExp.AppendTo(this.AssociatedTable);

            elementObjectModel.AssociatedTable = joinTableExp;
            var condition = this.ParseCondition(navigationNode.Condition, elementObjectModel, queryModel.ScopeTables, queryModel);
            //Filter 的条件放到 join 条件里去
            joinTableExp.AppendCondition(condition);

            elementObjectModel.Filters.AddRange(navigationNode.ContextFilters.Concat(navigationNode.GlobalFilters).Select(a => this.ParseCondition(a, elementObjectModel, queryModel.ScopeTables, queryModel)));

            bool orderByPrimaryKeyExists = queryModel.Orderings.Where(a => a.Expression == this.PrimaryKey).Any();
            if (!orderByPrimaryKeyExists)
            {
                //结果集分组
                DbOrdering ordering = new DbOrdering(this.PrimaryKey, DbOrderType.Asc);
                queryModel.Orderings.Add(ordering);
            }

            return elementObjectModel;
        }

        public void SetNullChecking(DbExpression exp)
        {
            if (this.NullChecking == null)
            {
                if (this.PrimaryKey != null)
                    this.NullChecking = this.PrimaryKey;
                else
                    this.NullChecking = exp;
            }

            foreach (var item in this.ComplexConstructorParameters.Values)
            {
                item.SetNullChecking(exp);
            }

            foreach (var item in this.ComplexMembers.Values)
            {
                item.SetNullChecking(exp);
            }
        }

        DbExpression ParseCondition(LambdaExpression condition, ComplexObjectModel objectModel, StringSet scopeTables, QueryModel? queryModel)
        {
            if (condition == null)
                return null;

            return FilterPredicateParser.Parse(this._queryContext, condition, new ScopeParameterDictionary(1) { { condition.Parameters[0], objectModel } }, scopeTables, queryModel);
        }
        void SetupFilters()
        {
            DbJoinTableExpression joinTableExpression = this.AssociatedTable as DbJoinTableExpression;
            if (joinTableExpression != null)
            {
                this.Filters.ForEach(a =>
                {
                    joinTableExpression.Condition = joinTableExpression.Condition.And(a);
                });
            }
        }
    }
}
