using Chloe.Annotations;
using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.Reflection;
using System.Data;
using System.Reflection;

namespace Chloe.SonnetDB
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        DbParamCollection _parameters = new DbParamCollection();

        public static readonly Dictionary<string, IPropertyHandler[]> PropertyHandlerDic = InitPropertyHandlers();
        public static readonly Dictionary<string, IMethodHandler[]> MethodHandlerDic = InitMethodHandlers();
        static readonly Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>> AggregateHandlerDic = InitAggregateHandlers();
        static readonly Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> BinaryWithMethodHandlersDic = InitBinaryWithMethodHandlers();
        public static readonly Dictionary<Type, string> CastTypeMap;
        static readonly List<string> CacheParameterNames;

        static SqlGenerator()
        {
            Dictionary<Type, string> castTypeMap = new Dictionary<Type, string>();
            castTypeMap.Add(typeof(string), "STRING");
            castTypeMap.Add(typeof(byte), "INT");
            castTypeMap.Add(typeof(sbyte), "INT");
            castTypeMap.Add(typeof(Int16), "INT");
            castTypeMap.Add(typeof(ushort), "INT");
            castTypeMap.Add(typeof(int), "INT");
            castTypeMap.Add(typeof(uint), "INT");
            castTypeMap.Add(typeof(long), "INT");
            castTypeMap.Add(typeof(ulong), "DECIMAL");
            castTypeMap.Add(typeof(float), "FLOAT");
            castTypeMap.Add(typeof(double), "FLOAT");
            castTypeMap.Add(typeof(decimal), "DECIMAL");
            castTypeMap.Add(typeof(bool), "BOOL");
            castTypeMap.Add(typeof(DateTime), "DATETIME");
            castTypeMap.Add(typeof(Guid), "STRING");
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

        public SqlGenerator(SqlGeneratorOptions options) : base(options)
        {

        }

        public List<DbParam> Parameters { get { return this._parameters.ToParameterList(); } }

        protected override Dictionary<string, IPropertyHandler[]> PropertyHandlers { get; } = PropertyHandlerDic;
        protected override Dictionary<string, IMethodHandler[]> MethodHandlers { get; } = MethodHandlerDic;
        protected override Dictionary<string, Action<DbAggregateExpression, SqlGeneratorBase>> AggregateHandlers { get; } = AggregateHandlerDic;
        protected override Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> BinaryWithMethodHandlers { get; } = BinaryWithMethodHandlersDic;

        public override void QuoteName(string name)
        {
            this.SqlBuilder.Append(Utils.QuoteName(name));
        }

        protected override void AppendTable(DbTable table)
        {
            if (!string.IsNullOrEmpty(table.Schema))
                throw new NotSupportedException("SonnetDB does not support database schemas. See https://github.com/IoTSharp/SonnetDB/issues/232.");

            this.QuoteName(table.Name);
        }

        public override DbExpression VisitColumnAccess(DbColumnAccessExpression exp)
        {
            Utils.EnsureSupportedMappingType(exp.Type);
            return base.VisitColumnAccess(exp);
        }

        protected override void AppendTableSegment(DbTableSegment seg)
        {
            if (seg.Lock != LockType.Unspecified)
                throw new NotSupportedException("SonnetDB does not support query lock hints. See https://github.com/IoTSharp/SonnetDB/issues/233.");

            base.AppendTableSegment(seg);
        }

        public override DbExpression VisitInsert(DbInsertExpression exp)
        {
            if (exp.InsertColumns.Count == 0)
            {
                this.SqlBuilder.Append("INSERT INTO ");
                this.AppendTable(exp.Table);
                this.SqlBuilder.Append(" DEFAULT VALUES");
            }
            else
            {
                this.SqlBuilder.Append("INSERT INTO ");
                this.AppendTable(exp.Table);
                this.SqlBuilder.Append("(");
                string separator = "";
                foreach (var column in exp.InsertColumns)
                {
                    this.SqlBuilder.Append(separator);
                    this.QuoteName(column.Column.Name);
                    separator = ",";
                }
                this.SqlBuilder.Append(") SELECT ");
                separator = "";
                foreach (var column in exp.InsertColumns)
                {
                    this.SqlBuilder.Append(separator);
                    column.Value.Accept(this);
                    separator = ",";
                }
            }

            if (exp.Returns.Count > 0)
            {
                this.SqlBuilder.Append(" RETURNING ");
                string separator = "";
                foreach (DbColumn column in exp.Returns)
                {
                    this.SqlBuilder.Append(separator);
                    this.QuoteName(column.Name);
                    separator = ",";
                }
            }

            return exp;
        }

        public override DbExpression VisitBitAnd(DbBitAndExpression exp)
        {
            if (IsUInt64Expression(exp.Left) || IsUInt64Expression(exp.Right))
                throw new NotSupportedException("SonnetDB does not support UInt64 bitwise operations on its DECIMAL representation. See https://github.com/IoTSharp/SonnetDB/issues/259.");
            return base.VisitBitAnd(exp);
        }

        public override DbExpression VisitBitOr(DbBitOrExpression exp)
        {
            if (IsUInt64Expression(exp.Left) || IsUInt64Expression(exp.Right))
                throw new NotSupportedException("SonnetDB does not support UInt64 bitwise operations on its DECIMAL representation. See https://github.com/IoTSharp/SonnetDB/issues/259.");
            return base.VisitBitOr(exp);
        }

        static bool IsUInt64Expression(DbExpression expression)
        {
            Type type = expression.Type.GetUnderlyingType();
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);
            return type == typeof(ulong);
        }

        public override DbExpression VisitSubtract(DbSubtractExpression exp)
        {
            Utils.EnsureSupportedMappingType(exp.Type);
            return base.VisitSubtract(exp);
        }

        public override DbExpression VisitDivide(DbDivideExpression exp)
        {
            Type resultType = exp.Type.GetUnderlyingType();
            bool integer = resultType == typeof(byte) || resultType == typeof(sbyte) || resultType == typeof(short) || resultType == typeof(ushort) || resultType == typeof(int) || resultType == typeof(uint) || resultType == typeof(long);
            if (resultType == typeof(ulong))
            {
                this.SqlBuilder.Append("((");
                new DbConvertExpression(typeof(decimal), exp.Left).Accept(this);
                this.SqlBuilder.Append(" - (");
                new DbConvertExpression(typeof(decimal), exp.Left).Accept(this);
                this.SqlBuilder.Append(" % ");
                new DbConvertExpression(typeof(decimal), exp.Right).Accept(this);
                this.SqlBuilder.Append(")) / ");
                new DbConvertExpression(typeof(decimal), exp.Right).Accept(this);
                this.SqlBuilder.Append(")");
                return exp;
            }

            this.SqlBuilder.Append(integer ? "CAST((" : "(");
            if (integer)
                new DbConvertExpression(typeof(decimal), exp.Left).Accept(this);
            else
                exp.Left.Accept(this);
            this.SqlBuilder.Append(" / ");
            if (integer)
                new DbConvertExpression(typeof(decimal), exp.Right).Accept(this);
            else
                exp.Right.Accept(this);
            this.SqlBuilder.Append(integer ? ") AS INT)" : ")");
            return exp;
        }

        public override DbExpression VisitSqlQuery(DbSqlQueryExpression exp)
        {
            this.BuildGeneralSql(exp);
            return exp;
        }

        public override DbExpression VisitCoalesce(DbCoalesceExpression exp)
        {
            this.SqlBuilder.Append("COALESCE(");
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
            Utils.EnsureSupportedMappingType(exp.Type);

            if (exp.Operand is DbAggregateExpression || (exp.Operand is DbMethodCallExpression aggregateMethod && aggregateMethod.Method.DeclaringType == PublicConstants.TypeOfSql && (aggregateMethod.Method.Name == "Sum" || aggregateMethod.Method.Name == "Average" || aggregateMethod.Method.Name == "Max" || aggregateMethod.Method.Name == "Min" || aggregateMethod.Method.Name == "Count" || aggregateMethod.Method.Name == "LongCount")))
                throw new NotSupportedException("SonnetDB does not support CAST around an aggregate expression. See https://github.com/IoTSharp/SonnetDB/issues/254.");

            Type sourceType = exp.Operand.Type.GetUnderlyingType();
            if (exp.Type.GetUnderlyingType() == typeof(ulong) && (sourceType == typeof(float) || sourceType == typeof(double) || sourceType == typeof(decimal)))
            {
                this.SqlBuilder.Append("(");
                new DbConvertExpression(typeof(decimal), exp.Operand).Accept(this);
                this.SqlBuilder.Append(" - (");
                new DbConvertExpression(typeof(decimal), exp.Operand).Accept(this);
                this.SqlBuilder.Append(" % 1))");
                return exp;
            }

            string dbTypeString;
            if (TryGetCastTargetDbTypeString(exp.Operand.Type, exp.Type, out dbTypeString, false))
            {
                BuildCastState(this, exp.Operand, dbTypeString);
            }
            else
            {
                throw new NotSupportedException(PublicHelper.AppendNotSupportedCastErrorMsg(exp.Operand.Type, exp.Type));
            }

            return exp;
        }

        protected override DbExpression VisitDbFunctionMethodCallExpression(DbMethodCallExpression exp)
        {
            DbFunctionAttribute dbFunction = exp.Method.GetCustomAttribute<DbFunctionAttribute>();
            if (!string.IsNullOrEmpty(dbFunction.Schema))
                throw new NotSupportedException("SonnetDB does not support database schemas. See https://github.com/IoTSharp/SonnetDB/issues/232.");
            string functionName = string.IsNullOrEmpty(dbFunction.Name) ? exp.Method.Name : dbFunction.Name;

            this.QuoteName(functionName);
            this.SqlBuilder.Append("(");

            string c = "";
            foreach (DbExpression argument in exp.Arguments)
            {
                this.SqlBuilder.Append(c);
                argument.Accept(this);
                c = ",";
            }

            this.SqlBuilder.Append(")");

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
                this.SqlBuilder.Append(((bool)exp.Value) ? "TRUE" : "FALSE");
                return exp;
            }
            else if (objType == PublicConstants.TypeOfString)
            {
                this.SqlBuilder.Append("'", ((string)exp.Value).Replace("'", "''"), "'");
                return exp;
            }
            else if (objType.IsEnum)
            {
                return new DbConstantExpression(Convert.ChangeType(exp.Value, Enum.GetUnderlyingType(objType))).Accept(this);
            }
            else if (objType == typeof(decimal) || objType == typeof(ulong))
            {
                return new DbParameterExpression(exp.Value, exp.Type).Accept(this);
            }
            else if (PublicHelper.IsNumericType(exp.Value.GetType()))
            {
                this.SqlBuilder.Append(Convert.ToString(exp.Value, System.Globalization.CultureInfo.InvariantCulture));
                return exp;
            }

            DbParameterExpression p = new DbParameterExpression(exp.Value);
            p.Accept(this);

            return exp;
        }
        public override DbExpression VisitParameter(DbParameterExpression exp)
        {
            object paramValue = exp.Value;
            Utils.EnsureSupportedValue(paramValue);
            paramValue = Utils.NormalizeParameterValue(paramValue);
            Type paramType = exp.Type.GetUnderlyingType();
            Utils.EnsureSupportedMappingType(paramType);
            DbType? dbType = exp.DbType;
            if (paramType.IsEnum)
            {
                paramType = Enum.GetUnderlyingType(paramType);
                if (paramValue != null)
                    paramValue = Convert.ChangeType(paramValue, paramType);
            }

            if (paramType == typeof(sbyte) || paramType == typeof(ushort) || paramType == typeof(uint))
            {
                if (paramValue != null && paramValue != DBNull.Value)
                    paramValue = Convert.ToInt64(paramValue, System.Globalization.CultureInfo.InvariantCulture);
                paramType = typeof(long);
                dbType = DbType.Int64;
            }

            bool decimalParameter = paramType == typeof(decimal) || paramType == typeof(ulong);
            if (decimalParameter)
            {
                if (paramValue != null)
                    paramValue = Convert.ToString(paramValue, System.Globalization.CultureInfo.InvariantCulture);
                paramType = typeof(string);
                dbType = DbType.String;
            }

            if (paramValue == null)
                paramValue = DBNull.Value;

            DbParam p = this._parameters.Find(paramValue, paramType, dbType);

            if (p != null)
            {
                this.AppendParameter(p.Name, decimalParameter);
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
            this.AppendParameter(paramName, decimalParameter);
            return exp;
        }

        void AppendParameter(string parameterName, bool decimalParameter)
        {
            if (decimalParameter)
                this.SqlBuilder.Append("CAST(", parameterName, " AS DECIMAL)");
            else
                this.SqlBuilder.Append(parameterName);
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

            if (exp.SkipCount == null && exp.TakeCount == null)
                return;

            int skipCount = exp.SkipCount ?? 0;
            int takeCount = int.MaxValue;
            if (exp.TakeCount != null)
                takeCount = exp.TakeCount.Value;

            this.SqlBuilder.Append(" LIMIT ", takeCount.ToString(), " OFFSET ", skipCount.ToString());
        }


    }
}
