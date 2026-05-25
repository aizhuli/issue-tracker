using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SimpleBase;

namespace AiIssueTracker.Api.Common.Pagination;

/// <summary>
/// Stateless, AES-encrypted, SHA-256-authenticated token pagination helper.
///
/// Wire format of a page token (before Base32):
///   AES-CBC-encrypt( JSON: { "o": offset, "h": "hex-sha256-of-request-params" } )
///
/// The SHA-256 hash covers all caller-supplied filter/sort parameters so that a token
/// issued for one query cannot be replayed against a different query.
/// </summary>
public sealed class LimitOffsetPaging
{
    private readonly PagingOptions _options;

    public LimitOffsetPaging(IOptions<PagingOptions> options)
    {
        _options = options.Value;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolves the effective max page size.
    /// Returns false (caller should throw) when <paramref name="requested"/> is out of range.
    /// </summary>
    public bool TryGetMaxPageSize(int? requested, out int resolved)
    {
        if (requested is null)
        {
            resolved = _options.DefaultMaxPageSize;
            return true;
        }

        if (requested <= 0 || requested > _options.MaxMaxPageSize)
        {
            resolved = 0;
            return false;
        }

        resolved = requested.Value;
        return true;
    }

    /// <summary>
    /// Decodes a page token into an SQL OFFSET + LIMIT pair.
    /// A null / empty token means "first page" → offset 0, limit = maxPageSize.
    /// Returns false (caller should throw) when the token is malformed or the hash mismatches.
    /// </summary>
    public bool TryGetOffsetAndLimit(
        string? pageToken,
        int maxPageSize,
        out int? offset,
        out int? limit,
        params object?[] requestParameters)
    {
        // First page — no token
        if (string.IsNullOrEmpty(pageToken))
        {
            offset = 0;
            limit = maxPageSize;
            return true;
        }

        try
        {
            var tokenBytes = Base32.Crockford.Decode(pageToken);
            var json = AesDecrypt(tokenBytes);
            var payload = JsonSerializer.Deserialize<TokenPayload>(json);

            if (payload is null || payload.Offset < 0)
            {
                offset = limit = null;
                return false;
            }

            // Verify the request-parameter hash so the token cannot be replayed
            // against a differently-filtered query.
            var expectedHash = ComputeParamHash(requestParameters);
            if (!string.Equals(payload.Hash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                offset = limit = null;
                return false;
            }

            offset = payload.Offset;
            limit = maxPageSize;
            return true;
        }
        catch
        {
            offset = limit = null;
            return false;
        }
    }

    /// <summary>
    /// Produces a next-page token, or null when there are no more pages.
    /// </summary>
    public string? CreateNextPageToken(
        int returnedCount,
        int offset,
        int limit,
        params object?[] requestParameters)
    {
        // Fewer rows than the page size → this was the last page
        if (returnedCount < limit)
        {
            return null;
        }

        var payload = new TokenPayload(
            Offset: offset + limit,
            Hash: ComputeParamHash(requestParameters));

        var json = JsonSerializer.Serialize(payload);
        var encrypted = AesEncrypt(json);
        return Base32.Crockford.Encode(encrypted);
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private record TokenPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("o")] int Offset,
        [property: System.Text.Json.Serialization.JsonPropertyName("h")] string Hash);

    /// <summary>
    /// SHA-256 of all request parameters joined with "|".
    /// Null parameters are represented as the empty string.
    /// </summary>
    private static string ComputeParamHash(object?[] parameters)
    {
        var raw = string.Join("|", parameters.Select(p => p?.ToString() ?? string.Empty));
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private byte[] AesEncrypt(string plainText)
    {
        var key = Convert.FromBase64String(_options.TokenEncryptionKeyInBase64);
        var iv = Convert.FromBase64String(_options.TokenIvInBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    }

    private string AesDecrypt(byte[] cipherBytes)
    {
        var key = Convert.FromBase64String(_options.TokenEncryptionKeyInBase64);
        var iv = Convert.FromBase64String(_options.TokenIvInBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
