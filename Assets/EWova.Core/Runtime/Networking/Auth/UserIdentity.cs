using Newtonsoft.Json;

using System;

namespace EWova.Auth
{
    public readonly struct UserIdentity
    {
        public readonly JwtPayload Payload;
        /// <summary>
        /// client fetch timestamp (NOT server updated time)
        /// </summary>
        public readonly DateTimeOffset FetchedAt;

        public UserIdentity(JwtPayload jwtPayload)
        {
            Payload = jwtPayload;
            FetchedAt = DateTimeOffset.UtcNow;
        }

        public override string ToString()
        {
            return ToString(false);
        }
        public string ToString(bool prettyPrint)
        {
            return JsonConvert.SerializeObject(this, prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }
}
