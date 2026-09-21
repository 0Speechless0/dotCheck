using MongoDB.Bson;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.Interfaces;

public interface IItemRepository
{
    Task InsertAsync(CheckItem item, CancellationToken cancellationToken = default);
    Task<CheckItem?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckItem>> FindByIdsAsync(IEnumerable<ObjectId> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckItem>> FindApprovalCandidatesAsync(ObjectId userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckItem>> FindByOwnerAsync(ObjectId ownerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckItem>> FindAllByStatusAsync(ItemStatus status, CancellationToken cancellationToken = default);
    Task<long> CountByStatusAsync(ItemStatus status, CancellationToken cancellationToken = default);
    Task<decimal> SumAmountByStatusAsync(ItemStatus status, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CheckItem> Items, long Total)> GetPagedByStatusAsync(ItemStatus status, int skip, int take, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CheckItem> Items, long Total)> GetPagedByStatusExcludingStatusAsync(ItemStatus status, ObjectId userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<bool> AddApprovalAsync(ObjectId itemId, ObjectId userId, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(ObjectId itemId, ItemStatus expectedStatus, ItemStatus newStatus, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(ObjectId[] itemId, ItemStatus expectedStatus, ItemStatus newStatus, CancellationToken cancellationToken = default);
    Task<int> MarkSettledFromConfirmedAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateOwnedItemAndResetForReapprovalAsync(CheckItem item, CancellationToken cancellationToken = default);
}
