using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class PayoutBillRepository(MongoDbContext context) : IPayoutBillRepository
{
    public Task InsertAsync(PayoutBill bill, CancellationToken cancellationToken = default) =>
        context.PayoutBills.InsertOneAsync(bill, cancellationToken: cancellationToken);

    public Task InsertManyAsync(
        IEnumerable<PayoutBill> bills,
        CancellationToken cancellationToken = default)
    {
        var list = bills.ToList();
        return list.Count == 0
            ? Task.CompletedTask
            : context.PayoutBills.InsertManyAsync(list, cancellationToken: cancellationToken);
    }

    public Task<PayoutBill?> FindByIdAsync(
        ObjectId id,
        CancellationToken cancellationToken = default) =>
        context.PayoutBills.Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PayoutBill>> FindByUserAsync(
        ObjectId userId,
        CancellationToken cancellationToken = default) =>
        await context.PayoutBills.Find(x => x.UserId == userId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByPaymentBillAndUserAsync(
        ObjectId paymentBillId,
        ObjectId userId,
        CancellationToken cancellationToken = default) =>
        context.PayoutBills.Find(x => x.PaymentBillId == paymentBillId && x.UserId == userId)
            .Limit(1)
            .AnyAsync(cancellationToken);

    public Task<bool> ExistsBySignatureAsync(
        string signature,
        CancellationToken cancellationToken = default) =>
        context.PayoutBills.Find(x => x.Signature == signature)
            .Limit(1)
            .AnyAsync(cancellationToken);

    public async Task UpdateAsync(PayoutBill bill, CancellationToken cancellationToken = default)
    {
        await context.PayoutBills.ReplaceOneAsync(
            x => x.Id == bill.Id,
            bill,
            cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<PayoutBill>> FindByUserWithDateRangeAsync(ObjectId userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var filter = Builders<PayoutBill>.Filter.Eq(x => x.UserId, userId)
            & Builders<PayoutBill>.Filter.Gte(x => x.CreatedAt, startDate)
            & Builders<PayoutBill>.Filter.Lte(x => x.CreatedAt, endDate);

        return await context.PayoutBills.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
