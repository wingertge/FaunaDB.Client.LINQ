using System.Threading.Tasks;
using FaunaDB.Client;
using FaunaDB.Query;
using FaunaDB.Types;

namespace FaunaDB.Extensions
{
    public class FaunaClientProxy : IFaunaClient
    {
        private readonly FaunaClient _client;
        public FaunaClientProxy(FaunaClient client)
        {
            _client = client;
        }

        public Task<Value> Query(Expr query)
        {
            return _client.Query(query);
        }
    }
}