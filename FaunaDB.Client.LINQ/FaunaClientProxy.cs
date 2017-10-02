using System.Threading.Tasks;
using FaunaDB.Client;
using FaunaDB.Query;
using FaunaDB.Types;

namespace FaunaDB.Extensions
{
    public class FaunaClientProxy : IFaunaClient
    {
        private readonly Client.FaunaClient _client;
        public FaunaClientProxy(Client.FaunaClient client)
        {
            _client = client;
        }

        public async Task<object> Query(Expr query)
        {
            return await _client.Query((dynamic)query);
        }

        public async Task<T> Query<T>(Expr query)
        {
            var result = await _client.Query((dynamic) query);
            return result.To<T>().Value;
        }
    }
}