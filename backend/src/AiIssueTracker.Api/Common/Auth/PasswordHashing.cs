using AiIssueTracker.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace AiIssueTracker.Api.Common.Auth;

public interface IPasswordHashing
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string providedPassword);
}

public class PasswordHashing(IPasswordHasher<User> hasher) : IPasswordHashing
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string providedPassword)
    {
        var result = hasher.VerifyHashedPassword(user, passwordHash, providedPassword);
        return result == PasswordVerificationResult.Success
               || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
