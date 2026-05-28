using Cysharp.Threading.Tasks;
using Proyecto26;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private static readonly Dictionary<string, string> DefaultHeaders = new()
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json",

            ["x-sdk-core-name"] = "ewova-core",
            ["x-sdk-core-version"] = PackageInfo.Version,

            ["x-sdk-platform"] = "unity",
        };

        protected readonly Dictionary<string, string> AdditionalHeaders = new();
        protected virtual void ApplyProductHeaders(Dictionary<string, string> headers)
        {
            // example:
            //   headers["x-sdk-name"] = "learning-portfolio-sdk";
            //   headers["x-sdk-version"] = PackageInfo.Version;
        }

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<string> Get(string endpoint, CancellationToken ct = default) => Send<string>(endpoint, "GET", cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<T> Get<T>(string endpoint, CancellationToken ct = default) => Send<T>(endpoint, "GET", cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<string> Post(string endpoint, object body, CancellationToken ct = default) => Send<string>(endpoint, "POST", body, cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<T> Post<T>(string endpoint, object body, CancellationToken ct = default) => Send<T>(endpoint, "POST", body, cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<string> Put(string endpoint, object body, CancellationToken ct = default) => Send<string>(endpoint, "PUT", body, cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<T> Put<T>(string endpoint, object body, CancellationToken ct = default) => Send<T>(endpoint, "PUT", body, cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<string> Delete(string endpoint, CancellationToken ct = default) => Send<string>(endpoint, "DELETE", cancellationToken: ct);

        /// <inheritdoc cref="Send{T}(string, string, object, bool, CancellationToken)" />
        protected virtual UniTask<T> Delete<T>(string endpoint, CancellationToken ct = default) => Send<T>(endpoint, "DELETE", cancellationToken: ct);

        ///<exception cref="ValidationException"></exception>
        ///<exception cref="UnauthorizedException"></exception>
        ///<exception cref="ForbiddenException"></exception>
        ///<exception cref="NotFoundException"></exception>
        ///<exception cref="RateLimitException"></exception>
        ///<exception cref="ServerException"></exception>
        ///<exception cref="ApiException"></exception>
        private async UniTask<T> Send<T>(
            string endpoint,
            string method,
            object body = null,
            bool requireAuth = true,
            CancellationToken cancellationToken = default)
        {
            var req = CreateRequest(endpoint, method, body, requireAuth);

            try
            {
                var rsp = await RestClient
                    .Request(req)
                    .AsUniTask(cancellationToken);

                var text = rsp.Text;

                if (typeof(T) == typeof(string))
                    return (T)(object)text;

                if (string.IsNullOrWhiteSpace(text))
                    return default;

                return JsonConvert.DeserializeObject<T>(
                    text,
                    JsonSettings);
            }
            catch (RequestException ex)
            {
                throw ConvertRequestException(ex);
            }
            catch (Exception ex)
            {
                throw new ApiException(
                    0,
                    "Unknown Error",
                    inner: ex);
            }
        }


        private Exception ConvertRequestException(
            RequestException ex)
        {
            var statusCode =
                (HttpStatusCode)ex.StatusCode;

            var response = ex.Response;

            _logger.Exce(
                $"HTTP {(int)statusCode} Exception: {response}",
                ex);

            return statusCode switch
            {
                HttpStatusCode.BadRequest => new ValidationException(response),
                HttpStatusCode.Unauthorized => new UnauthorizedException(response),
                HttpStatusCode.Forbidden => new ForbiddenException(response),
                HttpStatusCode.NotFound => new NotFoundException(response),
                HttpStatusCode.TooManyRequests => new RateLimitException(response),
                >= HttpStatusCode.InternalServerError => new ServerException(statusCode, response),

                _ => new ApiException(
                    statusCode,
                    $"HTTP Error {(int)statusCode}",
                    response,
                    ex)
            };
        }

        private RequestHelper CreateRequest(
            string endpoint,
            string method,
            object body = null,
            bool requireAuth = true)
        {
            var headers = new Dictionary<string, string>(DefaultHeaders);

            if (requireAuth && AuthenticatedTokenSet != null)
            {
                headers["Authorization"] = $"Bearer {AuthenticatedTokenSet.AccessToken}";
            }

            foreach (var kv in AdditionalHeaders)
            {
                headers[kv.Key] = kv.Value;
            }

            ApplyProductHeaders(headers);

            return new RequestHelper
            {
                Uri = BuildUrl(endpoint),
                Method = method,
                Headers = headers,
                Body = body,
            };
        }

        private string BuildUrl(string endpoint)
        {
            return $"{_baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
        }
    }
}