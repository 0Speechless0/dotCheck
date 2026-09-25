using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class UserRepository(MongoDbContext context) : IUserRepository
{
    public Task<User?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default) =>
        context.Users.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync(cancellationToken);

    public Task<User?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        context.Users.Find(x => x.UserName == userName && x.IsActive).FirstOrDefaultAsync(cancellationToken);

    public Task<long> CountActiveAsync(CancellationToken cancellationToken = default) =>
        context.Users.CountDocumentsAsync(x => x.IsActive, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<User>> FindAllActiveAsync(CancellationToken cancellationToken = default) =>
        await context.Users.Find(x => x.IsActive)
            .SortBy(x => x.UserName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> SearchActiveAsync(
        string keyword,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        keyword = keyword.Trim();

        if (string.IsNullOrWhiteSpace(keyword))
            return await context.Users.Find(x => x.IsActive)
                .SortBy(x => x.UserName)
                .Limit(limit)
                .ToListAsync(cancellationToken);

        var regex = new BsonRegularExpression(Regex.Escape(keyword), "i");
        var filter = Builders<User>.Filter.Eq(x => x.IsActive, true)
            & Builders<User>.Filter.Regex(x => x.UserName, regex);

        return await context.Users.Find(filter)
            .SortBy(x => x.UserName)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(User user, CancellationToken cancellationToken = default) =>
        context.Users.InsertOneAsync(user, cancellationToken: cancellationToken);

    public Task UpdateAsymmetricKeyAsync(
        ObjectId userId,
        string keyId,
        string publicKeyPem,
        string privateKeyProtectedPem,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<User>.Update
            .Set(x => x.AsymmetricKeyId, keyId)
            .Set(x => x.PublicKeyPem, publicKeyPem)
            .Set(x => x.PrivateKeyProtectedPem, privateKeyProtectedPem);

        return UpdateAsync();

        async Task UpdateAsync()
        {
            await context.Users.UpdateOneAsync(
                x => x.Id == userId,
                update,
                cancellationToken: cancellationToken);
        }
    }
}
