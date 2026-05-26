
using Cysharp.Threading.Tasks;

using Proyecto26;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        internal Dictionary<string, string> AuthHeader
        {
            get
            {
                if (!IsUserAuthenticated)
                    return null;

                return new() { ["Authorization"] = $"Bearer {AccessToken}" };
            }
        }

        public async UniTask<string> Get(string endpoint, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(_baseUrl, endpoint),
                Headers = AuthHeader,
                Method = "GET",
            };

            _logger.Log($"[{req.Method}] (/{endpoint}) Request");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{req.Method}] (/{endpoint}) Exception:{ex}", ex);
                return null;
            }

            _logger.Log($"[{req.Method}] (/{endpoint}) Response Content:{rsp.Text}");
            return rsp.Text;
        }
        public async UniTask<T> Get<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(_baseUrl, endpoint),
                Headers = AuthHeader,
                Method = "GET",
            };

            _logger.Log($"[{req.Method}] (/{endpoint}) Request");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{req.Method}] (/{endpoint}) Exception:{ex}", ex);
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
                    _logger.Exce($"[{req.Method}] (/{endpoint}) DeserializeObject({typeof(T)}) Exception:{ex}", ex);
                    return default(T);
                }
            }
            else
            {
                _logger.Warn($"[{req.Method}] (/{endpoint}) Empty Response");
                return default(T);
            }

            _logger.Log($"[{req.Method}] (/{endpoint}) Response Content({typeof(T)}):{rsp.Text}");
            return typed;
        }
        public async UniTask<string> Post(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(_baseUrl, endpoint),
                Headers = AuthHeader,
                Method = "POST",
                Body = body
            };

            _logger.Log($"[{req.Method}] (/{endpoint}) Request");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{req.Method}] (/{endpoint}) Exception:{ex}", ex);
                return null;
            }

            _logger.Log($"[{req.Method}] (/{endpoint}) Response Content:{rsp.Text}");

            return rsp.Text;
        }
        public async UniTask<T> Post<T>(string endpoint, object body, CancellationToken cancellationToken = default)
        {
            var req = new RequestHelper
            {
                Uri = Path.Combine(_baseUrl, endpoint),
                Headers = AuthHeader,
                Method = "POST",
                Body = body
            };

            _logger.Log($"[{req.Method}] (/{endpoint}) Request");

            ResponseHelper rsp;
            try
            {
                rsp = await RestClient.Request(req).AsUniTask(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{req.Method}] (/{endpoint}) Exception:{ex}", ex);
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
                    _logger.Exce($"[{req.Method}] (/{endpoint}) DeserializeObject({typeof(T)}) Exception:{ex}", ex);
                    return default(T);
                }
            }
            else
            {
                _logger.Warn($"[{req.Method}] (/{endpoint}) Empty Response");
                return default(T);
            }

            _logger.Log($"[{req.Method}] (/{endpoint}) Response Content({typeof(T)}):{rsp.Text}");
            return typed;
        }

        private T DeserializeObject<T>(string text)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text);
        }
    }
}
