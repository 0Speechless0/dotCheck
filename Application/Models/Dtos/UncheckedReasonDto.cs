using dotCheck.Domain.Entities;

namespace dotCheck.Application.DTOs;

public sealed class UncheckedReasonDto(
    UncheckedReason uncheckedReason,
    CheckItem checkItem)
{
    public string ItemId => checkItem.Id.ToString();
    public string ItemName => checkItem.ItemName;
    public string ForThing => checkItem.ForThing;
    public decimal Amount => checkItem.Amount;
    public string? InvoiceFilePath => checkItem.InvoiceFile?.RelativePath;
    public string UserId => uncheckedReason.UserId.ToString();
    public string UserName => uncheckedReason.UserName;
    public string Reason => uncheckedReason.Reason;
    public DateTime CreatedAt => uncheckedReason.CreatedAt;
    public DateTime UpdatedAt => uncheckedReason.UpdatedAt;
}
