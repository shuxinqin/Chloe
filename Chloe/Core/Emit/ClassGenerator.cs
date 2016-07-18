using Chloe.Extensions;
using Chloe.Mapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;

namespace Chloe.Core.Emit
{
    public static class ClassGenerator
    {
        static readonly ConcurrentDictionary<Assembly,Lazy<ModuleBuilder>> _moduleBuilders = new ConcurrentDictionary<Assembly, Lazy<ModuleBuilder>>();

        public static Type CreateMRMType(MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Property && member.MemberType != MemberTypes.Field)
                throw new NotSupportedException();

            Type entityType = member.DeclaringType;

            var assembly = typeof(IMRM).Assembly;

            ModuleBuilder moduleBuilder = _moduleBuilders.GetOrAdd(assembly, new Lazy<ModuleBuilder>(() => {
                var assemblyName =
                              new AssemblyName(String.Format(CultureInfo.InvariantCulture, "ChloeMRMs-{0}", assembly.FullName));
                assemblyName.Version = new Version(1, 0, 0, 0);

                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                     assemblyName, AssemblyBuilderAccess.Run);
                return assemblyBuilder.DefineDynamicModule("ChloeMRMModule");
            },true)).Value;
            
            var typeAttributes = TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed;
            TypeBuilder tb = moduleBuilder.DefineType(string.Format("Chloe.Core.Mapper.MRMs.{0}_{1}_{2}", entityType.Name, member.Name, Guid.NewGuid().ToString("N")), typeAttributes, null, new Type[] { typeof(IMRM) });

            tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName);

            MethodBuilder methodBuilder = tb.DefineMethod("Map", MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis, typeof(void), new Type[] { typeof(object), typeof(IDataReader), typeof(int) });

            var il = methodBuilder.GetILGenerator();

            int parameStartIndex = 1;

            il.Emit(OpCodes.Ldarg_S, parameStartIndex);//将第一个参数 object 对象加载到栈顶
            il.Emit(OpCodes.Castclass, member.DeclaringType);//将 object 对象转换为强类型对象 此时栈顶为强类型的对象

            var readerMethod = DataReaderConstant.GetReaderMethod(ReflectionExtensions.GetPropertyOrFieldType(member));

            //ordinal
            il.Emit(OpCodes.Ldarg_S, parameStartIndex + 1);    //加载参数DataReader
            il.Emit(OpCodes.Ldarg_S, parameStartIndex + 2);    //加载 read ordinal
            il.EmitCall(OpCodes.Call, readerMethod, null);     //调用对应的 readerMethod 得到 value  reader.Getxx(ordinal);  此时栈顶为 value

            EmitHelper.SetValueIL(il, member); // object.XX = value; 此时栈顶为空

            il.Emit(OpCodes.Ret);   // 即可 return

            Type t = tb.CreateType();

            return t;
        }
    }
}
