using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Chloe
{
    static class UtilConstants
    {
        public const string DefaultTableAlias = "T";
        public const string DefaultColumnAlias = "C";

        public static readonly ConstantExpression Constant_Null_String = Expression.Constant(null, typeof(string));
        public static readonly ConstantExpression Constant_Empty_String = Expression.Constant(string.Empty);
        public static readonly ConstantExpression Constant_Null_Boolean = Expression.Constant(null, typeof(Boolean?));
        public static readonly ConstantExpression Constant_True = Expression.Constant(true);
        public static readonly ConstantExpression Constant_False = Expression.Constant(false);
        public static readonly UnaryExpression Convert_TrueToNullable = Expression.Convert(Expression.Constant(true), typeof(Boolean?));
        public static readonly UnaryExpression Convert_FalseToNullable = Expression.Convert(Expression.Constant(false), typeof(Boolean?));

        public static readonly Regex CamelCaseNameTranslateRegex = new Regex("[A-Z]");
    }
}
