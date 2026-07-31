using System;
using System.Collections.Generic;

namespace EWova.Auth
{
    public class UserProfile
    {
        /// <summary>
        /// 使用者 ID
        /// </summary>
        public Guid Id { get; private set; }
        /// <summary>
        /// 名字
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 暱稱
        /// </summary>
        public string Nickname { get; private set; }
        /// <summary>
        /// 生日
        /// </summary>
        public DateTime? BirthDate { get; private set; }
        /// <summary>
        /// 電子郵件
        /// </summary>
        public string Email { get; private set; }
        /// <summary>
        /// 電子郵件是否已驗證
        /// </summary>
        public bool EmailVerified { get; private set; }
        /// <summary>
        /// 組織 ID
        /// </summary>
        public Guid OrgId { get; private set; }
        /// <summary>
        /// 組織名稱
        /// </summary>
        public string OrgName { get; private set; }
        /// <summary>
        /// 使用者角色列表
        /// </summary>
        public List<string> Roles { get; private set; }

        /// <summary>
        /// client fetch timestamp (NOT server updated time)
        /// </summary>
        public DateTimeOffset FetchedAt { get; private set; }

        private UserProfile() { }

        public static UserProfile FromJwt(JwtObject jwtObject, DateTimeOffset fetchedAt)
        {
            if (jwtObject == null || jwtObject.Payload == null || jwtObject.Payload.AdditionalClaims == null)
            {
                throw new ArgumentException("Invalid JWT object or missing claims.");
            }

            var sub = jwtObject.Payload.Subject;
            var claims = jwtObject.Payload.AdditionalClaims;
            return new UserProfile
            {
                Id = Guid.TryParse(sub, out var guid) ? guid : Guid.Empty,
                Name = claims.GetString("name"),
                Nickname = claims.GetString("nickname"),
                BirthDate = claims.GetDateTime("birthDate"),
                Email = claims.GetString("email"),
                EmailVerified = claims.GetBool("email_verified"),
                OrgId = claims.GetGuid("org_id"),
                OrgName = claims.GetString("org_name"),
                Roles = claims.GetStringList("roles") ?? new List<string>(),
                FetchedAt = fetchedAt
            };
        }

        public override string ToString()
        {
            return $@"
Id: {Id},
Name: {Name},
Nickname: {Nickname},
BirthDate: {BirthDate},
Email: {Email},
EmailVerified: {EmailVerified},
OrgId: {OrgId},
OrgName: {OrgName},
Roles: {string.Join(", ", Roles)},
FetchedAt: {FetchedAt.ToLocalTime()}
";
        }
    }
}
