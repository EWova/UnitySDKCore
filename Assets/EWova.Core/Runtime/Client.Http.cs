
using Cysharp.Threading.Tasks;

using Proyecto26;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace EWova.NetService
{
    public partial class Client
    {
        Dictionary<string, string> AuthHeader
        {
            get
            {
                if (!IsLogin)
                    return null;

                return new() { ["Authorization"] = $"Bearer {_ewovaAuthManager.AccessToken}" };
            }
        }

        public static readonly Logger HttpLogger = new("[NetService/Client] ", Logger.Level.Full);
        public async UniTask<string> Get(string path, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(EWova.ApiBaseUrl, path),
                Headers = AuthHeader,
                Method = "GET",
            };

            HttpLogger.Log($"[{req.Method}] Request Uri:{req.Uri}");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} Exception:{ex}", ex);
                return null;
            }

            HttpLogger.Log($"[{req.Method}] Response Uri:{req.Uri} Content:{rsp.Text}");
            return rsp.Text;
        }
        public async UniTask<T> Get<T>(string path, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(EWova.ApiBaseUrl, path),
                Headers = AuthHeader,
                Method = "GET",
            };

            HttpLogger.Log($"[{req.Method}] Request Uri:{req.Uri}");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} Exception:{ex}", ex);
                return default(T);
            }

            T typed;
            if (!string.IsNullOrWhiteSpace(rsp.Text))
            {
                try
                {
                    typed = DeserializeObject<T>(rsp.Text);
                }
                catch (Exception ex)
                {
                    HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} DeserializeObject({typeof(T)}) Exception:{ex}", ex);
                    return default(T);
                }
            }
            else
            {
                HttpLogger.Warn($"[{req.Method}] Warning Uri:{req.Uri} Empty Response");
                return default(T);
            }

            HttpLogger.Log($"[{req.Method}] Response Uri:{req.Uri} Content({typeof(T)}):{rsp.Text}");
            return typed;
        }
        public async UniTask<string> Post(string path, object body, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(EWova.ApiBaseUrl, path),
                Headers = AuthHeader,
                Method = "POST",
                Body = body
            };

            HttpLogger.Log($"[{req.Method}] Request Uri:{req.Uri}");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} Exception:{ex}", ex);
                return null;
            }

            HttpLogger.Log($"[{req.Method}] Response Uri:{req.Uri} Content:{rsp.Text}");

            return rsp.Text;
        }
        public async UniTask<T> Post<T>(string path, object body, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(EWova.ApiBaseUrl, path),
                Headers = AuthHeader,
                Method = "POST",
                Body = body
            };

            HttpLogger.Log($"[{req.Method}] Request Uri:{req.Uri}");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} Exception:{ex}", ex);
                return default(T);
            }

            T typed;
            if (!string.IsNullOrWhiteSpace(rsp.Text))
            {
                try
                {
                    typed = DeserializeObject<T>(rsp.Text);
                }
                catch (Exception ex)
                {
                    HttpLogger.Exce($"[{req.Method}] Error Uri:{req.Uri} DeserializeObject({typeof(T)}) Exception:{ex}", ex);
                    return default(T);
                }
            }
            else
            {
                HttpLogger.Warn($"[{req.Method}] Warning Uri:{req.Uri} Empty Response");
                return default(T);
            }

            HttpLogger.Log($"[{req.Method}] Response Uri:{req.Uri} Content({typeof(T)}):{rsp.Text}");
            return typed;
        }

        private T DeserializeObject<T>(string text)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text);
        }
    }
}
