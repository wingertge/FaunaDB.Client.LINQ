using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace FaunaDB.LINQ.Client
{
    public interface IClientIO
    {
        IClientIO NewSessionClient(string secret);
        Task<RequestResult> DoRequest(HttpMethod method, string path, string data, IReadOnlyDictionary<string, string> query = null);
    }
}