using System;
using System.Threading.Tasks;
using FaunaDB.Client;
using Newtonsoft.Json;

namespace FaunaDB.Extensions
{
    public class FaunaClient : IFaunaClient
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None };
        private readonly IClientIO _clientIo;

        public FaunaClient(string secret, string domain = "db.fauna.com", string scheme = "https", int? port = null, TimeSpan? timeout = null, IClientIO clientIO = null)
        {
            if (!port.HasValue)
                port = scheme == "https" ? 443 : 80;
            _clientIo = clientIO ?? new DefaultClientIO(new Uri( $"{scheme}://{domain}:{port}"), timeout ?? TimeSpan.FromSeconds(60.0), secret);
        }

        public async Task<object> Query(Expr query)
        {
            var json = JsonConvert.SerializeObject(query, Settings);
            var result = await _clientIo.DoRequest(HttpMethodKind.Post, "", json);
            RaiseForStatusCode(result);
            return JsonConvert.DeserializeObject(result.RequestContent);
        }

        public async Task<T> Query<T>(Expr query)
        {
            var json = JsonConvert.SerializeObject(query, Settings);
            var result = await _clientIo.DoRequest(HttpMethodKind.Post, "", json);
            RaiseForStatusCode(result);
            return result.Decode<T>();
        }

        internal static void RaiseForStatusCode(RequestResult resultRequest)
        {
            var statusCode = resultRequest.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
                return;
            /*Client.FaunaClient.ErrorsWrapper errorsWrapper = JsonConvert.DeserializeObject<Client.FaunaClient.ErrorsWrapper>(resultRequest.ResponseContent);
            var response = new QueryErrorResponse(statusCode, errorsWrapper.Errors);
            switch (statusCode)
            {
                case 400:
                    throw new BadRequest(response);
                case 401:
                    throw new Unauthorized(response);
                case 403:
                    throw new PermissionDenied(response);
                case 404:
                    throw new NotFound(response);
                case 500:
                    throw new InternalError(response);
                case 503:
                    throw new UnavailableError(response);
                default:
                    throw new UnknowException(response);
            }*/
        }
    }
}