using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;
using dotCheck.Infrastructure.Mongo;

namespace dotCheck.Application.Services;

public sealed class AuthService(
    MongoDbContext context,
    PasswordService passwordService,
    IConfiguration configuration)
{
    public async Task<User?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.Find(x => x.UserName == userName && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return user is not null && passwordService.Verify(user, password) ? user : null;
    }

    public async Task<(bool Success, User? User, string? ErrorMessage)> RegisterAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (password.Length < 6)
        {
            return (false, null, "密碼至少需要6碼");
        }

        if (await context.Users.Find(x => x.UserName == userName).AnyAsync(cancellationToken))
        {
            return (false, null, "帳號已存在");
        }

        var user = new User
        {
            Id = ObjectId.GenerateNewId(),
            UserName = userName,
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordService.Hash(user, password);

        try
        {
            await context.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return (true, user, null);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            return (false, null, "帳號已存在");
        }
    }

    public async Task EnsureAdminAsync(CancellationToken cancellationToken = default)
    {
        var userName = configuration["Admin:UserName"]?.Trim();
        var password = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var existing = await context.Users.Find(x => x.UserName == userName).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return;
        }

        if (password.Length < 6)
        {
            throw new InvalidOperationException("Admin:Password 至少需要6碼。");
        }

        var admin = new User
        {
            Id = ObjectId.GenerateNewId(),
            UserName = userName,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = passwordService.Hash(admin, password);
        await context.Users.InsertOneAsync(admin, cancellationToken: cancellationToken);
    }
}
