using Cysharp.Threading.Tasks;
using Proyecto26;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient : IDisposable
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

        private readonly CancellationTokenSource _disposeCts = new();

        private bool _disposed;

        protected virtual void ApplyProductHeaders(Dictionary<string, string> headers)
        {
            // example:
            //   headers["x-sdk-name"] = "learning-portfolio-sdk";
            //   headers["x-sdk-version"] = PackageInfo.Version;
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (disposing)
            {
                try { _disposeCts.Cancel(); } catch { }
                _disposeCts.Dispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AuthenticatedApiClient));
        }

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<string> Get(
            string endpoint,
            CancellationToken ct = default)
            => Send<string>(endpoint, "GET", cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<T> Get<T>(
            string endpoint,
            CancellationToken ct = default)
            => Send<T>(endpoint, "GET", cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<string> Post(
            string endpoint,
            object body,
            CancellationToken ct = default)
            => Send<string>(endpoint, "POST", body, cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<T> Post<T>(
            string endpoint,
            object body,
            CancellationToken ct = default)
            => Send<T>(endpoint, "POST", body, cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<string> Put(
            string endpoint,
            object body,
            CancellationToken ct = default)
            => Send<string>(endpoint, "PUT", body, cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<T> Put<T>(
            string endpoint,
            object body,
            CancellationToken ct = default)
            => Send<T>(endpoint, "PUT", body, cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<string> Delete(
            string endpoint,
            CancellationToken ct = default)
            => Send<string>(endpoint, "DELETE", cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        protected virtual UniTask<T> Delete<T>(
            string endpoint,
            CancellationToken ct = default)
            => Send<T>(endpoint, "DELETE", cancellationToken: ct);

        ///<exception cref="ApiException"></exception>
        private async UniTask<T> Send<T>(
            string endpoint,
            string method,
            object body = null,
            bool requireAuth = true,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

            var req = CreateRequest(endpoint, method, body, requireAuth);

            ResponseHelper rsp = null;

            try
            {
                rsp = await RestClient
                    .Request(req)
                    .AsUniTask(linkedCts.Token);

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
                _logger.Exce($"HTTP Error: {ex.Response}", ex);

                var statusCode = (HttpStatusCode)ex.StatusCode;

                var errorCode = Enum.IsDefined(typeof(ApiErrorCode), ex.StatusCode)
                    ? (ApiErrorCode)ex.StatusCode
                    : (ex.StatusCode >= 500
                        ? ApiErrorCode.ServerError
                        : ApiErrorCode.Unknown);

                throw new ApiException(
                    errorCode: errorCode,
                    statusCode: statusCode,
                    endPoint: endpoint,
                    responseBody: ex.Response,
                    message: $"HTTP Error {(int)statusCode}: {statusCode}",
                    inner: ex
                );
            }
            catch (JsonException ex)
            {
                _logger.Exce($"Deserialization Error: {rsp?.Text}", ex);

                throw new ApiException(
                    errorCode: ApiErrorCode.DeserializationError,
                    statusCode: null,
                    endPoint: endpoint,
                    responseBody: rsp?.Text,
                    message: "Failed to deserialize response.",
                    inner: ex);
            }

            catch (OperationCanceledException)
            {
                _logger.Warn($"Request cancelled: {method} {endpoint}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Exce($"Unexpected Error: {ex}", ex);
                throw;
            }
        }

        private RequestHelper CreateRequest(
            string endpoint,
            string method,
            object body = null,
            bool requireAuth = true)
        {
            var headers = new Dictionary<string, string>(DefaultHeaders);

            if (requireAuth && IsUserAuthenticated)
                headers["Authorization"] = $"Bearer {AccessToken}";

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