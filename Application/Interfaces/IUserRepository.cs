using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
    Task<User?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<long> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> FindAllActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> SearchActiveAsync(string keyword, int limit = 20, CancellationToken cancellationToken = default);
    Task InsertAsync(User user, CancellationToken cancellationToken = default);
}
