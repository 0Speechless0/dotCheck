using Microsoft.AspNetCore.Identity;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Services;

public sealed class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) =>
        _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
        is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
