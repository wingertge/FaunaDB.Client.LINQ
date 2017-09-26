using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using FaunaDB.Client;
using FaunaDB.Query;
using static FaunaDB.Query.Language;

namespace FaunaDB.Extensions
{
    public static class FaunaClientExtensions
    {
        private static Expr[] ObjToParamsOrSingle(object obj)
        {
            return obj.GetType().Name.StartsWith("Tuple") || obj.GetType().Name.StartsWith("ValueTuple")
                ? obj.GetType().GetProperties().Select(a => a.GetValue(obj).ToFaunaObjOrPrimitive()).ToArray()
                : new[] { obj.ToFaunaObjOrPrimitive() };
        }

        public static IQueryable<T> Query<T>(this FaunaClient client, Expression<Func<T, bool>> selector)
        {
            if (!(selector.Body is BinaryExpression binary)) throw new ArgumentException("Index selector must be binary expression.");

            return new FaunaQueryableData<T>(client, Map(WalkSelector(binary), @ref => Language.Get(@ref)));
        }

        private static Expr WalkSelector(BinaryExpression expression)
        {
            switch (expression.Left)
            {
                case BinaryExpression leftExp when expression.Right is BinaryExpression rightExp:
                    var left = WalkSelector(leftExp);
                    var right = WalkSelector(rightExp);

                    switch (expression.NodeType)
                    {
                        case ExpressionType.Or:
                        case ExpressionType.OrElse:
                            return Union(left, right);
                        case ExpressionType.And:
                        case ExpressionType.AndAlso:
                            return Intersection(left, right);
                        default:
                            throw new UnsupportedMethodException(expression.NodeType.ToString());
                    }
                case MemberExpression _ when expression.Right is ConstantExpression:
                case ConstantExpression _ when expression.Right is MemberExpression:
                    var member = expression.Left is MemberExpression mem ? mem : (MemberExpression) expression.Right;
                    var constant = expression.Right is ConstantExpression con ? con : (ConstantExpression) expression.Left;
                    var args = ObjToParamsOrSingle(constant.Value);
                    var indexAttr = member.GetPropertyInfo().GetCustomAttribute<IndexedAttribute>();
                    if(indexAttr == null) throw new ArgumentException("Can't use unindexed property for selector!");
                    var indexName = indexAttr.Name;
                    return Match(Index(indexName), args);
                case MemberExpression _ when expression.Right is MethodCallExpression:
                case MethodCallExpression _ when expression.Right is MemberExpression:
                    var member1 = expression.Left is MemberExpression mem1 ? mem1 : (MemberExpression)expression.Right;
                    var method = expression.Right is MethodCallExpression meth ? meth : (MethodCallExpression)expression.Left;
                    var methodValue = Expression.Lambda(method).Compile().DynamicInvoke();
                    var args1 = ObjToParamsOrSingle(methodValue);
                    var indexAttr1 = member1.GetPropertyInfo().GetCustomAttribute<IndexedAttribute>();
                    if (indexAttr1 == null) throw new ArgumentException("Can't use unindexed property for selector!");
                    var indexName1 = indexAttr1.Name;
                    return Match(Index(indexName1), args1);
            }

            throw new ArgumentException("Invalid format for selector. Has to be tree of index selector operations.");
        }

        public static IQueryable<T> Query<T>(this FaunaClient client, Expression<Func<T, object>> index,
            params Expr[] args)
        {
            if(!(index.Body is MemberExpression member)) throw new ArgumentException("Index selector must be a member.");

            var propInfo = member.GetPropertyInfo();
            var indexAttr = propInfo.GetCustomAttribute<IndexedAttribute>();
            if (indexAttr == null) throw new ArgumentException("Can't use unindexed property as selector!", nameof(index));
            var indexName = indexAttr.Name;

            return client.Query<T>(indexName, args);
        }

        public static IQueryable<T> Query<T>(this FaunaClient client, string index, params Expr[] args)
        {
            return new FaunaQueryableData<T>(client, Map(Match(Index(index), args), @ref => Language.Get(@ref)));
        }

        public static IQueryable<T> Query<T>(this FaunaClient client, string @ref)
        {
            return new FaunaQueryableData<T>(client, Language.Get(Ref(@ref)));
        }

        public static async Task<T> Create<T>(this FaunaClient client, T obj)
        {
            var objType = obj.GetType();
            var className = string.Concat(objType.Name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
            var result = await client.Query(Language.Create(Ref(Class(className), 1), Obj("data", obj.ToFaunaObj())));
            return obj is IReferenceType ? result.To<T>().Value : result.To<FaunaResult<T>>().Value.Data;
        }

        public static Task<T> Update<T>(this FaunaClient client, T obj) where T : IReferenceType
        {
            return client.Update(obj, obj.Id);
        }

        public static async Task<T> Update<T>(this FaunaClient client, T obj, string id)
        {
            var result = await client.Query(Language.Update(Ref(id), obj.ToFaunaObj()));
            return obj is IReferenceType ? result.To<T>().Value : result.To<FaunaResult<T>>().Value.Data;
        }

        public static Task<T> Upsert<T>(this FaunaClient client, T obj) where T : IReferenceType
        {
            return client.Upsert(obj, obj.Id);
        }

        public static async Task<T> Upsert<T>(this FaunaClient client, T obj, string id)
        {
            var objType = obj.GetType();
            var className = string.Concat(objType.Name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
            var result = await client.Query(If(Exists(id),
                Language.Update(id, obj.ToFaunaObj()),
                Language.Create(Ref(Class(className), 1), obj.ToFaunaObj())));
            return obj is IReferenceType ? result.To<T>().Value : result.To<FaunaResult<T>>().Value.Data;
        }

        public static Task Delete(this FaunaClient client, IReferenceType obj)
        {
            return client.Delete(obj.Id);
        }

        public static Task Delete(this FaunaClient client, string id)
        {
            return client.Query(Language.Delete(Ref(id)));
        }

        public static async Task<T> Get<T>(this FaunaClient client, string @ref)
        {
            var result = await client.Query(Language.Get(Ref(@ref)));
            return typeof(IReferenceType).IsAssignableFrom(typeof(T))
                ? result.To<T>().Value
                : result.To<FaunaResult<T>>().Value.Data;
        }
    }
}