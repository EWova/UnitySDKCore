using Cysharp.Threading.Tasks;
using Proyecto26;

using System;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine;
using Newtonsoft.Json;

namespace EWova.NetService
{
    public partial class AuthApiClient
    {
        public async UniTask<Texture2D> GetTex2D(
            string url,
            bool isAbsoluteUrl = false,
            CancellationToken ct = default)
        {
            var req = CreateRequest(url, "GET", isAbsoluteUrl: isAbsoluteUrl);

#if UNITY_6000_0_OR_NEWER
            req.Helper.DownloadHandler = new DownloadHandlerTexture();
#else
            req.RequestHelper.DownloadHandler = new DownloadHandlerBuffer();
#endif

            const string method = "GetTex2D";
            if (_logger.InfoEnabled)
            {
                var detail = new { req.Helper.Body, req.Helper.Headers };
                if (!isAbsoluteUrl)
                    _logger.Info($"{url} {req.Index} {method} Request {JsonConvert.SerializeObject(detail, Formatting.None, JsonSettings)}");
                else
                    _logger.Info($" (AbsUrl) {url} {req.Index} {method} Request {JsonConvert.SerializeObject(detail, Formatting.None, JsonSettings)}");
            }

            try
            {
                var rsp = await RestClient.Request(req.Helper).AsUniTask(ct);

#if UNITY_6000_0_OR_NEWER
                var tex = (rsp.Request.downloadHandler as DownloadHandlerTexture)?.texture;
#else
                var data = (rsp.Request.downloadHandler as DownloadHandlerBuffer)?.data;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                tex.LoadImage(data, true);
#endif
                if (_logger.InfoEnabled)
                {
                    if (!isAbsoluteUrl)
                        _logger.Info($"{url} {req.Index} {method} Response {tex.width}x{tex.height}");
                    else
                        _logger.Info($" (AbsUrl) {url} {req.Index} {method} Response {tex.width}x{tex.height}");
                }

                return tex;
            }
            catch (OperationCanceledException)
            {
                if (_logger.WarnEnabled)
                {
                    if (!isAbsoluteUrl)
                        _logger.Warn($"{url} {req.Index} {method} Response Canceled");
                    else
                        _logger.Warn($" (AbsUrl) {url} {req.Index} {method} Response Canceled");
                }

                return null;
            }
            catch (Exception ex)
            {
                if (_logger.ErrorEnabled)
                {
                    if (!isAbsoluteUrl)
                        _logger.Err($"{url} {req.Index} {method} Response Exception:{ex.Message}");
                    else
                        _logger.Err($" (AbsUrl) {url} {req.Index} {method} Response Exception:{ex.Message}");
                }

                UnityEngine.Debug.LogException(ex);
                return null;
            }
        }

    }
}