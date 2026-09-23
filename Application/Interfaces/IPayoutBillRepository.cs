using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface IPayoutBillRepository
{
    Task InsertAsync(PayoutBill bill, CancellationToken cancellationToken = default);
    Task UpdateAsync(PayoutBill bill, CancellationToken cancellationToken = default);
    Task InsertManyAsync(IEnumerable<PayoutBill> bills, CancellationToken cancellationToken = default);
    Task<PayoutBill?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayoutBill>> FindByUserAsync(ObjectId userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPaymentBillAndUserAsync(ObjectId paymentBillId, ObjectId userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySignatureAsync(string signature, CancellationToken cancellationToken = default);

}
