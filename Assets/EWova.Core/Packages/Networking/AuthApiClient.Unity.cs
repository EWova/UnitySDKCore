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
        public async UniTask<Texture2D> GetTex2D(
            string url,
            bool isAbsoluteUrl = false,
            CancellationToken ct = default)
        {
            var task = CreateRequestTask(
                urlOrEndpoint: url,
                method: UnityWebRequest.kHttpVerbGET,
                acceptType: null,
                body: null,
                contentType: null,
                isAbsoluteUrl: isAbsoluteUrl,
                cancellationToken: ct);

            var index = task.TaskId;
            var logger = task.Logger;
            var token = task.CancellationToken;

            using var request = new UnityWebRequest(task.Uri, UnityWebRequest.kHttpVerbGET);

#if UNITY_6000_0_OR_NEWER
            var downloadHandler = new DownloadHandlerTexture();
            request.downloadHandler = downloadHandler;
#else
            request.downloadHandler = new DownloadHandlerBuffer();
#endif

            foreach (var kv in task.Headers)
                request.SetRequestHeader(kv.Key, kv.Value);

            if (_logger.InfoEnabled)
            {

                logger.Info($"GetTex2D {task.TaskId} Request {task.BackendUrlOrAbsUrl}");
            }

            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: token);

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

                if (_logger.InfoEnabled)
                {
                    _logger.Info($"GetTex2D {task.TaskId} Response size={tex.width}x{tex.height},memory={tex.CalcUnityObjectNativeSize().RuntimeEstimateSize.ToHumanReadableSize()}");
                }

                if (_logger.WarnEnabled && (tex.width > 2048 || tex.height > 2048))
                {
                    _logger.Warn("Response Texture size is too large, it may cause performance issues.");
                }

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
                if (_logger.WarnEnabled)
                    logger.Warn($"GetTex2D {task.TaskId} Response {task.BackendUrlOrAbsUrl} Canceled");

                return null;
            }
            catch (Exception ex)
            {
                if (_logger.ErrorEnabled)
                    logger.Warn($"GetTex2D {task.TaskId} Response {task.BackendUrlOrAbsUrl} Failed");

                Debug.LogException(ex);
                return null;
            }
            finally
            {
                request.Dispose();
                task.Dispose();
            }
        }
    }
}