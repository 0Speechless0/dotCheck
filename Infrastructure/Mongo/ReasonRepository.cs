using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class ReasonRepository(MongoDbContext context) : IReasonRepository
{
    public async Task UpsertAsync(UncheckedReason reason, CancellationToken cancellationToken = default)
    {
        reason.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<UncheckedReason>.Filter.Eq(x => x.ItemId, reason.ItemId)
            & Builders<UncheckedReason>.Filter.Eq(x => x.UserId, reason.UserId);

        var update = Builders<UncheckedReason>.Update
            .Set(x => x.UserName, reason.UserName)
            .Set(x => x.Reason, reason.Reason)
            .Set(x => x.UpdatedAt, reason.UpdatedAt)
            .SetOnInsert(x => x.Id, ObjectId.GenerateNewId())
            .SetOnInsert(x => x.CreatedAt, reason.CreatedAt);

        await context.UncheckedReasons.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<IReadOnlyList<UncheckedReason>> FindByUserAsync(ObjectId userId, CancellationToken cancellationToken = default)
    {
        return await context.UncheckedReasons.Find(x => x.UserId == userId)
            .SortByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByItemAsync(ObjectId itemId, CancellationToken cancellationToken = default) =>
        context.UncheckedReasons.DeleteManyAsync(x => x.ItemId == itemId, cancellationToken);
}
