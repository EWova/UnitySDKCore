using Cysharp.Threading.Tasks;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using UnityEngine.Networking;
using UnityEngine;
using UnityEngine.Scripting;

namespace EWova.Networking
{
    public partial class AuthApiClient
    {
        private int _requestIndex = 0;
        public sealed class RequestTask : IDisposable
        {
            private readonly CancellationTokenSource _selfCts;
            private CancellationTokenSource _linkedCts;
            internal AuthApiClient Handler;

            public bool IsReady => Handler != null && !IsDisposed;
            public string Uri { get; private set; }
            public int TaskId { get; private set; }
            public string BaseUrl { get; private set; }
            public string BackendUrlOrAbsUrl { get; private set; }
            public string Method { get; private set; }
            public string AcceptType { get; private set; }
            public Dictionary<string, string> Headers { get; private set; } = new Dictionary<string, string>();
            public string BodyString { get; private set; }
            public string ContentType { get; private set; }
            public bool IsAbsoluteUrl { get; private set; }
            public Logger Logger { get; private set; }
            public bool IsDisposed { get; private set; }
            public double CreatedAt { get; private set; }
            public double DisposedAt { get; private set; }
            public CancellationToken CancellationToken { get; private set; }
            /// <summary>
            /// 是否將 HTTP 4xx Client Error 狀態碼轉換為 ApiException。
            /// 啟用時，收到 400~499 回應會中斷正常流程並拋出 ApiException；
            /// 停用時，4xx 回應會視為一般 HTTP 回應，由呼叫端自行解析處理。
            /// 預設為 true。
            /// </summary>
            public bool ThrowApiExceptionFor4xxResponses { get; set; } = true;
            public TimeSpan ElapsedTime => TimeSpan.FromSeconds((IsDisposed ? DisposedAt : Time.realtimeSinceStartupAsDouble) - CreatedAt);
            public RequestTask(
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl,
                string method,
                string acceptType,
                object body,
                string contentType,
                bool throwApiExceptionFor4xxResponses,
                CancellationToken ct)
            {
                BackendUrlOrAbsUrl = backendUrlOrAbsoluteUrl;
                IsAbsoluteUrl = isAbsoluteUrl;
                Method = method;
                AcceptType = acceptType;
                string bodyString;
                if (body == null)
                    bodyString = null;
                else
                {
                    if (body.GetType() != typeof(string))
                        bodyString = JsonConvert.SerializeObject(body);
                    else
                        bodyString = (string)body;
                }
                BodyString = bodyString;
                ContentType = contentType;
                ThrowApiExceptionFor4xxResponses = throwApiExceptionFor4xxResponses;

                CreatedAt = Time.realtimeSinceStartupAsDouble;

                _selfCts = new CancellationTokenSource();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _selfCts.Token);
                CancellationToken = linkedCts.Token;
            }

            internal void HandleRequest(AuthApiClient authApiClient)
            {
                var headers = new Dictionary<string, string>();

                if (authApiClient.TryGetValidAccessToken(out string accessToken))
                    headers[key: "Authorization"] = $"Bearer {accessToken}";

                foreach (var kv in authApiClient.AdditionalHeaders)
                {
                    var value = kv.Value?.Invoke();
                    if (value != null)
                        headers[kv.Key] = value;
                }

                var PackageHeaders = new List<SdkPackageInfo>();
                authApiClient.CollectPackages(PackageHeaders);
                var sdkHeader = new UnitySdkHeader()
                {
                    CoreVersion = PackageInfo.Version,
                    Packages = PackageHeaders
                };
                headers[key: "X-Unity-Sdk"] = JsonConvert.SerializeObject(sdkHeader, Formatting.None, JsonSettings);

                if (ContentType != null)
                    headers["Content-Type"] = ContentType;
                if (AcceptType != null)
                    headers["Accept"] = AcceptType;

                foreach (var kv in headers)
                    Headers[kv.Key] = kv.Value;

                Handler = authApiClient;
                BaseUrl = authApiClient._baseUrl;
                TaskId = authApiClient._requestIndex++;
                Logger = authApiClient._logger;
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    authApiClient._disposeCts.Token,
                    CancellationToken);
                CancellationToken = _linkedCts.Token;
                Uri = IsAbsoluteUrl ? BackendUrlOrAbsUrl : $"{BaseUrl}/{BackendUrlOrAbsUrl.TrimStart('/')}";
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

            public static RequestTask GET
            (
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl = false,
                string method = UnityWebRequest.kHttpVerbGET,
                string acceptType = null,
                object body = null,
                string contentType = null,
                bool throwApiExceptionFor4xxResponses = true,
                CancellationToken ct = default)
            {
                return new RequestTask(
                    backendUrlOrAbsoluteUrl: backendUrlOrAbsoluteUrl,
                    isAbsoluteUrl: isAbsoluteUrl,
                    method: method,
                    acceptType: acceptType,
                    body: body,
                    contentType: contentType,
                    throwApiExceptionFor4xxResponses: throwApiExceptionFor4xxResponses,
                    ct: ct);
            }
            public static RequestTask POST
            (
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl = false,
                string acceptType = null,
                object body = null,
                string contentType = null,
                bool throwApiExceptionFor4xxResponses = true,
                CancellationToken ct = default)
            {
                return new RequestTask(
                    backendUrlOrAbsoluteUrl: backendUrlOrAbsoluteUrl,
                    isAbsoluteUrl: isAbsoluteUrl,
                    method: UnityWebRequest.kHttpVerbPOST,
                    acceptType: acceptType,
                    body: body,
                    contentType: contentType,
                    throwApiExceptionFor4xxResponses: throwApiExceptionFor4xxResponses,
                    ct: ct);
            }
            public static RequestTask PUT
            (
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl = false,
                string acceptType = null,
                object body = null,
                string contentType = null,
                bool throwApiExceptionFor4xxResponses = true,
                CancellationToken ct = default)
            {
                return new RequestTask(
                    backendUrlOrAbsoluteUrl: backendUrlOrAbsoluteUrl,
                    isAbsoluteUrl: isAbsoluteUrl,
                    method: UnityWebRequest.kHttpVerbPUT,
                    acceptType: acceptType,
                    body: body,
                    contentType: contentType,
                    throwApiExceptionFor4xxResponses: throwApiExceptionFor4xxResponses,
                    ct: ct);
            }
            public static RequestTask DELETE
            (
                string backendUrlOrAbsoluteUrl,
                bool isAbsoluteUrl = false,
                string acceptType = null,
                object body = null,
                string contentType = null,
                bool throwApiExceptionFor4xxResponses = true,
                CancellationToken ct = default)
            {
                return new RequestTask(
                    backendUrlOrAbsoluteUrl: backendUrlOrAbsoluteUrl,
                    isAbsoluteUrl: isAbsoluteUrl,
                    method: UnityWebRequest.kHttpVerbDELETE,
                    acceptType: acceptType,
                    body: body,
                    contentType: contentType,
                    throwApiExceptionFor4xxResponses: throwApiExceptionFor4xxResponses,
                    ct: ct);
            }
        }

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        [Preserve]
        public class UnitySdkHeader
        {
            [Preserve]
            [JsonProperty("coreVersion")]
            public string CoreVersion { get; set; } = string.Empty;

            [Preserve]
            [JsonProperty("packages")]
            public List<SdkPackageInfo> Packages { get; set; } = new();
        }

        [Preserve]
        public class SdkPackageInfo
        {
            [Preserve]
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [Preserve]
            [JsonProperty("version")]
            public string Version { get; set; } = string.Empty;
        }

        /// <summary>
        /// value 若 return null，則不會加入 Header
        /// </summary>
        protected readonly Dictionary<string, Func<string>> AdditionalHeaders = new();

        /// <summary>
        /// 將會用到的套件資訊加入 list 中，最終會傳回 "X-Unity-Sdk" Header
        /// </summary>
        protected virtual void CollectPackages(List<SdkPackageInfo> list) { }

        #region Methods
        ///<exception cref="ApiException"></exception>
        protected internal UniTask<T> Send<T>(RequestTask task,
            Action<RequestTask> postProcRequestTask = null)
        {
            ThrowIfDisposed();
            postProcRequestTask?.Invoke(task);
            task.HandleRequest(this);
            return SendUnityWebRequest<T>(task);
        }
        #endregion

        private static async UniTask<T> SendUnityWebRequest<T>(RequestTask task)
        {
            var logger = task.Logger;
            var token = task.CancellationToken;

            UnityWebRequest request = null;

            T HandleResponse()
            {
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

            try
            {
                token.ThrowIfCancellationRequested();

                request = new UnityWebRequest(task.Uri, task.Method);
                request.timeout = DefaultRequestTimeoutSeconds;

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
                    logger.Info($"{task.Method} {task.TaskId} Request {task.BackendUrlOrAbsUrl} {task.BodyString}");

                await request.SendWebRequest().ToUniTask(cancellationToken: token);

                return HandleResponse();
            }
            catch (UnityWebRequestException ex)
            {
                var httpCode = (HttpStatusCode)ex.ResponseCode;

                if (!task.ThrowApiExceptionFor4xxResponses && (int)httpCode >= 400 && (int)httpCode < 500)
                    return HandleResponse();

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

                throw new ApiException(
                    errorCode: ApiErrorCode.Unknown,
                    statusCode: 0,
                    uri: task.Uri,
                    responseText: null,
                    message: "發送請求時發生非預期錯誤，若是 SDK 內部錯誤無法解決請聯絡我們。",
                    inner: ex
                );
            }
            finally
            {
                request?.Dispose();
                task.Dispose();
            }
        }
    }
}