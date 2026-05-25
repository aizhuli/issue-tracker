namespace AiIssueTracker.Api.Common.Pagination;

public sealed class PagingOptions
{
    public const string SectionName = "Paging";

    /// <summary>Base64-encoded 32-byte AES key used to encrypt page tokens.</summary>
    public string TokenEncryptionKeyInBase64 { get; init; } = string.Empty;

    /// <summary>Base64-encoded 16-byte AES IV used to encrypt page tokens.</summary>
    public string TokenIvInBase64 { get; init; } = string.Empty;

    /// <summary>Default page size when the caller does not specify maxPageSize.</summary>
    public int DefaultMaxPageSize { get; init; } = 10;

    /// <summary>Hard ceiling on maxPageSize — requests above this are rejected.</summary>
    public int MaxMaxPageSize { get; init; } = 100;
}
