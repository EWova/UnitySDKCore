using Cysharp.Threading.Tasks;
using Proyecto26;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace EWova.NetService
{
    public partial class AuthenticatedApiClient
    {
        internal Dictionary<string, string> AuthHeader =>
            IsUserAuthenticated
                ? new() { ["Authorization"] = $"Bearer {AccessToken}" }
                : null;

        public UniTask<string> Get(string endpoint, CancellationToken ct = default) =>
            Send(endpoint, "GET", cancellationToken: ct);

        public UniTask<T> Get<T>(string endpoint, CancellationToken ct = default) =>
            Send<T>(endpoint, "GET", cancellationToken: ct);

        public UniTask<string> Post(string endpoint, object body, CancellationToken ct = default) =>
            Send(endpoint, "POST", body, ct);

        public UniTask<T> Post<T>(string endpoint, object body, CancellationToken ct = default) =>
            Send<T>(endpoint, "POST", body, ct);

        public UniTask<string> Put(string endpoint, object body, CancellationToken ct = default) =>
            Send(endpoint, "PUT", body, ct);

        public UniTask<T> Put<T>(string endpoint, object body, CancellationToken ct = default) =>
            Send<T>(endpoint, "PUT", body, ct);

        public UniTask<string> Delete(string endpoint, CancellationToken ct = default) =>
            Send(endpoint, "DELETE", cancellationToken: ct);

        public UniTask<T> Delete<T>(string endpoint, CancellationToken ct = default) =>
            Send<T>(endpoint, "DELETE", cancellationToken: ct);

        private async UniTask<string> Send(
            string endpoint,
            string method,
            object body = null,
            CancellationToken cancellationToken = default)
        {
            var req = CreateRequest(endpoint, method, body);

            _logger.Log($"[{method}] (/{endpoint}) Request");

            try
            {
                var rsp = await RestClient.Request(req).AsUniTask(cancellationToken);

                _logger.Log($"[{method}] (/{endpoint}) Response:{rsp.Text}");

                return rsp.Text;
            }
            catch (Exception ex)
            {
                _logger.Exce($"[{method}] (/{endpoint}) Exception:{ex}", ex);
                return null;
            }
        }

        private async UniTask<T> Send<T>(
            string endpoint,
            string method,
            object body = null,
            CancellationToken cancellationToken = default)
        {
            var text = await Send(endpoint, method, body, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.Warn($"[{method}] (/{endpoint}) Empty Response");
                return default;
            }

            try
            {
                var result = JsonConvert.DeserializeObject<T>(text);

                _logger.Log($"[{method}] (/{endpoint}) Response({typeof(T)}):{text}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.Exce(
                    $"[{method}] (/{endpoint}) Deserialize<{typeof(T).Name}> Exception:{ex}",
                    ex);

                return default;
            }
        }

        private RequestHelper CreateRequest(
            string endpoint,
            string method,
            object body = null)
        {
            return new RequestHelper
            {
                Uri = Path.Combine(_baseUrl, endpoint),
                Headers = AuthHeader,
                Method = method,
                Body = body
            };
        }
    }
}