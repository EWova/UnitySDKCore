using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;

namespace EWova.Auth
{
    public static class ClaimExtensions
    {
        public static string GetString(this Dictionary<string, JToken> claims, string key, string defaultValue = null)
        {
            if (!claims.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null)
                return defaultValue;

            return token.ToString();
        }

        public static bool GetBool(this Dictionary<string, JToken> claims, string key, bool defaultValue = false)
        {
            if (!claims.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null)
                return defaultValue;

            if (token.Type == JTokenType.Boolean)
                return token.ToObject<bool>();

            return bool.TryParse(token.ToString(), out var result)
                ? result
                : defaultValue;
        }

        public static Guid GetGuid(this Dictionary<string, JToken> claims, string key, Guid defaultValue = default)
        {
            if (!claims.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null)
                return defaultValue;

            return Guid.TryParse(token.ToString(), out var result)
                ? result
                : defaultValue;
        }

        public static DateTime? GetDateTime(this Dictionary<string, JToken> claims, string key, DateTime? defaultValue = null)
        {
            if (!claims.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null)
                return defaultValue;

            return DateTime.TryParse(token.ToString(), out var result)
                ? result
                : defaultValue;
        }

        public static List<string> GetStringList(this Dictionary<string, JToken> claims, string key)
        {
            if (!claims.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null)
                return new List<string>();

            if (token.Type == JTokenType.Array)
                return token.ToObject<List<string>>() ?? new List<string>();

            return new List<string> { token.ToString() };
        }
    }
}
