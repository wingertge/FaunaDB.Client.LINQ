﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using FaunaDB.LINQ.Query;
using FaunaDB.LINQ.Types;

namespace FaunaDB.LINQ.Extensions
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

        internal static readonly MethodInfo IncludeMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(Include));

        public static IIncludeQuery<T, TSelected> Include<T, TSelected>(this IQueryable<T> source, Expression<Func<T, TSelected>> selector)
        {
            return new FaunaQueryableData<T, TSelected>(source.Provider, Expression.Call(
                instance: null,
                method: IncludeMethodInfo.MakeGenericMethod(typeof(T), typeof(TSelected)),
                arg0: source.Expression,
                arg1: selector
            ));
        }

        internal static readonly MethodInfo AlsoIncludeMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(AlsoInclude));

        public static IIncludeQuery<TOrigin, TSelected> AlsoInclude<TOrigin, TCurrent, TSelected>(this IIncludeQuery<TOrigin, TCurrent> source, Expression<Func<TCurrent, TSelected>> selector)
        {
            return new FaunaQueryableData<TOrigin, TSelected>(source.Provider, Expression.Call(
                instance: null,
                method: AlsoIncludeMethodInfo.MakeGenericMethod(typeof(TOrigin), typeof(TCurrent), typeof(TSelected)),
                arg0: source.Expression,
                arg1: selector
            ));
        }

        internal static readonly MethodInfo FromQueryMethodInfo =
            typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethod(nameof(FromQuery));

        public static IQueryable<TOut> FromQuery<TIn, TOut>(this IQueryable<TIn> source, Expr query)
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