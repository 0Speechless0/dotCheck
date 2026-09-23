using MongoDB.Bson;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;
using dotCheck.Infrastructure.Mongo;
using dotCheck.Infrastructure.Files;

namespace dotCheck.Application.Services;

public sealed class ItemService(
    IItemRepository itemRepository,
    IReasonRepository reasonRepository,
    InvoiceFileService fileService)
{
    public Task<CheckItem?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken = default) =>
        itemRepository.FindByIdAsync(id, cancellationToken);

    public async Task<CheckItem> CreateAsync(
        ObjectId ownerUserId,
        string ownerUserName,
        string itemName,
        string forThing,
        decimal amount,
        Microsoft.AspNetCore.Components.Forms.IBrowserFile? invoice,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("項目名稱不可為空。", nameof(itemName));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        var item = new CheckItem
        {
            Id = ObjectId.GenerateNewId(),
            FormDate = DateTime.Now,
            OwnerUserId = ownerUserId,
            OwnerUserName = ownerUserName,
            ItemName = itemName.Trim(),
            ForThing = forThing.Trim(),
            Amount = amount,
            Status = ItemStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (invoice is not null)
        {
            item.InvoiceFile = await fileService.SaveAsync(invoice, item.Id, cancellationToken);
        }

        try
        {
            await itemRepository.InsertAsync(item, cancellationToken);
            return item;
        }
        catch
        {
            if (item.InvoiceFile is not null)
            {
                await fileService.DeleteAsync(item.InvoiceFile.RelativePath);
            }
            throw;
        }
    }

    public Task<IReadOnlyList<CheckItem>> FindByOwnerAsync(ObjectId ownerUserId, CancellationToken cancellationToken = default) =>
        itemRepository.FindByOwnerAsync(ownerUserId, cancellationToken);

    public async Task UpdateOwnedItemAsync(
        ObjectId userId,
        ObjectId itemId,
        string itemName,
        decimal amount,
        Microsoft.AspNetCore.Components.Forms.IBrowserFile? newInvoice,
        CancellationToken cancellationToken = default)
    {
        var item = await itemRepository.FindByIdAsync(itemId, cancellationToken)
            ?? throw new InvalidOperationException("找不到項目。");

        if (item.OwnerUserId != userId)
            throw new UnauthorizedAccessException("只有項目申請人可以修改項目。");

        if (item.Status == ItemStatus.Settled)
            throw new InvalidOperationException("已結清項目不可修改。");

        itemName = itemName.Trim();
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            throw new ArgumentException("項目資料不正確。");

        var oldRelativePath = item.InvoiceFile?.RelativePath;
        InvoiceFileInfo? newInvoiceInfo = item.InvoiceFile;

        if (newInvoice is not null)
        {
            newInvoiceInfo = await fileService.SaveAsync(newInvoice, item.Id, cancellationToken);
        }

        item.ItemName = itemName;
        item.Amount = amount;
        item.InvoiceFile = newInvoiceInfo;

        var databaseUpdated = false;
        try
        {
            if (!await itemRepository.UpdateOwnedItemAndResetForReapprovalAsync(item, cancellationToken))
                throw new InvalidOperationException("項目更新失敗或項目已結清。");

            databaseUpdated = true;
            await reasonRepository.DeleteByItemAsync(item.Id, cancellationToken);

            if (newInvoice is not null && !string.IsNullOrWhiteSpace(oldRelativePath))
            {
                await fileService.DeleteAsync(oldRelativePath);
            }
        }
        catch
        {
            if (!databaseUpdated && newInvoice is not null && newInvoiceInfo is not null)
            {
                await fileService.DeleteAsync(newInvoiceInfo.RelativePath);
            }
            throw;
        }
    }

    public async Task<(IReadOnlyList<CheckItem> Items, long Total)> GetPageAsync(
        ItemStatus status,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await itemRepository.GetPagedByStatusAsync(status, skip, take, cancellationToken);

    public async Task<(long Count, decimal TotalAmount)> GetSummaryAsync(
        ItemStatus status,
        CancellationToken cancellationToken = default)
    {
        var countTask = itemRepository.CountByStatusAsync(status, cancellationToken);
        var totalTask = itemRepository.SumAmountByStatusAsync(status, cancellationToken);
        await Task.WhenAll(countTask, totalTask);
        return (countTask.Result, totalTask.Result);
    }



    public async Task<IReadOnlyList<CheckItem>> GetPendingItemsAsync() =>
        await itemRepository.FindAllByStatusAsync(ItemStatus.Pending, CancellationToken.None);

    public async Task<IReadOnlyList<CheckItem>> GetConfirmedItemsAsync() =>
        await itemRepository.FindAllByStatusAsync(ItemStatus.Confirmed, CancellationToken.None);

    public async Task AdminApproveItemsAsync(List<string> itemIds) =>
        await itemRepository.SetStatusAsync(
            itemIds.Select(id => ObjectId.Parse(id)).ToArray(),
            ItemStatus.Pending,
            ItemStatus.Confirmed,
            CancellationToken.None);
}
