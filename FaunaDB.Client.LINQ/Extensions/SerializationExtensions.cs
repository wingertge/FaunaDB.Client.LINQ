using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FaunaDB.LINQ.Client;
using FaunaDB.LINQ.Modeling;
using FaunaDB.LINQ.Query;
using FaunaDB.LINQ.Types;
using Newtonsoft.Json.Linq;

namespace FaunaDB.LINQ.Extensions
{
    public static class SerializationExtensions
    {
        public static object ToFaunaObj(this object obj)
        {
            var fields = new Dictionary<string, object>();
            foreach (var prop in obj.GetType().GetProperties())
            {
                var propType = prop.PropertyType;
                if(propType.Name.StartsWith("CompositeIndex")) continue;
                var propValue = prop.GetValue(obj);
                var propName = prop.GetFaunaFieldName().Replace("data.", "");
                if (propName == "ref" || propName == "ts") continue;
                if (propValue == null) fields[propName] = null;
                switch (Type.GetTypeCode(propType))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Single:
                    case TypeCode.String:
                    case TypeCode.Boolean:
                        fields[propName] = propValue;
                        continue;
                    case TypeCode.UInt64:
                        fields[propName] = (ulong) propValue;
                        continue;
                    case TypeCode.Object:
                        var referenceAttr = prop.GetCustomAttribute(typeof(ReferenceAttribute));

                        if (typeof(IEnumerable).IsAssignableFrom(propType))
                        {
                            fields[propName] = referenceAttr == null 
                                ? ((IEnumerable)propValue).Cast<object>().Select(a => a.ToFaunaObjOrPrimitive()).ToArray()
                                : ((IEnumerable)propValue).Cast<IReferenceType>().Select(a => Language.Ref(a.Id)).ToArray();
                            continue;
                        }

                        fields[propName] = referenceAttr == null ? propValue.ToFaunaObj() : Language.Ref(((IReferenceType)propValue).Id);
                        continue;
                    case TypeCode.Char:
                        fields[propName] = propValue.ToString();
                        continue;
                    case TypeCode.DateTime:
                        fields[propName] = Language.Time(((DateTime) propValue).ToString("O"));
                        continue;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return Language.Obj(fields);
        }

        public static T Decode<T>(this RequestResult request)
        {
            return (T) Decode(request.ResponseContent, typeof(T));
        }

        public static dynamic Decode(this JToken value, Type type)
        {
            var obj = Activator.CreateInstance(type);

            foreach (var prop in type.GetProperties())
            {
                var faunaPath = prop.GetFaunaFieldName().Split('.');
                var current = value;
                var valid = true;
                foreach (var segment in faunaPath)
                {
                    if (current is JObject jObj && jObj.TryGetValue(segment, out current)) continue;
                    valid = false;
                    break;
                }
                if(!valid) continue;

                switch (Type.GetTypeCode(prop.PropertyType))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Single:
                    case TypeCode.String:
                    case TypeCode.Boolean:
                    case TypeCode.UInt64:
                        prop.SetValue(obj, current.ToObject(prop.PropertyType));
                        continue;
                    case TypeCode.Object:
                        if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                        {
                            if (typeof(IReferenceType).IsAssignableFrom(prop.PropertyType.GetElementType()))
                            {
                                var enumerable = current.ToObject<IEnumerable<JObject>>();
                                var result = enumerable.Select(item => Decode(item, prop.PropertyType.GetElementType())).ToList();
                                prop.SetValue(obj, (dynamic) result);
                            }
                            else prop.SetValue(obj, current.ToObject(prop.PropertyType));
                        }
                        else prop.SetValue(obj, Decode(current, prop.PropertyType));
                        continue;
                    case TypeCode.Char:
                        prop.SetValue(obj, current.ToObject<string>().ToCharArray()[0]);
                        continue;
                    case TypeCode.DateTime:
                        prop.SetValue(obj, DateTime.Parse(current.ToObject<Language.TimeStampV>().Ts.ToString()));
                        continue;
                    case TypeCode.Empty:
                        break;
                    case TypeCode.DBNull:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return obj;
        }

        internal static object ToFaunaObjOrPrimitive(this object obj)
        {
            if (obj == null) return null;
            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Object:
                    return obj.ToFaunaObj();
                case TypeCode.DBNull:
                    return null;
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.String:
                    return obj;
                case TypeCode.Char:
                    return obj.ToString();
                case TypeCode.DateTime:
                    return Language.Time(((DateTime)obj).ToString("O"));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}