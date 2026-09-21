using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Infrastructure.Mongo;

public sealed class ItemRepository(MongoDbContext context) : IItemRepository
{
    public async Task<IReadOnlyList<CheckItem>> FindAllByStatusAsync(
    ItemStatus status,
    CancellationToken cancellationToken = default)
    {
        return await context.Items.Find(x => x.Status == status)
            .SortBy(x => x.FormDate)
            .ToListAsync(cancellationToken);
    }
    public Task InsertAsync(CheckItem item, CancellationToken cancellationToken = default) =>
        context.Items.InsertOneAsync(item, cancellationToken: cancellationToken);

    public Task<CheckItem?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default) =>
        context.Items.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<CheckItem>> FindByIdsAsync(IEnumerable<ObjectId> ids, CancellationToken cancellationToken = default)
    {
        return await context.Items.Find(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<CheckItem>> FindApprovalCandidatesAsync(ObjectId userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CheckItem>.Filter.Eq(x => x.Status, ItemStatus.Pending)
        // & Builders<CheckItem>.Filter.Ne(x => x.OwnerUserId, userId)
        ;
        return await context.Items.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CheckItem>> FindByOwnerAsync(ObjectId ownerUserId, CancellationToken cancellationToken = default)
    {
        return await context.Items.Find(x => x.OwnerUserId == ownerUserId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<long> CountByStatusAsync(ItemStatus status, CancellationToken cancellationToken = default) =>
        context.Items.CountDocumentsAsync(x => x.Status == status, cancellationToken: cancellationToken);

    public async Task<decimal> SumAmountByStatusAsync(ItemStatus status, CancellationToken cancellationToken = default)
    {
        var result = await context.Items.Aggregate()
            .Match(x => x.Status == status)
            .Group(_ => 1, g => new { Total = g.Sum(x => x.Amount) })
            .FirstOrDefaultAsync(cancellationToken);

        return result?.Total ?? 0m;
    }

    public async Task<(IReadOnlyList<CheckItem> Items, long Total)> GetPagedByStatusAsync(
        ItemStatus status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        var filter = Builders<CheckItem>.Filter.Eq(x => x.Status, status);

        var countTask = context.Items.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = context.Items.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return (itemsTask.Result, countTask.Result);
    }

    public async Task<(IReadOnlyList<CheckItem> Items, long Total)> GetPagedByStatusExcludingStatusAsync(
        ItemStatus status,
        ObjectId userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await GetPagedByStatusAsync(status, skip, take, cancellationToken);
    }

    public async Task<bool> AddApprovalAsync(ObjectId itemId, ObjectId userId, CancellationToken cancellationToken = default)
    {
        var update = Builders<CheckItem>.Update
            .AddToSet(x => x.ApprovedUserIds, userId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await context.Items.UpdateOneAsync(
            x => x.Id == itemId && x.Status == ItemStatus.Pending,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<bool> SetStatusAsync(
        ObjectId[] itemId,
        ItemStatus expectedStatus,
        ItemStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CheckItem>.Update
            .Set(x => x.Status, newStatus)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (newStatus == ItemStatus.Confirmed)
        {
            builder = builder.Set(x => x.ConfirmedAt, DateTime.UtcNow);
        }

        var result = await context.Items.UpdateManyAsync(
            x => itemId.Contains(x.Id) && x.Status == expectedStatus,
            builder,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> SetStatusAsync(
        ObjectId itemId,
        ItemStatus expectedStatus,
        ItemStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CheckItem>.Update
            .Set(x => x.Status, newStatus)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (newStatus == ItemStatus.Confirmed)
        {
            builder = builder.Set(x => x.ConfirmedAt, DateTime.UtcNow);
        }

        var result = await context.Items.UpdateOneAsync(
            x => x.Id == itemId && x.Status == expectedStatus,
            builder,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    public async Task<int> MarkSettledFromConfirmedAsync(CancellationToken cancellationToken = default)
    {
        var update = Builders<CheckItem>.Update
            .Set(x => x.Status, ItemStatus.Settled)
            .Set(x => x.SettledAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await context.Items.UpdateManyAsync(
            x => x.Status == ItemStatus.Confirmed,
            update,
            cancellationToken: cancellationToken);

        return (int)result.ModifiedCount;
    }

    public async Task<bool> UpdateOwnedItemAndResetForReapprovalAsync(
        CheckItem item,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<CheckItem>.Update
            .Set(x => x.ItemName, item.ItemName)
            .Set(x => x.Amount, item.Amount)
            .Set(x => x.InvoiceFile, item.InvoiceFile)
            .Set(x => x.Status, ItemStatus.Pending)
            .Set(x => x.ApprovedUserIds, new List<ObjectId>())
            .Unset(x => x.ConfirmedAt)
            .Unset(x => x.SettledAt)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await context.Items.UpdateOneAsync(
            x => x.Id == item.Id
                && x.OwnerUserId == item.OwnerUserId
                && x.Status != ItemStatus.Settled,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }



}
