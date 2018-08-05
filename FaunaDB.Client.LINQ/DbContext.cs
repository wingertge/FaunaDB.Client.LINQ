using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FaunaDB.LINQ.Client;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Modeling;
using FaunaDB.LINQ.Query;

namespace FaunaDB.LINQ
{
    public class DbContext : IDbContext
    {
        public Dictionary<Type, TypeConfiguration> Mappings { get; set; }

        public IFaunaClient Client { get; set; }

        public DbContext(IFaunaClient client, Dictionary<Type, TypeConfiguration> mappings)
        {
            Client = client;
            Mappings = mappings;
        }

        public virtual async Task<T> Query<T>(Expr query)
        {
            var result = await Client.Query(query);
            return result.Decode<T>(this);
        }

        public static IDbContextBuilder StartBuilding(IFaunaClient client)
        {
            return new DbContextBuilder(client);
        }
    }
}