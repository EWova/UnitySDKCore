using Cysharp.Threading.Tasks;

using EWova.NetService.Model;

using System;
using System.Threading;

namespace EWova.NetService
{
    /// <summary>
    /// EWovaApiClient 是 EWova.NetService 的預設 AuthenticatedApiClient 實作，提供了針對 EWova API 的專用方法。
    /// 請透過 AuthenticatedApiClient.EWovaService 來存取此類別的實例。
    /// </summary>
    public class EWovaApiClient : AuthenticatedApiClient
    {
        internal EWovaApiClient() : base(EWova.ApiBaseUrl) { }

        /// <summary>
        /// 取得目前使用者的個人資料
        /// </summary>
        /// <exception cref="NotAuthenticatedException">使用者未通過驗證</exception>
        public UniTask<UserProfile> GetProfile(CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();
            return Get<UserProfile>("user", ct: cancellationToken);
        }

        /// <summary>
        /// 取得目前使用者所屬的組織資料
        /// </summary>
        /// <exception cref="NotAuthenticatedException">使用者未通過驗證</exception>
        public async UniTask<OrganizationProfile> GetOrganization(CancellationToken cancellationToken = default)
        {
            EnsureAuthenticated();
            Guid schoolGuid = (await GetProfile(cancellationToken)).schoolGuid;
            return await GetOrganization(schoolGuid, cancellationToken: cancellationToken);
        }

        public UniTask<UserProfile> GetUser(Guid guid, CancellationToken ct = default)
        {
            return Get<UserProfile>($"user/{guid}", ct: ct);
        }
        public UniTask<OrganizationProfile> GetOrganization(Guid guid, CancellationToken cancellationToken = default)
        {
            return Get<OrganizationProfile>($"school/{guid}", ct: cancellationToken);
        }

        private void EnsureAuthenticated()
        {
            if (!IsUserAuthenticated)
                throw new NotAuthenticatedException();
        }
    }

    public class NotAuthenticatedException : Exception
    {
        public NotAuthenticatedException() : base("User is not authenticated.") { }
    }
}
