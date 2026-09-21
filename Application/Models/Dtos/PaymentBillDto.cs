using MongoDB.Bson;
using dotCheck.Domain.Entities;

namespace dotCheck.Application.DTOs;

public sealed class PaymentBillDto(
    PaymentBill bill,
    ObjectId userId,
    int paidUserCount)
{
    private PaymentPayer? UserPayer => bill.Payers.FirstOrDefault(x => x.UserId == userId);

    public string Id => bill.Id.ToString();
    public DateTime StartDate => bill.StartDate;
    public DateTime EndDate => bill.EndDate;
    public decimal TotalAmount => bill.TotalAmount;
    public int ItemCount => bill.ItemIds.Count;
    public IEnumerable<ObjectId> ItemIds => bill.ItemIds ?? Enumerable.Empty<ObjectId>();
    public int PayerCount => bill.Payers.Count;
    public int PaidUserCount => paidUserCount;
    public decimal PayableAmount => UserPayer?.PayableAmount ?? 0m;

    public bool PaidReceiptExists { get; init; }
}
