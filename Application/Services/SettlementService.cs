using MongoDB.Bson;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.Services;

public sealed class SettlementService(IItemRepository itemRepository)
{
    public async Task<IReadOnlyList<CheckItem>> GetConfirmedAsync(CancellationToken cancellationToken = default)
    {
        var result = await itemRepository.GetPagedByStatusAsync(ItemStatus.Confirmed, 0, 200, cancellationToken);
        return result.Items;
    }

    public async Task<int> SettleAsync(CancellationToken cancellationToken = default)
    {

        return await itemRepository.MarkSettledFromConfirmedAsync(cancellationToken);
    }
}
