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
                if (propValue == null)
                {
                    fields[propName] = null;
                    continue;
                }

                if (propType.GetTypeInfo().IsPrimitive)
                    fields[propName] = propValue;
                else if (propType == typeof(DateTime))
                    fields[propName] = Language.Time(((DateTime) propValue).ToString("O"));
                else
                {
                    var referenceAttr = prop.GetCustomAttribute(typeof(ReferenceAttribute));

                    if (typeof(IEnumerable).IsAssignableFrom(propType))
                    {
                        fields[propName] = referenceAttr == null
                            ? ((IEnumerable)propValue).Cast<object>().Select(a => a.ToFaunaObjOrPrimitive()).ToArray()
                            : ((IEnumerable)propValue).Cast<IReferenceType>().Select(a => Language.Ref(a.Id)).ToArray();
                        continue;
                    }

                    fields[propName] = referenceAttr == null ? propValue.ToFaunaObj() : Language.Ref(((IReferenceType)propValue).Id);
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

                var propType = prop.PropertyType.GetTypeInfo();

                if (propType.IsPrimitive)
                    prop.SetValue(obj, current.ToObject(prop.PropertyType));
                else if (prop.PropertyType == typeof(DateTime))
                    prop.SetValue(obj, DateTime.Parse(current.ToObject<Language.TimeStampV>().Ts.ToString()));
                else
                {
                    if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                    {
                        if (typeof(IReferenceType).IsAssignableFrom(prop.PropertyType.GetElementType()))
                        {
                            var enumerable = current.ToObject<IEnumerable<JObject>>();
                            var result = enumerable.Select(item => Decode(item, prop.PropertyType.GetElementType()))
                                .ToList();
                            prop.SetValue(obj, (dynamic) result);
                        }
                        else prop.SetValue(obj, current.ToObject(prop.PropertyType));
                    }
                    else prop.SetValue(obj, Decode(current, prop.PropertyType));
                }
            }

            return obj;
        }

        internal static object ToFaunaObjOrPrimitive(this object obj)
        {
            if (obj == null) return null;
            switch (Convert.GetTypeCode(obj))
            {
                case TypeCode.Object:
                    return obj.ToFaunaObj();
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