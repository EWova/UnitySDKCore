using Cysharp.Threading.Tasks;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using UnityEngine.Networking;
using UnityEngine;

namespace EWova.Networking
{
    public partial class AuthApiClient
    {
        internal sealed class RequestTask : IDisposable
        {
            private readonly CancellationTokenSource _selfCts;
            private readonly CancellationTokenSource _linkedCts;

            public string Uri { get; }

            public int TaskId { get; }
            public string BaseUrl { get; }
            public string BackendUrlOrAbsUrl { get; }
            public string Method { get; }
            public Dictionary<string, string> Headers { get; }
            public string BodyString { get; }
            public string ContentType { get; }
            public bool IsAbsoluteUrl { get; }
            public Logger Logger { get; }
            public bool IsDisposed { get; private set; }
            public double CreatedAt { get; }
            public double DisposedAt { get; private set; }
            public CancellationToken CancellationToken { get; }
            public TimeSpan ElapsedTime => TimeSpan.FromSeconds((IsDisposed ? DisposedAt : Time.realtimeSinceStartupAsDouble) - CreatedAt);
            public RequestTask(
                int taskId,
                string baseUrl,
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl,
                string method,
                Dictionary<string, string> headers,
                string bodyString,
                string contentType,
                Logger logger,
                CancellationToken cancellationToken)
            {
                TaskId = taskId;
                BaseUrl = baseUrl;
                BackendUrlOrAbsUrl = backendUrlOrAbsoluteUrl;
                Method = method;
                Headers = headers;
                BodyString = bodyString;
                ContentType = contentType;
                IsAbsoluteUrl = isAbsoluteUrl;
                Logger = logger;

                CreatedAt = Time.realtimeSinceStartupAsDouble;

                _selfCts = new CancellationTokenSource();
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _selfCts.Token);

                CancellationToken = _linkedCts.Token;

                Uri = isAbsoluteUrl ? backendUrlOrAbsoluteUrl : $"{baseUrl}/{backendUrlOrAbsoluteUrl.TrimStart('/')}";
            }

            public void Cancel()
            {
                if (IsDisposed)
                    return;

                _selfCts.Cancel();
            }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                DisposedAt = Time.realtimeSinceStartupAsDouble;

                _linkedCts.Dispose();
                _selfCts.Dispose();
            }
        }

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public class UnitySdkHeader
        {
            [JsonProperty("coreVersion")]
            public string CoreVersion { get; set; } = string.Empty;

            [JsonProperty("packages")]
            public List<SdkPackageInfo> Packages { get; set; } = new();
        }

        public class SdkPackageInfo
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("version")]
            public string Version { get; set; } = string.Empty;
        }

        protected readonly Dictionary<string, string> AdditionalHeaders = new();

        /// <summary>
        /// 將會用到的套件資訊加入 list 中，最終會傳回 "X-Unity-Sdk" Header
        /// </summary>
        protected virtual void CollectPackages(List<SdkPackageInfo> list) { }

        #region Methods

        ///<exception cref="ApiException"></exception>
        public UniTask<string> Get(
            string endpoint,
            string acceptType = null,
            CancellationToken ct = default)
            => Send<string>(endpoint, "GET", acceptType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<T> Get<T>(
            string endpoint,
            string acceptType = null,
            CancellationToken ct = default)
            => Send<T>(endpoint, "GET", acceptType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<string> Post(
            string endpoint,
            object body,
            string acceptType = null,
            string contentType = null,
            CancellationToken ct = default)
            => Send<string>(endpoint, "POST", acceptType, body, contentType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<T> Post<T>(
            string endpoint,
            object body,
            string acceptType = null,
            string contentType = null,
            CancellationToken ct = default)
            => Send<T>(endpoint, "POST", acceptType, body, contentType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<string> Put(
            string endpoint,
            object body,
            string acceptType = null,
            string contentType = null,
            CancellationToken ct = default)
            => Send<string>(endpoint, "PUT", acceptType, body, contentType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<T> Put<T>(
            string endpoint,
            object body,
            string acceptType = null,
            string contentType = null,
            CancellationToken ct = default)
            => Send<T>(endpoint, "PUT", acceptType, body, contentType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<string> Delete(
            string endpoint,
            string acceptType = null,
            CancellationToken ct = default)
            => Send<string>(endpoint, "DELETE", acceptType, ct: ct);

        ///<exception cref="ApiException"></exception>
        public UniTask<T> Delete<T>(
            string endpoint,
            string acceptType = null,
            CancellationToken ct = default)
            => Send<T>(endpoint, "DELETE", acceptType, ct: ct);

        ///<exception cref="ApiException"></exception>
        internal UniTask<T> Send<T>(
            string urlOrEndpoint,
            string method,
            string acceptType = null,
            object body = null,
            string contentType = null,
            bool isAbsoluteUrl = false,
            Action<RequestTask> postProcRequestTask = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            RequestTask req = CreateRequestTask(
                urlOrEndpoint: urlOrEndpoint,
                method,
                acceptType,
                body,
                contentType,
                isAbsoluteUrl,
                cancellationToken: ct);

            postProcRequestTask?.Invoke(req);
            return SendUnityWebRequest<T>(req);
        }
        #endregion

        private int _requestIndex = 0;
        private RequestTask CreateRequestTask(
            string urlOrEndpoint,
            string method,
            string acceptType,
            object body,
            string contentType,
            bool isAbsoluteUrl,
            CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

            var headers = new Dictionary<string, string>();

            if (TryGetValidAccessToken(out string accessToken))
                headers[key: "Authorization"] = $"Bearer {accessToken}";

            foreach (var kv in AdditionalHeaders)
                headers[kv.Key] = kv.Value;

            var PackageHeaders = new List<SdkPackageInfo>();
            CollectPackages(PackageHeaders);
            var sdkHeader = new UnitySdkHeader()
            {
                CoreVersion = PackageInfo.Version,
                Packages = PackageHeaders
            };
            headers[key: "X-Unity-Sdk"] = JsonConvert.SerializeObject(sdkHeader, Formatting.None, JsonSettings);

            if (contentType != null)
                headers["Content-Type"] = contentType;
            if (acceptType != null)
                headers["Accept"] = acceptType;

            string bodyString;
            if (body == null)
            {
                bodyString = null;
            }
            else
            {
                if (body.GetType() != typeof(string))
                    bodyString = JsonConvert.SerializeObject(body);
                else
                    bodyString = (string)body;
            }

            return new RequestTask
            (
                taskId: _requestIndex++,
                baseUrl: _baseUrl,
                backendUrlOrAbsoluteUrl: urlOrEndpoint,
                isAbsoluteUrl: isAbsoluteUrl,
                method: method,
                headers: headers,
                bodyString: bodyString,
                contentType: contentType,
                logger: _logger,
                cancellationToken: linkedCts.Token
            );
        }

        private static async UniTask<T> SendUnityWebRequest<T>(RequestTask task)
        {
            var logger = task.Logger;
            var token = task.CancellationToken;

            UnityWebRequest request = null;

            try
            {
                token.ThrowIfCancellationRequested();

                request = new UnityWebRequest(task.Uri, task.Method);

                if (!string.IsNullOrEmpty(task.BodyString))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(task.BodyString);
                    request.uploadHandler = new UploadHandlerRaw(bytes);
                    request.uploadHandler.contentType = task.ContentType;
                }

                request.downloadHandler = new DownloadHandlerBuffer();

                foreach (var kv in task.Headers)
                    request.SetRequestHeader(kv.Key, kv.Value);

                if (logger.InfoEnabled)
                {
                    logger.Info($"{task.Method} {task.TaskId} Request {task.BackendUrlOrAbsUrl} {task.BodyString}");
                }

                await request.SendWebRequest().ToUniTask(cancellationToken: token);

                var httpCode = (HttpStatusCode)request.responseCode;
                var text = request.downloadHandler?.text ?? string.Empty;

                if (typeof(T) == typeof(string))
                {
                    if (logger.InfoEnabled)
                        logger.Info($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} {text}");

                    return (T)(object)text;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (logger.InfoEnabled)
                        logger.Info($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} {task.Method}");

                    return default;
                }

                try
                {
                    var obj = JsonConvert.DeserializeObject<T>(text, JsonSettings);

                    if (logger.InfoEnabled)
                        logger.Info($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} {text}");

                    return obj;
                }
                catch (JsonException ex)
                {
                    if (logger.WarnEnabled)
                        logger.Warn($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} (JsonError) {text}");

                    throw new ApiException(
                        errorCode: ApiErrorCode.DeserializationError,
                        statusCode: httpCode,
                        uri: task.Uri,
                        responseText: text,
                        message: $"Deserialize failed: {text}",
                        inner: ex
                    );
                }
            }
            catch (UnityWebRequestException ex)
            {
                var httpCode = (HttpStatusCode)ex.ResponseCode;
                var text = ex.Text ?? string.Empty;

                var errorCode = ex.ResponseCode >= 500
                    ? ApiErrorCode.ServerError
                    : ApiErrorCode.Unknown;

                var message = $"({(int)httpCode}){httpCode} {text}";
                if (string.IsNullOrWhiteSpace(text))
                    message += $" [{ex.Error}]";

                if (logger.WarnEnabled)
                    logger.Warn($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} Failed {message}");

                throw new ApiException(
                    errorCode: errorCode,
                    statusCode: httpCode,
                    uri: task.Uri,
                    responseText: text,
                    message: message
                );
            }
            catch (OperationCanceledException)
            {
                if (logger.WarnEnabled)
                    logger.Warn($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                if (logger.ErrorEnabled)
                    logger.Err($"{task.Method} {task.TaskId} Response {task.BackendUrlOrAbsUrl} Unexpected {ex}");

                throw;
            }
            finally
            {
                request?.Dispose();
                task.Dispose();
            }
        }
    }
}