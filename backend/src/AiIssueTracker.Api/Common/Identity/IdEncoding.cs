using SimpleBase;

namespace AiIssueTracker.Api.Common.Identity;

public static class IdEncoding
{
    public static string Encode(long id)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BitConverter.TryWriteBytes(bytes, id);

        Span<byte> bytesWithChecksum = stackalloc byte[bytes.Length + 1];
        bytes.CopyTo(bytesWithChecksum);
        bytesWithChecksum[^1] = CalculateChecksum(bytes);

        return Base32.Crockford.Encode(bytesWithChecksum).ToLowerInvariant();
    }

    public static bool TryDecode(string? encodedId, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(encodedId))
        {
            return false;
        }

        byte[] bytesWithChecksum;
        try
        {
            bytesWithChecksum = Base32.Crockford.Decode(encodedId);
        }
        catch
        {
            return false;
        }

        if (bytesWithChecksum.Length != sizeof(long) + 1)
        {
            return false;
        }

        var bytes = bytesWithChecksum.AsSpan()[..^1];
        var providedChecksum = bytesWithChecksum[^1];
        if (providedChecksum != CalculateChecksum(bytes))
        {
            return false;
        }

        id = BitConverter.ToInt64(bytes);
        return true;
    }

    private static byte CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        byte checksum = 0;
        foreach (var b in bytes)
        {
            checksum ^= b;
        }
        return checksum;
    }
}
