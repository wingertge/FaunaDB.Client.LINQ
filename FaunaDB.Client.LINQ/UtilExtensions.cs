using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FaunaDB.Types;

[assembly:InternalsVisibleTo("FaunaDB.Client.LINQ.Tests")]

namespace FaunaDB.Extensions
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
            var nameAttr = propInfo.GetCustomAttribute<FaunaFieldAttribute>();
            var attrName = typeof(IReferenceType).IsAssignableFrom(propInfo.PropertyType)
                ? nameAttr?.Name
                : $"data.{nameAttr?.Name}";
            var propName = attrName ?? $"data.{propInfo.Name}";
            return propName;
        }
    }
}