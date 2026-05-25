using AiIssueTracker.Api.Common.Exceptions;

namespace AiIssueTracker.Api.Common.Pagination;

public static class PaginationExceptions
{
    public static BadRequestException InvalidMaxPageSize()
        => new("The requested maxPageSize is invalid.", "paging:validation:max_page_size_invalid");

    public static BadRequestException InvalidPageToken()
        => new("The provided pageToken is invalid or has expired.", "paging:validation:page_token_invalid");
}
