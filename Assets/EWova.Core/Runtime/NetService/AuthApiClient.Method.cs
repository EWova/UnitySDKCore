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
    public partial class AuthApiClient : IDisposable
    {
        private struct RequestItem : IDisposable
        {
            public RequestItem(int index, RequestHelper requestHelper)
            {
                IsDisposed = false;
                Index = index;
                Helper = requestHelper;
                CreateAt = UnityEngine.Time.realtimeSinceStartup;
                DisposeAt = default;
            }
            public bool IsDisposed { get; private set; }
            public readonly int Index;
            public readonly RequestHelper Helper;
            public readonly float CreateAt;
            public float DisposeAt { get; private set; }
            public readonly TimeSpan ElapsedTime => TimeSpan.FromSeconds((IsDisposed ? DisposeAt : UnityEngine.Time.realtimeSinceStartup) - CreateAt);
            public void Dispose()
            {
                if (IsDisposed)
                    return;
                IsDisposed = true;
                DisposeAt = UnityEngine.Time.realtimeSinceStartup;
            }
        }

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
                throw new ObjectDisposedException(nameof(AuthApiClient));
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
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

            if (UnityEngine.Application.isPlaying)
                return await SendRestClient<T>(endpoint, method, body, linkedCts.Token);
            else
                return await SendUnityWebRequest<T>(endpoint, method, body, linkedCts.Token);
        }

        private async UniTask<T> SendRestClient<T>(
            string endpoint,
            string method,
            object body,
            CancellationToken token)
        {
            using var req = CreateRequest(endpoint, method, body);
            ResponseHelper rsp = null;
            try
            {
                if (_logger.InfoEnabled)
                {
                    var detail = new { req.Helper.Body, req.Helper.Headers };
                    _logger.Info($"{endpoint} {req.Index} {method} Request:{JsonConvert.SerializeObject(detail, Formatting.None, JsonSettings)}");
                }

                rsp = await RestClient
                    .Request(req.Helper)
                    .AsUniTask(token);

                var text = rsp.Text;

                if (typeof(T) == typeof(string))
                {
                    if (_logger.InfoEnabled)
                        _logger.Info($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:{text}");
                    return (T)(object)text;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (_logger.InfoEnabled)
                        _logger.Info($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response");
                    return default;
                }

                var desObj = JsonConvert.DeserializeObject<T>(text, JsonSettings);
                if (_logger.InfoEnabled)
                    _logger.Info($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:{text}");
                return desObj;
            }
            catch (RequestException ex)
            {
                if (_logger.ErrorEnabled)
                    _logger.Err($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:Exception {ex}");
                UnityEngine.Debug.LogException(ex);

                var statusCode = (HttpStatusCode)ex.StatusCode;

                var errorCode = Enum.IsDefined(typeof(ApiErrorCode), (int)ex.StatusCode)
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
                if (_logger.ErrorEnabled)
                    _logger.Err($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:JsonException {rsp?.Text}");
                UnityEngine.Debug.LogException(ex);

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
                if (_logger.WarnEnabled)
                    _logger.Warn($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:Cancelled");
                throw;
            }
            catch (Exception ex)
            {
                if (_logger.ErrorEnabled)
                    _logger.Err($"{endpoint} {req.Index} {method} Elapsed:{req.ElapsedTime.TotalMilliseconds:F0}ms Response:Unexpected {ex}");
                UnityEngine.Debug.LogException(ex);
                throw;
            }
        }

        private async UniTask<T> SendUnityWebRequest<T>(
            string endpoint,
            string method,
            object body,
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
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            var headers = CreateHeader();
            foreach (var kv in headers)
                request.SetRequestHeader(kv.Key, kv.Value);

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
                throw;
            }
            catch (Exception ex)
            {
                // UnityWebRequest error
                var code = request.responseCode;
                var response = request.downloadHandler?.text;

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

        private Dictionary<string, string> CreateHeader()
        {
            var headers = new Dictionary<string, string>(DefaultHeaders);

            if (TryGetValidAccessToken(out string accessToken))
                headers[key: "Authorization"] = $"Bearer {accessToken}";

            foreach (var kv in AdditionalHeaders)
                headers[kv.Key] = kv.Value;

            var productHeader = GetProductHeaders();
            if (productHeader != null)
            {
                foreach (var kv in productHeader)
                    headers[kv.Key] = kv.Value;
            }

            return headers;
        }

        private int _requestIndex = 0;
        private RequestItem CreateRequest(
            string endpoint,
            string method,
            object body = null,
            bool isAbsoluteUrl = false)
        {
            var headers = CreateHeader();

            return new RequestItem
            (
                index: _requestIndex++,
                requestHelper: new RequestHelper
                {
                    Uri = isAbsoluteUrl ? endpoint : BuildUrl(endpoint),
                    Method = method,
                    Headers = headers,
                    Body = body,
                }
            );
        }

        private string BuildUrl(string endpoint)
        {
            return $"{_baseUrl}/{endpoint.TrimStart('/')}";
        }
    }
}