﻿using Chloe.Visitors;
using Chloe.Reflection;

namespace Chloe.DbExpressions
{
    public static class DbExpressionExtension
    {
        public static DbExpression StripConvert(this DbExpression exp)
        {
            while (exp.NodeType == DbExpressionType.Convert)
            {
                exp = ((DbConvertExpression)exp).Operand;
            }

            return exp;
        }

        /// <summary>
        /// 去掉一些无效的转换
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public static DbExpression StripInvalidConvert(this DbExpression exp)
        {
            if (exp.NodeType != DbExpressionType.Convert)
                return exp;

            DbConvertExpression convertExpression = (DbConvertExpression)exp;

            if (convertExpression.Type == convertExpression.Operand.Type)
            {
                return StripInvalidConvert(convertExpression.Operand);
            }

            //(enumType)1
            if (convertExpression.Type.IsEnum)
            {
                Type enumUnderlyingType = Enum.GetUnderlyingType(convertExpression.Type);
                if (enumUnderlyingType == convertExpression.Operand.Type)
                {
                    //(enumType)1 --> 1
                    return StripInvalidConvert(convertExpression.Operand);
                }

                //(enumType)1 --> (Int16/Int32/Int64)1
                DbConvertExpression newExp = new DbConvertExpression(enumUnderlyingType, convertExpression.Operand);
                return StripInvalidConvert(newExp);
            }

            Type underlyingType;

            //(Nullable<T>)1
            if (convertExpression.Type.IsNullable(out underlyingType))//可空类型转换
            {
                if (underlyingType == convertExpression.Operand.Type)
                {
                    //T == convertExpression.Operand.Type
                    //(Nullable<T>)1 --> 1
                    return StripInvalidConvert(convertExpression.Operand);
                }

                //(Nullable<T>)1 --> (T)1
                DbConvertExpression newExp = new DbConvertExpression(underlyingType, convertExpression.Operand);
                return StripInvalidConvert(newExp);
            }

            if (!exp.Type.IsEnum)
            {
                //(Int16/Int32/Int64)TEnum
                if (convertExpression.Operand.Type.IsEnum)
                {
                    //(Int16/Int32/Int64)TEnum --> TEnum
                    return StripInvalidConvert(convertExpression.Operand);
                }

                //(Int16/Int32/Int64)Nullable<TEnum>
                if (convertExpression.Operand.Type.IsNullable(out underlyingType) && underlyingType.IsEnum)
                {
                    //(Int16/Int32/Int64)Nullable<TEnum> --> TEnum
                    return StripInvalidConvert(convertExpression.Operand);
                }
            }

            //float long double and so on
            if (exp.Type.IsValueType)
            {
                if (convertExpression.Operand.Type.IsNullable(out underlyingType) && underlyingType == exp.Type)
                {
                    //(T)Nullable<T> --> T
                    return StripInvalidConvert(convertExpression.Operand);
                }
            }

            //如果是子类向父类转换
            if (exp.Type.IsAssignableFrom(convertExpression.Operand.Type))
                return StripInvalidConvert(convertExpression.Operand);

            return convertExpression;
        }


        /// <summary>
        /// 尝试将 exp 转换成 DbParameterExpression。
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public static bool TryConvertToParameterExpression(this DbMemberAccessExpression exp, out DbParameterExpression val)
        {
            val = null;
            if (!exp.IsEvaluable())
                return false;

            //求值
            val = exp.ConvertToParameterExpression();
            return true;
        }
        /// <summary>
        /// 对 memberExpression 进行求值
        /// </summary>
        /// <param name="memberExpression"></param>
        /// <returns>返回 DbParameterExpression</returns>
        public static DbParameterExpression ConvertToParameterExpression(this DbMemberAccessExpression memberExpression)
        {
            //求值
            object val = Evaluate(memberExpression);
            return new DbParameterExpression(val, memberExpression.Type);
        }

        public static bool IsEvaluable(this DbExpression expression)
        {
            return DbExpressionEvaluableJudgment.IsEvaluable(expression);
        }
        public static object Evaluate(this DbExpression exp)
        {
            return DbExpressionEvaluator.Evaluate(exp);
        }


        /// <summary>
        /// 判定 exp 返回值肯定是 null
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="regardEmptyStringAsNull">是否将空字符串视为 null</param>
        /// <returns></returns>
        public static bool AffirmExpressionRetValueIsNull(this DbExpression exp, bool regardEmptyStringAsNull = false)
        {
            exp = DbExpressionExtension.StripConvert(exp);

            if (exp.NodeType == DbExpressionType.Constant)
            {
                var c = (DbConstantExpression)exp;
                return IsNullValue(c.Value, regardEmptyStringAsNull);
            }

            if (exp.NodeType == DbExpressionType.Parameter)
            {
                var p = (DbParameterExpression)exp;
                return IsNullValue(p.Value, regardEmptyStringAsNull);
            }

            return false;
        }

        static bool IsNullValue(object obj, bool regardEmptyStringAsNull)
        {
            if (obj == null || obj == DBNull.Value)
                return true;

            if (regardEmptyStringAsNull)
            {
                string stringValue = obj as string;
                if (stringValue != null && stringValue == string.Empty)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判定 exp 返回值肯定不是 null
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public static bool AffirmExpressionRetValueIsNotNull(this DbExpression exp)
        {
            exp = DbExpressionExtension.StripConvert(exp);

            if (exp.NodeType == DbExpressionType.Constant)
            {
                var c = (DbConstantExpression)exp;
                return c.Value != null && c.Value != DBNull.Value;
            }

            if (exp.NodeType == DbExpressionType.Parameter)
            {
                var p = (DbParameterExpression)exp;
                return p.Value != null && p.Value != DBNull.Value;
            }

            return false;
        }

        public static DbExpression And(this DbExpression left, DbExpression right)
        {
            if (left == null)
                return right;

            if (right == null)
                return left;

            return new DbAndExpression(left, right);
        }
        public static DbExpression And(this DbExpression left, List<DbExpression> right)
        {
            for (int i = 0; i < right.Count; i++)
            {
                left = left.And(right[i]);
            }

            return left;
        }
        public static DbExpression And(this List<DbExpression> expressions)
        {
            return And(null, expressions);
        }

        public static bool IsColumnAccessWithName(this DbExpression exp, string name)
        {
            if (exp.NodeType == DbExpressionType.ColumnAccess)
            {
                DbColumnAccessExpression columnAccessExp = (DbColumnAccessExpression)exp;
                if (columnAccessExp.Column.Name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
