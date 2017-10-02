using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FaunaDB.Client;
using FaunaDB.Query;

namespace FaunaDB.Extensions
{
    public class FaunaQueryableData<TData> : IQueryable<TData>
    {
        public IEnumerator<TData> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<TData>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Provider.Execute<IEnumerable>(Expression).GetEnumerator();
        }

        public Expression Expression { get; }
        public Type ElementType { get; }
        public IQueryProvider Provider { get; }

        public FaunaQueryableData(IFaunaClient client, object selector)
        {
            Provider = new FaunaQueryProvider(client, selector);
            Expression = Expression.Constant(this);
        }

        public FaunaQueryableData(IQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }
    }

    public class FaunaQueryableData<TData, TCurrent> : FaunaQueryableData<TData>, IIncludeQuery<TData, TCurrent> {
        public FaunaQueryableData(IFaunaClient client, Expr selector) : base(client, selector) { }

        public FaunaQueryableData(IQueryProvider provider, Expression expression) : base(provider, expression) { }
    }
}