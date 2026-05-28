using Cysharp.Threading.Tasks;
using Proyecto26;

using System;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        public async UniTask<Texture2D> GetTex2D(
            string url,
            bool isAbsoluteUrl = false,
            CancellationToken ct = default)
        {
            var req = CreateRequest(url, "GET", isAbsoluteUrl);

#if UNITY_6000_0_OR_NEWER
            req.DownloadHandler = new DownloadHandlerTexture();
#else
            req.DownloadHandler = new DownloadHandlerBuffer();
#endif

            _logger.Log($"[{req.Method}] Tex2D Request:{url}");

            try
            {
                var rsp = await RestClient.Request(req).AsUniTask(ct);

#if UNITY_6000_0_OR_NEWER
                var tex = (rsp.Request.downloadHandler as DownloadHandlerTexture)?.texture;
#else
                var data = (rsp.Request.downloadHandler as DownloadHandlerBuffer)?.data;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                tex.LoadImage(data, true);
#endif

                _logger.Log($"[{req.Method}] Tex2D Response:{tex.width}x{tex.height}");
                return tex;
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{req.Method}] Tex2D Exception:{ex}", ex);
                return null;
            }
        }

    }
}