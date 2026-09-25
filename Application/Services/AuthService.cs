using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;
using dotCheck.Infrastructure.Mongo;
using dotCheck.Infrastructure.Security;

namespace dotCheck.Application.Services;

public sealed class AuthService(
    MongoDbContext context,
    PasswordService passwordService,
    IConfiguration configuration,
    IUserRepository userRepository,
    ILoginLogRepository loginLogRepository,
    UserAsymmetricKeyService keyService,
    HttpFingerprintService fingerprintService)
{
    public async Task<User?> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
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
            return (false, null, "密碼至少需要6碼");

        if (await context.Users.Find(x => x.UserName == userName).AnyAsync(cancellationToken))
            return (false, null, "帳號已存在");

        var keyPair = keyService.GenerateKeyPair();
        var user = new User
        {
            Id = ObjectId.GenerateNewId(),
            UserName = userName,
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            AsymmetricKeyId = keyPair.KeyId,
            PublicKeyPem = keyPair.PublicKeyPem,
            PrivateKeyProtectedPem = keyPair.ProtectedPrivateKeyPem
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

    public async Task RecordLoginAsync(
        User user,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var loginAt = DateTime.UtcNow;
        var loginLog = fingerprintService.CreateLoginLog(
            user.Id,
            httpContext.Request,
            loginAt);

        await loginLogRepository.InsertAsync(loginLog, cancellationToken);
    }

    public async Task<User> EnsureAsymmetricKeyAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(user.AsymmetricKeyId)
            && !string.IsNullOrWhiteSpace(user.PublicKeyPem)
            && !string.IsNullOrWhiteSpace(user.PrivateKeyProtectedPem))
        {
            return user;
        }

        var keyPair = keyService.GenerateKeyPair();
        await userRepository.UpdateAsymmetricKeyAsync(
            user.Id,
            keyPair.KeyId,
            keyPair.PublicKeyPem,
            keyPair.ProtectedPrivateKeyPem,
            cancellationToken);

        user.AsymmetricKeyId = keyPair.KeyId;
        user.PublicKeyPem = keyPair.PublicKeyPem;
        user.PrivateKeyProtectedPem = keyPair.ProtectedPrivateKeyPem;
        return user;
    }

    public async Task EnsureAdminAsync(CancellationToken cancellationToken = default)
    {
        var userName = configuration["Admin:UserName"]?.Trim();
        var password = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return;

        var existing = await context.Users.Find(x => x.UserName == userName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            await EnsureAsymmetricKeyAsync(existing, cancellationToken);
            return;
        }

        if (password.Length < 6)
            throw new InvalidOperationException("Admin:Password 至少需要6碼。");

        var keyPair = keyService.GenerateKeyPair();
        var admin = new User
        {
            Id = ObjectId.GenerateNewId(),
            UserName = userName,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            AsymmetricKeyId = keyPair.KeyId,
            PublicKeyPem = keyPair.PublicKeyPem,
            PrivateKeyProtectedPem = keyPair.ProtectedPrivateKeyPem
        };

        admin.PasswordHash = passwordService.Hash(admin, password);
        await context.Users.InsertOneAsync(admin, cancellationToken: cancellationToken);
    }
}
