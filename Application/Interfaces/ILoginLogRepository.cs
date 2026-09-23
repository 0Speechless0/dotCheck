using dotCheck.Domain.Entities;

namespace dotCheck.Application.Interfaces;

public interface ILoginLogRepository
{
    Task InsertAsync(UserLoginLog log, CancellationToken cancellationToken = default);
}
