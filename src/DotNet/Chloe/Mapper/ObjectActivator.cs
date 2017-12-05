using Chloe.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chloe.Mapper
{
    public class ObjectActivator : IObjectActivator
    {
        int? _checkNullOrdinal;
        Func<IDataReader, ReaderOrdinalEnumerator, ObjectActivatorEnumerator, object> _instanceCreator;
        List<int> _readerOrdinals;
        List<IObjectActivator> _objectActivators;
        List<IValueSetter> _memberSetters;

        ReaderOrdinalEnumerator _readerOrdinalEnumerator;
        ObjectActivatorEnumerator _objectActivatorEnumerator;
        public ObjectActivator(Func<IDataReader, ReaderOrdinalEnumerator, ObjectActivatorEnumerator, object> instanceCreator, List<int> readerOrdinals, List<IObjectActivator> objectActivators, List<IValueSetter> memberSetters, int? checkNullOrdinal)
        {
            this._instanceCreator = instanceCreator;
            this._readerOrdinals = readerOrdinals;
            this._objectActivators = objectActivators;
            this._memberSetters = memberSetters;
            this._checkNullOrdinal = checkNullOrdinal;

            this._readerOrdinalEnumerator = new ReaderOrdinalEnumerator(readerOrdinals);
            this._objectActivatorEnumerator = new ObjectActivatorEnumerator(objectActivators);
        }

        public virtual object CreateInstance(IDataReader reader)
        {
            if (this._checkNullOrdinal != null)
            {
                if (reader.IsDBNull(this._checkNullOrdinal.Value))
                    return null;
            }

            this._readerOrdinalEnumerator.Reset();
            this._objectActivatorEnumerator.Reset();

            object obj = null;
            try
            {
                obj = this._instanceCreator(reader, this._readerOrdinalEnumerator, this._objectActivatorEnumerator);
            }
            catch (ChloeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (this._readerOrdinalEnumerator.CurrentOrdinal >= 0)
                {
                    throw new ChloeException(AppendErrorMsg(reader, this._readerOrdinalEnumerator.CurrentOrdinal, ex), ex);
                }

                throw;
            }

            IValueSetter memberSetter = null;
            try
            {
                int count = this._memberSetters.Count;
                for (int i = 0; i < count; i++)
                {
                    memberSetter = this._memberSetters[i];
                    if (!reader.IsDBNull(i))
                    {
                        memberSetter.SetValue(obj, reader);
                    }
                }
            }
            catch (ChloeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                MappingMemberBinder binder = memberSetter as MappingMemberBinder;
                if (binder != null)
                {
                    throw new ChloeException(AppendErrorMsg(reader, binder.Ordinal, ex), ex);
                }

                throw;
            }

            return obj;
        }

        public static string AppendErrorMsg(IDataReader reader, int ordinal, Exception ex)
        {
            string msg = null;
            if (reader.IsDBNull(ordinal))
            {
                msg = string.Format("Please make sure that the member of the column '{0}'({1},{2},{3}) map is nullable.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            }
            else if (ex is InvalidCastException)
            {
                msg = string.Format("Please make sure that the member of the column '{0}'({1},{2},{3}) map is the correct type.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            }
            else
                msg = string.Format("An error occurred while mapping the column '{0}'({1},{2},{3}). For details please see the inner exception.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            return msg;
        }
    }

    public class ObjectActivatorWithTracking : ObjectActivator
    {
        IDbContext _dbContext;
        public ObjectActivatorWithTracking(Func<IDataReader, ReaderOrdinalEnumerator, ObjectActivatorEnumerator, object> instanceCreator, List<int> readerOrdinals, List<IObjectActivator> objectActivators, List<IValueSetter> memberSetters, int? checkNullOrdinal, IDbContext dbContext)
            : base(instanceCreator, readerOrdinals, objectActivators, memberSetters, checkNullOrdinal)
        {
            this._dbContext = dbContext;
        }

        public override object CreateInstance(IDataReader reader)
        {
            object obj = base.CreateInstance(reader);

            if (obj != null)
                this._dbContext.TrackEntity(obj);

            return obj;
        }
    }
}
