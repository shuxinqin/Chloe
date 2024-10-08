﻿using Chloe.Reflection;
using System.Reflection;

namespace Chloe.DbExpressions
{
    public class DbMemberAccessExpression : DbExpression
    {
        MemberInfo _member;
        DbExpression _exp;

        public DbMemberAccessExpression(MemberInfo member, DbExpression exp) : base(DbExpressionType.MemberAccess)
        {
            if (member.MemberType != MemberTypes.Property && member.MemberType != MemberTypes.Field)
                throw new ArgumentException();

            this._member = member;
            this._exp = exp;
        }

        public override Type Type
        {
            get
            {
                return this._member.GetMemberType();
            }
        }

        public MemberInfo Member
        {
            get { return this._member; }
        }

        /// <summary>
        /// 字段或属性的包含对象
        /// </summary>
        public DbExpression Expression
        {
            get { return this._exp; }
        }

        public override T Accept<T>(DbExpressionVisitor<T> visitor)
        {
            return visitor.VisitMemberAccess(this);
        }
    }
}
