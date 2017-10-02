using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using FaunaDB.Client;

namespace FaunaDB.Extensions
{
    public class DefaultClientIO : IClientIO
    {
        private readonly HttpClient _client;
        private readonly string _authHeader;

        internal DefaultClientIO(HttpClient client, string secret)
        {
            _authHeader = AuthString(secret);
            _client = client;
        }

        public DefaultClientIO(Uri domain, TimeSpan timeout, string secret)
        {
            Console.WriteLine($"Address {domain}");
            _client = new HttpClient
            {
                BaseAddress = domain,
                Timeout = timeout
            };
            _client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("X-FaunaDB-API-Version", "2.0");
            _authHeader = AuthString(secret);
        }

        public IClientIO NewSessionClient(string secret)
        {
            return new DefaultClientIO(_client, secret);
        }

        public Task<RequestResult> DoRequest(HttpMethodKind method, string path, string data, IReadOnlyDictionary<string, string> query = null)
        {
            return DoRequestAsync(method, path, data, query);
        }

        private async Task<RequestResult> DoRequestAsync(HttpMethodKind method, string path, string data, IReadOnlyDictionary<string, string> query = null)
        {
            var stringContent = data == null ? null : new StringContent(data);
            var str = query == null ? null : QueryString(query);
            if (str != null)
                path = $"{path}?{str}";
            var startTime = DateTime.UtcNow;
            var httpResponse = await this._client.SendAsync(new HttpRequestMessage(new HttpMethod(method.Name()), path)
            {
                Content = stringContent,
                Headers = {
          Authorization = new AuthenticationHeaderValue("Basic", this._authHeader)
        }
            }).ConfigureAwait(false);
            var contentEncoding = httpResponse.Content.Headers.ContentEncoding;
            var func = (Func<string, bool>)(encoding => encoding == "gzip");
            string responseContent;
            if (contentEncoding.Any(func))
                responseContent = await DecompressGZip(httpResponse.Content).ConfigureAwait(false);
            else
                responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var utcNow = DateTime.UtcNow;
            return new RequestResult(method, path, query, data, responseContent, (int)httpResponse.StatusCode, ToDictionary(httpResponse.Headers), startTime, utcNow);
        }

        private static async Task<string> DecompressGZip(HttpContent content)
        {
            string str;
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                {
                    using (var streamReader = new StreamReader((Stream)gzipStream))
                        str = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
            return str;
        }

        private static IReadOnlyDictionary<string, IEnumerable<string>> ToDictionary(HttpResponseHeaders headers)
        {
            return headers.ToDictionary(a => a.Key, a => a.Value);
        }

        /// <summary>Encodes secret string using base 64.</summary>
        private static string AuthString(string secret)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(secret));
        }

        /// <summary>Convert query parameters to a URL string.</summary>
        private static string QueryString(IReadOnlyDictionary<string, string> query)
        {
            var queryString = HttpUtility.ParseQueryString("");
            foreach (var keyValuePair in query)
                queryString[keyValuePair.Key] = keyValuePair.Value;
            return queryString.ToString();
        }
    }
}