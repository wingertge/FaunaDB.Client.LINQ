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
        public static object ToFaunaObj(this object obj, IDbContext context)
        {
            if (!context.Mappings.TryGetValue(obj.GetType(), out var mappings))
                throw new InvalidMappingException("Trying to use unregistered type.");

            var fields = new Dictionary<string, object>();
            foreach (var prop in obj.GetType().GetProperties())
            {
                var mapping = mappings[prop];

                if(new [] { DbPropertyType.Key, DbPropertyType.Timestamp, DbPropertyType.CompositeIndex }.Contains(mapping.Type)) continue;

                var propValue = prop.GetValue(obj);
                var propName = mapping.Name;

                if (propValue == null)
                {
                    fields[propName] = null;
                    continue;
                }

                var propType = prop.PropertyType;

                if (propType.GetTypeInfo().IsPrimitive || propType == typeof(string))
                    fields[propName] = propValue;
                else if (propType == typeof(DateTime))
                    fields[propName] = Language.Time(((DateTime)propValue).ToString("O"));
                else
                {
                    if (typeof(IEnumerable).IsAssignableFrom(propType))
                    {
                        fields[propName] = mapping.Type == DbPropertyType.Reference
                            ? ((IEnumerable) propValue).Cast<object>().Select(a => Language.Ref(SelectId(a, context))).ToArray()
                            : ((IEnumerable) propValue).Cast<object>().Select(a => a.ToFaunaObjOrPrimitive(context)).ToArray();
                        continue;
                    }

                    fields[propName] = mapping.Type == DbPropertyType.Reference
                        ? Language.Ref(SelectId(propValue, context))
                        : propValue.ToFaunaObj(context);
                }
            }

            return Language.Obj(fields);
        }

        private static object SelectId(object obj, IDbContext context)
        {
            if (!context.Mappings.TryGetValue(obj.GetType(), out var mapping))
                throw new InvalidMappingException($"Unregistered type \"{obj.GetType()}\" mapped as reference.");

            return mapping.Key.GetValue(obj);
        }

        public static T Decode<T>(this RequestResult request, IDbContext context)
        {
            return (T) Decode(JObject.Parse(request.ResponseContent), typeof(T), context);
        }

        public static dynamic Decode(this JToken value, Type type, IDbContext context)
        {
            var obj = Activator.CreateInstance(type);
            if(!context.Mappings.ContainsKey(type))
                throw new InvalidMappingException("Trying to decode to unregistered mapping.");

            var mappings = context.Mappings[type];

            foreach (var prop in type.GetProperties())
            {
                var faunaPath = mappings[prop].GetStringFieldPath();
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

                if (propType.IsPrimitive || prop.PropertyType == typeof(string))
                    prop.SetValue(obj, current.ToObject(prop.PropertyType));
                else if (prop.PropertyType == typeof(DateTime))
                    prop.SetValue(obj, DateTime.Parse(current.ToObject<Language.TimeStampV>().Ts.ToString()).ToUniversalTime());
                else
                {
                    if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                    {
                        var mapping = context.Mappings[prop.PropertyType.GetElementType() ?? throw new InvalidMappingException("Trying to decode unregistered type.")];
                        if (mapping.Any(a => a.Value.Type == DbPropertyType.Key))
                        {
                            var enumerable = current.ToObject<IEnumerable<JObject>>();
                            var result = enumerable.Select(item => Decode(item, prop.PropertyType.GetElementType(), context))
                                .ToList();
                            prop.SetValue(obj, (dynamic) result);
                        }
                        else prop.SetValue(obj, current.ToObject(prop.PropertyType));
                    }
                    else prop.SetValue(obj, Decode(current, prop.PropertyType, context));
                }
            }

            return obj;
        }

        internal static object ToFaunaObjOrPrimitive(this object obj, IDbContext context)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            if (type.GetTypeInfo().IsPrimitive || type == typeof(string))
                return obj;
            return type == typeof(DateTime) 
                ? Language.Time(((DateTime)obj).ToString("O")) 
                : obj.ToFaunaObj(context);
        }
    }
}