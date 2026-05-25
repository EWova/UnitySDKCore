using System;
using System.Threading;

using Cysharp.Threading.Tasks;

using EWova.Auth;
using EWova.NetService.Model;

namespace EWova.NetService
{
    public partial class Client
    {
        public static readonly Logger Debugger = new("[NetService/Client] ");

        public Client(EwovaAuthManager ewovaAuthManager)
        {
            _ewovaAuthManager = ewovaAuthManager != null ? ewovaAuthManager : throw new ArgumentNullException(nameof(ewovaAuthManager));
        }
        private readonly EwovaAuthManager _ewovaAuthManager;

        public bool IsLogin => _ewovaAuthManager.State == AuthState.Authenticated;

        public UniTask<UserProfile> GetProfile(CancellationToken cancellationToken = default)
        {
            if (!IsLogin)
            {
                Debugger.Warn("未登入");
                return UniTask.FromResult<UserProfile>(null);
            }

            return Get<UserProfile>("user", cancellationToken: cancellationToken);
        }
        public async UniTask<OrganizationProfile> GetOrganization(CancellationToken cancellationToken = default)
        {
            if (!IsLogin)
            {
                Debugger.Warn("未登入");
                return null;
            }

            Guid schoolGuid = (await GetProfile(cancellationToken)).schoolGuid;
            return await GetOrganization(schoolGuid, cancellationToken: cancellationToken);
        }
    }
}
