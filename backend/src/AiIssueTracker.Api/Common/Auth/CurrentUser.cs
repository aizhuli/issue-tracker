using System.Security.Claims;
using AiIssueTracker.Api.Common.Exceptions;

namespace AiIssueTracker.Api.Common.Auth;

public interface ICurrentUser
{
    long UserId { get; }
    bool IsAuthenticated { get; }
    bool TryGetUserId(out long userId);
}

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true &&
        TryGetUserId(out _);

    public long UserId =>
        TryGetUserId(out var id)
            ? id
            : throw new UnauthorizedDomainException(
                "Current user is not authenticated.",
                "auth:current_user:unauthenticated");

    public bool TryGetUserId(out long userId)
    {
        userId = 0;
        var raw = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }
}
