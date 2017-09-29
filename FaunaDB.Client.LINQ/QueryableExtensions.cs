using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace FaunaDB.Extensions
{
    public static class QueryableExtensions
    {
        internal static readonly MethodInfo PaginateMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(Paginate));

        public static IQueryable<T> Paginate<T>(this IQueryable<T> source, string fromRef = "",
            ListSortDirection sortDirection = ListSortDirection.Ascending, int size = 16, DateTime timeStamp = default(DateTime))
        {
            return source.Provider.CreateQuery<T>(Expression.Call(
                instance: null,
                method: PaginateMethodInfo.MakeGenericMethod(typeof(T)),
                arguments: new [] {
                    source.Expression,
                    Expression.Constant(fromRef),
                    Expression.Constant(sortDirection),
                    Expression.Constant(size),
                    Expression.Constant(timeStamp)
                }
            ));
        }

        internal static readonly MethodInfo FromQueryMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(FromQuery));

        public static IQueryable<TOut> FromQuery<TIn, TOut>(this IQueryable<TIn> source, Expression query)
        {
            return source.Provider.CreateQuery<TOut>(Expression.Call(
                instance: null,
                method: FromQueryMethodInfo.MakeGenericMethod(typeof(TIn), typeof(TOut)),
                arg0: source.Expression,
                arg1: Expression.Constant(query)
            ));
        }

        internal static readonly MethodInfo AtMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(At));

        public static IQueryable<T> At<T>(this IQueryable<T> source, DateTime timeStamp)
        {
            return source.Provider.CreateQuery<T>(Expression.Call(
                instance: null,
                method: FromQueryMethodInfo.MakeGenericMethod(typeof(T)),
                arg0: source.Expression,
                arg1: Expression.Constant(timeStamp)
            ));
        }

        public static async Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source)
        {
            var result = await ExecuteAsync<T, IEnumerable<T>>(source.Paginate(size: 1).GetAll());
            return result.FirstOrDefault();
        }

        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
        {
            return ExecuteAsync<T, List<T>>(source);
        }

        public static async Task<bool> AnyAsync<T>(this IQueryable<T> source)
        {
            var result = await ExecuteAsync<T, IEnumerable<T>>(source.Paginate(size: 1));
            return result.Any();
        }

        internal static readonly MethodInfo GetAllMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(GetAll));

        internal static IQueryable<T> GetAll<T>(this IQueryable<T> source)
        {
            return source.Provider.CreateQuery<T>(Expression.Call(
                instance: null,
                method: GetAllMethodInfo.MakeGenericMethod(typeof(T)),
                arguments: source.Expression
            ));
        }

        private static Task<TTarget> ExecuteAsync<TSource, TTarget>(IQueryable<TSource> source)
        {
            return source.Provider is FaunaQueryProvider provider
                ? provider.ExecuteAsync<TTarget>(source.Expression)
                : Task.Run(() => source.Provider.Execute<TTarget>(source.Expression));
        }
    }
}