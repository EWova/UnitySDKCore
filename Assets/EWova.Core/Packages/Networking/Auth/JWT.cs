using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine.Scripting;


[Serializable]
[Preserve]
public sealed class JwtObject
{
    public JwtHeader Header { get; set; }
    public JwtPayload Payload { get; set; }
}

[Serializable]
[Preserve]
public sealed class JwtHeader
{
    [Preserve]
    [JsonProperty("alg")]
    public string Algorithm { get; set; }

    [Preserve]
    [JsonProperty("typ")]
    public string Type { get; set; }

    [Preserve]
    [JsonProperty("kid")]
    public string KeyId { get; set; }
}

[Serializable]
[Preserve]
public sealed class JwtPayload
{
    [Preserve]
    [JsonProperty("nonce")]
    public string Nonce { get; set; }

    [Preserve]
    [JsonProperty("iss")]
    public string Issuer { get; set; }

    [Preserve]
    [JsonProperty("aud")]
    public string Audience { get; set; }

    [Preserve]
    [JsonProperty("exp")]
    public long Expiry { get; set; }

    [Preserve]
    [JsonProperty("sub")]
    public string Subject { get; set; }

    [Preserve]
    [JsonExtensionData]
    public Dictionary<string, JToken> AdditionalClaims { get; set; }
}

public class JwtException : Exception
{
    public JwtException(string message) : base(message) { }
}

public static class JWT
{
    [Preserve]
    public static JwtObject Read(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            throw new JwtException("JWT is null or empty.");

        string[] parts = jwt.Split('.');

        if (parts.Length != 3)
            throw new JwtException("JWT must contain exactly 3 parts.");

        try
        {
            string headerJson = Base64UrlDecode(parts[0]);
            string payloadJson = Base64UrlDecode(parts[1]);

            JwtHeader header =
                JsonConvert.DeserializeObject<JwtHeader>(headerJson);

            JwtPayload payload =
                JsonConvert.DeserializeObject<JwtPayload>(payloadJson);

            if (header == null)
                throw new JwtException("Failed to deserialize JWT header.");

            if (payload == null)
                throw new JwtException("Failed to deserialize JWT payload.");

            return new JwtObject
            {
                Header = header,
                Payload = payload
            };
        }
        catch (FormatException ex)
        {
            throw new JwtException($"Invalid Base64Url format: {ex.Message}");
        }
        catch (JsonException ex)
        {
            throw new JwtException($"Invalid JWT JSON: {ex.Message}");
        }
    }

    private static string Base64UrlDecode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string padded = input
            .Replace('-', '+')
            .Replace('_', '/');

        int padding = 4 - (padded.Length % 4);

        if (padding < 4)
            padded = padded.PadRight(padded.Length + padding, '=');

        byte[] bytes = Convert.FromBase64String(padded);

        return Encoding.UTF8.GetString(bytes);
    }
}