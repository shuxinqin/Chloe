﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.Reflection;
using System.Data;
using System.Reflection;

namespace Chloe.KingbaseES
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        DbParamCollection _parameters = new DbParamCollection();

        public static readonly Dictionary<string, IPropertyHandler[]> PropertyHandlerDic = InitPropertyHandlers();
        public static readonly Dictionary<string, IMethodHandler[]> MethodHandlerDic = InitMethodHandlers();
        static readonly Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>> AggregateHandlerDic = InitAggregateHandlers();
        static readonly Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> BinaryWithMethodHandlersDic = InitBinaryWithMethodHandlers();
        static readonly Dictionary<Type, string> CastTypeMap;
        static readonly List<string> CacheParameterNames;

        static SqlGenerator()
        {
            Dictionary<Type, string> castTypeMap = new Dictionary<Type, string>();
            castTypeMap.Add(typeof(string), "varchar");
            castTypeMap.Add(typeof(byte), "smallint");
            castTypeMap.Add(typeof(Int16), "smallint");
            castTypeMap.Add(typeof(int), "integer");
            castTypeMap.Add(typeof(long), "bigint");
            castTypeMap.Add(typeof(float), "real");
            castTypeMap.Add(typeof(double), "double");
            castTypeMap.Add(typeof(decimal), "numeric");
            castTypeMap.Add(typeof(bool), "boolean");
            castTypeMap.Add(typeof(DateTime), "datetime");
            castTypeMap.Add(typeof(Guid), "uuid");
            CastTypeMap = PublicHelper.Clone(castTypeMap);


            int cacheParameterNameCount = 4 * 12;
            List<string> cacheParameterNames = new List<string>(cacheParameterNameCount);
            for (int i = 0; i < cacheParameterNameCount; i++)
            {
                string paramName = UtilConstants.ParameterNamePrefix + i.ToString();
                cacheParameterNames.Add(paramName);
            }
            CacheParameterNames = cacheParameterNames;
        }

        public SqlGenerator(KingbaseESSqlGeneratorOptions options) : base(options)
        {
            this.Options = options;
        }

        public new KingbaseESSqlGeneratorOptions Options { get; set; }

        public List<DbParam> Parameters { get { return this._parameters.ToParameterList(); } }

        protected override Dictionary<string, IPropertyHandler[]> PropertyHandlers { get; } = PropertyHandlerDic;
        protected override Dictionary<string, IMethodHandler[]> MethodHandlers { get; } = MethodHandlerDic;
        protected override Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>> AggregateHandlers { get; } = AggregateHandlerDic;
        protected override Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> BinaryWithMethodHandlers { get; } = BinaryWithMethodHandlersDic;

        public override DbExpression VisitSqlQuery(DbSqlQueryExpression exp)
        {
            this.BuildGeneralSql(exp);
            return exp;
        }
        public override DbExpression VisitInsert(DbInsertExpression exp)
        {
            string separator = "";

            this.SqlBuilder.Append("INSERT INTO ");
            this.AppendTable(exp.Table);
            this.SqlBuilder.Append("(");

            separator = "";
            foreach (var item in exp.InsertColumns)
            {
                this.SqlBuilder.Append(separator);
                this.QuoteName(item.Column.Name);
                separator = ",";
            }

            this.SqlBuilder.Append(")");

            this.SqlBuilder.Append(" VALUES(");
            separator = "";
            foreach (var item in exp.InsertColumns)
            {
                this.SqlBuilder.Append(separator);

                DbExpression valExp = DbExpressionExtension.StripInvalidConvert(item.Value);
                DbValueExpressionTransformer.Transform(valExp).Accept(this);
                separator = ",";
            }

            this.SqlBuilder.Append(")");

            if (exp.Returns.Count > 0)
            {
                this.SqlBuilder.Append(" returning ");
                separator = "";
                foreach (DbColumn returnColumn in exp.Returns)
                {
                    this.SqlBuilder.Append(separator);
                    this.QuoteName(returnColumn.Name);
                    separator = ",";
                }
            }

            return exp;
        }
        public override DbExpression VisitUpdate(DbUpdateExpression exp)
        {
            this.SqlBuilder.Append("UPDATE ");
            this.AppendTable(exp.Table);
            this.SqlBuilder.Append(" SET ");

            string separator = "";
            foreach (var item in exp.UpdateColumns)
            {
                this.SqlBuilder.Append(separator);

                this.QuoteName(item.Column.Name);
                this.SqlBuilder.Append("=");

                DbExpression valExp = DbExpressionExtension.StripInvalidConvert(item.Value);
                DbValueExpressionTransformer.Transform(valExp).Accept(this);

                separator = ",";
            }

            this.BuildWhereState(exp.Condition);

            return exp;
        }

        public override DbExpression VisitCoalesce(DbCoalesceExpression exp)
        {
            this.SqlBuilder.Append("NVL(");
            exp.CheckExpression.Accept(this);
            this.SqlBuilder.Append(",");
            exp.ReplacementValue.Accept(this);
            this.SqlBuilder.Append(")");

            return exp;
        }

        public override DbExpression VisitConvert(DbConvertExpression exp)
        {
            DbExpression stripedExp = DbExpressionExtension.StripInvalidConvert(exp);

            if (stripedExp.NodeType != DbExpressionType.Convert)
            {
                stripedExp.Accept(this);
                return exp;
            }

            exp = (DbConvertExpression)stripedExp;

            string dbTypeString;
            if (TryGetCastTargetDbTypeString(exp.Operand.Type, exp.Type, out dbTypeString, false))
            {
                BuildCastState(this, exp.Operand, dbTypeString);
            }
            else
                exp.Operand.Accept(this);

            return exp;
        }

        public override DbExpression VisitConstant(DbConstantExpression exp)
        {
            if (exp.Value == null || exp.Value == DBNull.Value)
            {
                this.SqlBuilder.Append("NULL");
                return exp;
            }

            var objType = exp.Value.GetType();
            if (objType == PublicConstants.TypeOfBoolean)
            {
                this.SqlBuilder.Append(((bool)exp.Value) ? "true" : "false");
                return exp;
            }
            else if (objType == PublicConstants.TypeOfString)
            {
                this.SqlBuilder.Append("N'", exp.Value, "'");
                return exp;
            }
            else if (objType.IsEnum)
            {
                this.SqlBuilder.Append(Convert.ChangeType(exp.Value, Enum.GetUnderlyingType(objType)).ToString());
                return exp;
            }
            else if (PublicHelper.IsNumericType(exp.Value.GetType()))
            {
                this.SqlBuilder.Append(exp.Value);
                return exp;
            }

            DbParameterExpression p = new DbParameterExpression(exp.Value);
            p.Accept(this);

            return exp;
        }
        public override DbExpression VisitParameter(DbParameterExpression exp)
        {
            object paramValue = exp.Value;
            Type paramType = exp.Type.GetUnderlyingType();
            DbType? dbType = exp.DbType;

            if (paramType.IsEnum)
            {
                paramType = Enum.GetUnderlyingType(paramType);
                if (paramValue != null)
                    paramValue = Convert.ChangeType(paramValue, paramType);
            }

            if (paramValue == null)
                paramValue = DBNull.Value;

            DbParam p = this._parameters.Find(paramValue, paramType, dbType);

            if (p != null)
            {
                this.SqlBuilder.Append(p.Name);
                return exp;
            }

            string paramName = GenParameterName(this._parameters.Count);
            p = DbParam.Create(paramName, paramValue, paramType);

            if (paramValue.GetType() == PublicConstants.TypeOfString)
            {
                if (dbType == DbType.AnsiStringFixedLength || dbType == DbType.StringFixedLength)
                    p.Size = ((string)paramValue).Length;
                else if (((string)paramValue).Length <= 4000)
                    p.Size = 4000;
            }

            if (dbType != null)
                p.DbType = dbType;

            this._parameters.Add(p);
            this.SqlBuilder.Append(paramName);
            return exp;
        }

        public override void QuoteName(string name)
        {
            if (this.Options.ConvertToLowercase)
                name = name.ToLower();

            base.QuoteName(name);
        }

        protected override void AppendColumnSegment(DbColumnSegment seg)
        {
            var e = DbValueExpressionTransformer.Transform(seg.Body);
            e.Accept(this);

            if (e.IsColumnAccessWithName(seg.Alias))
            {
                return;
            }

            this.SqlBuilder.Append(" AS ");
            this.QuoteName(seg.Alias);
        }

        void BuildGeneralSql(DbSqlQueryExpression exp)
        {
            this.SqlBuilder.Append("SELECT ");

            if (exp.IsDistinct)
                this.SqlBuilder.Append("DISTINCT ");

            List<DbColumnSegment> columns = exp.ColumnSegments;
            for (int i = 0; i < columns.Count; i++)
            {
                DbColumnSegment column = columns[i];
                if (i > 0)
                    this.SqlBuilder.Append(",");

                this.AppendColumnSegment(column);
            }

            this.SqlBuilder.Append(" FROM ");
            exp.Table.Accept(this);
            this.BuildWhereState(exp.Condition);
            this.BuildGroupState(exp);
            this.BuildOrderState(exp.Orderings);

            if (exp.SkipCount != null || exp.TakeCount != null)
            {
                int skipCount = exp.SkipCount ?? 0;
                long takeCount = long.MaxValue;
                if (exp.TakeCount != null)
                    takeCount = exp.TakeCount.Value;

                this.SqlBuilder.Append(" LIMIT ", takeCount.ToString(), " OFFSET ", skipCount.ToString());
            }

            DbTableSegment seg = exp.Table.Table;
            if (seg.Lock == LockType.UpdLock)
            {
                this.SqlBuilder.Append(" FOR UPDATE");
            }
            else if (seg.Lock == LockType.Unspecified || seg.Lock == LockType.NoLock)
            {
                //Do nothing.
            }
            else
                throw new NotSupportedException($"lock type: {seg.Lock.ToString()}");
        }

    }
}
