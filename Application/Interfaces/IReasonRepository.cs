using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface IReasonRepository
{
    Task UpsertAsync(UncheckedReason reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UncheckedReason>> FindByUserAsync(ObjectId userId, CancellationToken cancellationToken = default);
    Task DeleteByItemAsync(ObjectId itemId, CancellationToken cancellationToken = default);
}
