using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FaunaDB.LINQ.Errors;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FaunaDB.LINQ.Client
{
    public class FaunaClient : IFaunaClient
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.None, ContractResolver = new CamelCasePropertyNamesContractResolver() };
        private readonly IClientIO _clientIo;

        public FaunaClient(string secret, string domain = "db.fauna.com", string scheme = "https", int? port = null, TimeSpan? timeout = null, IClientIO clientIO = null)
        {
            if (!port.HasValue)
                port = scheme == "https" ? 443 : 80;
            _clientIo = clientIO ?? new DefaultClientIO(new Uri( $"{scheme}://{domain}:{port}"), timeout ?? TimeSpan.FromSeconds(60.0), secret);
        }

        public async Task<RequestResult> Query(Expr query)
        {
            var json = JsonConvert.SerializeObject(query, Settings);
            var result = await _clientIo.DoRequest(HttpMethod.Post, "", json);
            RaiseForStatusCode(result);
            return result;
        }

        internal struct ErrorsWrapper
        {
            public IReadOnlyList<QueryError> Errors;
        }

        internal static void RaiseForStatusCode(RequestResult resultRequest)
        {
            var statusCode = resultRequest.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
                return;
            var errorsWrapper = JsonConvert.DeserializeObject<ErrorsWrapper>(resultRequest.ResponseContent);
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
            }
        }
    }
}