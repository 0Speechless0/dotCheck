using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class LoginLogRepository(MongoDbContext context) : ILoginLogRepository
{
    public Task InsertAsync(
        UserLoginLog log,
        CancellationToken cancellationToken = default) =>
        context.UserLoginLogs.InsertOneAsync(log, cancellationToken: cancellationToken);
}
