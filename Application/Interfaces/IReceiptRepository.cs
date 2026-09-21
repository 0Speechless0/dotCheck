using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface IReceiptRepository
{
    Task InsertAsync(Receipt receipt, CancellationToken cancellationToken = default);
    Task<Receipt?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receipt>> FindByUserAsync(ObjectId userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receipt>> FindByPaymentBillAsync(ObjectId paymentBillId, CancellationToken cancellationToken = default);
    Task<Receipt?> FindByPaymentBillAndUserAsync(ObjectId paymentBillId, ObjectId userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySignatureAsync(string signature, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receipt>> FindByIdsAndUserAsync(IEnumerable<ObjectId> ids, ObjectId userId, CancellationToken cancellationToken = default);
}
