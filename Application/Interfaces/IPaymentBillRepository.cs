using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface IPaymentBillRepository
{
    Task InsertAsync(PaymentBill bill, CancellationToken cancellationToken = default);
    Task<PaymentBill?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentBill>> FindByUserWithDateRangeAsync(ObjectId userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentBill>> FindByUserAsync(ObjectId userId, CancellationToken cancellationToken = default);
    Task<bool> HasAnyItemAsync(IEnumerable<ObjectId> itemIds, CancellationToken cancellationToken = default);
}
