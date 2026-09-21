using MongoDB.Bson;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.Services;

public sealed class ApprovalService(
    IItemRepository itemRepository,
    IReasonRepository reasonRepository,
    IUserRepository userRepository)
{
    public Task<IReadOnlyList<CheckItem>> GetCandidatesAsync(ObjectId userId, CancellationToken cancellationToken = default) =>
        itemRepository.FindApprovalCandidatesAsync(userId, cancellationToken);


    public async Task SubmitApprovalsAsync(
        ObjectId userId,
        string userName,
        IReadOnlyCollection<ObjectId> selectedItemIds,
        IReadOnlyCollection<ObjectId> unselectedItemIds,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (unselectedItemIds.Count > 0 && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("存在未勾選項目時，必須填寫理由。");

        var totalUsers = await userRepository.CountActiveAsync(cancellationToken);
        if (totalUsers <= 0)
            throw new InvalidOperationException("目前沒有可用的系統使用者。");

        foreach (var itemId in selectedItemIds)
        {
            await itemRepository.AddApprovalAsync(itemId, userId, cancellationToken);
        }

        if (unselectedItemIds.Count > 0)
        {
            foreach (var itemId in unselectedItemIds)
            {
                await reasonRepository.UpsertAsync(new UncheckedReason
                {
                    Id = ObjectId.GenerateNewId(),
                    ItemId = itemId,
                    UserId = userId,
                    UserName = userName,
                    Reason = reason!.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        foreach (var itemId in selectedItemIds)
        {
            var item = await itemRepository.FindByIdAsync(itemId, cancellationToken);
            if (item is null || item.Status != ItemStatus.Pending)
                continue;

            if (item.ApprovedUserIds.Count * 2 > totalUsers)
            {
                await itemRepository.SetStatusAsync(itemId, ItemStatus.Pending, ItemStatus.Confirmed, cancellationToken);
            }
        }
    }
}
