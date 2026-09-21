using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class PaymentBillRepository(MongoDbContext context) : IPaymentBillRepository
{
    public Task InsertAsync(PaymentBill bill, CancellationToken cancellationToken = default) =>
        context.PaymentBills.InsertOneAsync(bill, cancellationToken: cancellationToken);

    public Task<PaymentBill?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default) =>
        context.PaymentBills.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentBill>> FindByUserAsync(
        ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<PaymentBill>.Filter.ElemMatch(
            x => x.Payers,
            payer => payer.UserId == userId);

        return await context.PaymentBills.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasAnyItemAsync(
        IEnumerable<ObjectId> itemIds,
        CancellationToken cancellationToken = default)
    {
        var ids = itemIds.Distinct().ToArray();
        if (ids.Length == 0)
            return false;

        var filter = Builders<PaymentBill>.Filter.AnyIn(x => x.ItemIds, ids);
        return await context.PaymentBills.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }
}
