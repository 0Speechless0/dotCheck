using dotCheck.Domain.Entities;
using MongoDB.Bson;

public sealed class UncheckedReasonDto(
    UncheckedReason? uncheckedReason,
    CheckItem checkItem)
{
    public ObjectId ItemId => checkItem.Id;

    public string ItemName => checkItem.ItemName;

    public decimal Amount => checkItem.Amount;

    public string? InvoiceFilePath =>
        checkItem.InvoiceFile?.RelativePath;

    public ObjectId UserId => checkItem.OwnerUserId;

    public string UserName => uncheckedReason?.UserName;

    public string Reason => uncheckedReason?.Reason;

    public DateTimeOffset? CreatedAt => uncheckedReason?.CreatedAt;
    public DateTimeOffset? UpdatedAt => uncheckedReason?.UpdatedAt;
}