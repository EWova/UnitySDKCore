using System;
using System.Text;

using Newtonsoft.Json;

using UnityEngine.Scripting;

[Preserve]
public readonly struct JwtObject
{
    public readonly JwtHeader Header;
    public readonly JwtPayload Payload;

    public JwtObject(JwtHeader header, JwtPayload payload)
    {
        Header = header;
        Payload = payload;
    }
}

[Preserve]
public readonly struct JwtHeader
{
    [Preserve][JsonProperty("alg")] public readonly string Algorithm;
    [Preserve][JsonProperty("typ")] public readonly string Type;
    [Preserve][JsonProperty("kid")] public readonly string KeyId;
}

[Preserve]
public readonly struct JwtPayload
{
    [Preserve][JsonProperty("sub")] public readonly string Subject;
    [Preserve][JsonProperty("amr")] public readonly string[] AuthenticationMethods;
    [Preserve][JsonProperty("quick_code")] public readonly string QuickCode;
    [Preserve][JsonProperty("quick_org")] public readonly string QuickOrg;

    [Preserve][JsonProperty("name")] public readonly string Name;
    [Preserve][JsonProperty("nickname")] public readonly string Nickname;
    [Preserve][JsonProperty("birthDate")] public readonly DateTimeOffset? BirthDate;
    [Preserve][JsonProperty("email")] public readonly string Email;
    [Preserve][JsonProperty("email_verified")] public readonly bool? EmailVerified;
    [Preserve][JsonProperty("org_id")] public readonly string OrgId;
    [Preserve][JsonProperty("org_name")] public readonly string OrgName;
    [Preserve][JsonProperty("roles")] public readonly string[] Roles;

    [Preserve][JsonProperty("updated_at")] public readonly long? UpdatedAt;
    [Preserve][JsonProperty("auth_time")] public readonly long? AuthTime;

    [Preserve][JsonProperty("nonce")] public readonly string Nonce;
    [Preserve][JsonProperty("at_hash")] public readonly string AccessTokenHash;

    [Preserve][JsonProperty("aud")] public readonly string Audience;
    [Preserve][JsonProperty("exp")] public readonly long? Expiry;
    [Preserve][JsonProperty("iat")] public readonly long? IssuedAt;
    [Preserve][JsonProperty("iss")] public readonly string Issuer;
}

public class JwtException : Exception
{
    public JwtException(string message) : base(message) { }
}

public static class JWT
{
    private readonly static JsonSerializerSettings JsonSerializerSettings = new() { MissingMemberHandling = MissingMemberHandling.Ignore };

    [Preserve]
    public static JwtObject Read(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            throw new JwtException("JWT is null or empty.");

        string[] parts = jwt.Split('.');

        if (parts.Length != 3)
            throw new JwtException("JWT must contain exactly 3 parts.");

        string headerJson;
        string payloadJson;
        try
        {
            headerJson = Base64UrlDecode(parts[0]);
            payloadJson = Base64UrlDecode(parts[1]);
        }
        catch (Exception ex)
        {
            throw new JwtException($"Unexpected error during JWT decoding: {ex.Message}");
        }


        JwtHeader header;
        try
        {
            header = JsonConvert.DeserializeObject<JwtHeader>(headerJson, JsonSerializerSettings);
        }
        catch (Exception ex)
        {
            throw new JwtException($"Unexpected error during JWT header deserialization: {ex.Message}");
        }

        JwtPayload payload;
        try
        {
            payload = JsonConvert.DeserializeObject<JwtPayload>(payloadJson, JsonSerializerSettings);
        }
        catch (Exception ex)
        {
            throw new JwtException($"Unexpected error during JWT payload deserialization: {ex.Message}");
        }

        return new JwtObject
        (
            header,
            payload
        );
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