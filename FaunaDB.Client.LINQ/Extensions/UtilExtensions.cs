using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FaunaDB.LINQ.Query;
using Newtonsoft.Json;

[assembly:InternalsVisibleTo("FaunaDB.Client.LINQ.Tests")]

namespace FaunaDB.LINQ.Extensions
{
    public static class UtilExtensions
    {

        internal static PropertyInfo GetPropertyInfo<TSource, TProperty>(this Expression<Func<TSource, TProperty>> propertyLambda)
        {
            if (!(propertyLambda.Body is MemberExpression member))
                throw new ArgumentException(
                    $"Expression '{propertyLambda}' refers to a method, not a property.");

            return GetPropertyInfo(member);
        }

        internal static PropertyInfo GetPropertyInfo(this MemberExpression memberExp)
        {
            var propInfo = memberExp.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException(
                    $"Member '{memberExp}' refers to a field, not a property.");

            return propInfo;
        }

        internal static string GetFaunaFieldName(this PropertyInfo propInfo)
        {
            var nameAttr = propInfo.GetCustomAttribute<JsonPropertyAttribute>();
            var attrName = new[] { "ref", "ts" }.Contains(nameAttr?.PropertyName)
                ? nameAttr?.PropertyName
                : $"data.{nameAttr?.PropertyName}";
            var propName = nameAttr != null ? attrName : $"data.{propInfo.Name.ToLowerUnderscored()}";
            return propName;
        }

        internal static string ToLowerUnderscored(this string s)
        {
            return string.Concat(s.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
        }

        internal static object[] GetFaunaFieldPath(this PropertyInfo propInfo)
        {
            return GetFaunaFieldName(propInfo).Split('.').ToArray<object>();
        }

        internal static object GetClassRef(this object obj)
        {
            return Language.Class(obj.GetType().Name.ToLowerUnderscored());
        }

        internal static T GetConstantValue<T>(this Expression expression)
        {
            return (T) ((ConstantExpression) expression).Value;
        }
    }
}