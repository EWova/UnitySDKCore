using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using EWova.Utility;

namespace EWova.Networking
{
    public partial class AuthApiClient
    {
        public UniTask<Texture2D> GetTex2D(
            string url,
            bool isAbsoluteUrl = false,
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            var task = new RequestTask
            (
                backendUrlOrAbsoluteUrl: url,
                isAbsoluteUrl: isAbsoluteUrl,
                method: UnityWebRequest.kHttpVerbGET,
                acceptType: null,
                body: null,
                contentType: null,
                throwApiExceptionFor4xxResponses: true,
                progress: progress,
                ct: ct
            );
            task.HandleRequest(this);
            return SendUnityWebRequestTexture2D(task);
        }
        internal static async UniTask<Texture2D> SendUnityWebRequestTexture2D(RequestTask task)
        {
            var logger = task.Logger;
            var token = task.CancellationToken;

            UnityWebRequest request = null;
            try
            {
                token.ThrowIfCancellationRequested();

                request = new UnityWebRequest(task.Uri, task.Method);
                request.timeout = DefaultRequestTimeoutSeconds;

#if UNITY_6000_0_OR_NEWER
                var downloadHandler = new DownloadHandlerTexture();
                request.downloadHandler = downloadHandler;
#else
                request.downloadHandler = new DownloadHandlerBuffer();
#endif

                foreach (var kv in task.Headers)
                    request.SetRequestHeader(kv.Key, kv.Value);

                if (logger.InfoEnabled)
                    logger.Info($"GetTex2D {task.TaskId} Request {task.BackendUrlOrAbsUrl}");

                bool hasProgress = task.Progress != null;
                float progressValue = 0f;
                IProgress<float> progress = hasProgress ? Progress.Create<float>(p =>
                {
                    progressValue = p;
                    task.Progress.Report(progressValue);
                }) : null;

                await request.SendWebRequest().ToUniTask(progress, cancellationToken: token);

                if (hasProgress && progressValue != 1f)
                        task.Progress.Report(1f);

                Texture2D tex;

#if UNITY_6000_0_OR_NEWER
                tex = downloadHandler.texture;
#else
                var data = request.downloadHandler?.data;

                if (data == null || data.Length == 0)
                    return null;

                tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                tex.LoadImage(data, true);
#endif

                if (logger.InfoEnabled)
                    logger.Info($"GetTex2D {task.TaskId} Response size={tex.width}x{tex.height},memory={tex.CalcUnityObjectNativeSize().RuntimeEstimateSize.ToHumanReadableSize()}");

                if (logger.WarnEnabled && (tex.width > 2048 || tex.height > 2048))
                    logger.Warn("Response Texture size is too large, it may cause performance issues.");

                if (progressValue != 1f)
                    task.Progress?.Report(1f);

                return tex;
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
                    logger.Warn($"GetTex2D {task.TaskId} Response {task.BackendUrlOrAbsUrl} Failed {message}");

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
                    logger.Warn($"GetTex2D {task.TaskId} Response {task.BackendUrlOrAbsUrl} Cancelled");

                throw;
            }
            catch (Exception ex)
            {
                if (logger.ErrorEnabled)
                    logger.Err($"GetTex2D {task.TaskId} Response {task.BackendUrlOrAbsUrl} Unexpected {ex}");

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