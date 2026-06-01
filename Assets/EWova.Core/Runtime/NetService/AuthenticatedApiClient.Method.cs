using Cysharp.Threading.Tasks;
using Proyecto26;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using UnityEngine.Networking;

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

            ["x-sdk-platform"] = "unity",
            ["x-sdk-version"] = PackageInfo.Version,
        };

        protected readonly Dictionary<string, string> AdditionalHeaders = new();

        private readonly CancellationTokenSource _disposeCts = new();

        private bool _disposed;

        protected virtual Dictionary<string, string> GetProductHeaders()
        {
            // example:
            //   headers["x-sdk-name"] = "learning-portfolio-sdk";
            //   headers["x-sdk-version"] = PackageInfo.Version;

            return null;
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

            if (UnityEngine.Application.isPlaying)
                return await SendRestClient<T>(endpoint, method, body, requireAuth, linkedCts.Token);
            else
                return await SendUnityWebRequest<T>(endpoint, method, body, requireAuth, linkedCts.Token);
        }

        private async UniTask<T> SendRestClient<T>(
            string endpoint,
            string method,
            object body,
            bool requireAuth,
            CancellationToken token)
        {
            var req = CreateRequest(endpoint, method, body, requireAuth);

            ResponseHelper rsp = null;

            try
            {
                rsp = await RestClient
                    .Request(req)
                    .AsUniTask(token);

                var text = rsp.Text;

                if (typeof(T) == typeof(string))
                    return (T)(object)text;

                if (string.IsNullOrWhiteSpace(text))
                    return default;

                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
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

        private async UniTask<T> SendUnityWebRequest<T>(
            string endpoint,
            string method,
            object body,
            bool requireAuth,
            CancellationToken token)
        {
            string url = BuildUrl(endpoint);

            using var request = new UnityWebRequest(url, method);

            // body
            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body, JsonSettings);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);

                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            if (requireAuth && IsUserAuthenticated)
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: token);

                var text = request.downloadHandler.text;

                if (typeof(T) == typeof(string))
                    return (T)(object)text;

                if (string.IsNullOrWhiteSpace(text))
                    return default;

                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
            }
            catch (OperationCanceledException)
            {
                _logger.Warn($"Request cancelled: {method} {endpoint}");
                throw;
            }
            catch (Exception ex)
            {
                // UnityWebRequest error
                var code = request.responseCode;
                var response = request.downloadHandler?.text;

                _logger.Exce($"UnityWebRequest Error: {response}", ex);

                throw new ApiException(
                    errorCode: code >= 500 ? ApiErrorCode.ServerError : ApiErrorCode.Unknown,
                    statusCode: (HttpStatusCode)code,
                    endPoint: endpoint,
                    responseBody: response,
                    message: $"HTTP Error {code}",
                    inner: ex
                );
            }
            finally
            {
                request.Dispose();
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
                headers[kv.Key] = kv.Value;

            var productHeader = GetProductHeaders();
            if (productHeader != null)
            {
                foreach (var kv in productHeader)
                    headers[kv.Key] = kv.Value;
            }

            if (_logger.PrintLevel.HasFlag(Logger.Level.Info))
                _logger.Log($"Creating Request: {method} {endpoint} with body: {(body != null ? JsonConvert.SerializeObject(body, JsonSettings) : "null")} with headers: {JsonConvert.SerializeObject(headers)}");

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