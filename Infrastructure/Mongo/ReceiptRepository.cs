using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class ReceiptRepository(MongoDbContext context) : IReceiptRepository
{
    public Task InsertAsync(Receipt receipt, CancellationToken cancellationToken = default) =>
        context.Receipts.InsertOneAsync(receipt, cancellationToken: cancellationToken);

    public Task<Receipt?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default) =>
        context.Receipts.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Receipt>> FindByUserAsync(
        ObjectId userId,
        CancellationToken cancellationToken = default) =>
        await context.Receipts.Find(x => x.UserId == userId)
            .SortByDescending(x => x.IssuedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Receipt>> FindByPaymentBillAsync(
        ObjectId paymentBillId,
        CancellationToken cancellationToken = default) =>
        await context.Receipts.Find(x => x.PaymentBillId == paymentBillId)
            .SortBy(x => x.UserId)
            .ToListAsync(cancellationToken);

    public Task<Receipt?> FindByPaymentBillAndUserAsync(
        ObjectId paymentBillId,
        ObjectId userId,
        CancellationToken cancellationToken = default) =>
        context.Receipts.Find(x => x.PaymentBillId == paymentBillId && x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsBySignatureAsync(
        string signature,
        CancellationToken cancellationToken = default) =>
        context.Receipts.Find(x => x.Signature == signature)
            .Limit(1)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Receipt>> FindByIdsAndUserAsync(
        IEnumerable<ObjectId> ids,
        ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var idArray = ids.Distinct().ToArray();
        if (idArray.Length == 0)
            return [];

        var filter = Builders<Receipt>.Filter.In(x => x.Id, idArray)
            & Builders<Receipt>.Filter.Eq(x => x.UserId, userId);

        return await context.Receipts.Find(filter)
            .SortByDescending(x => x.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Receipt>> FindByUserAndDateRangeAsync(ObjectId userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var filter = Builders<Receipt>.Filter.Eq(x => x.UserId, userId)
            & Builders<Receipt>.Filter.Gte(x => x.IssuedAt, startDate)
            & Builders<Receipt>.Filter.Lte(x => x.IssuedAt, endDate);

        return await context.Receipts.Find(filter)
            .SortByDescending(x => x.IssuedAt)
            .ToListAsync(cancellationToken);
    }

}
