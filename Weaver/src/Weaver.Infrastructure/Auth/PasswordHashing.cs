using Microsoft.AspNetCore.Identity;
using Weaver.Domain;

namespace Weaver.Infrastructure.Auth;

public interface IPasswordHashingService
{
    string Hash(User user, string password);

    /// <summary>True if the password matches the stored hash.</summary>
    bool Verify(User user, string password);
}

public class PasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
