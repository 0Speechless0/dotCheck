using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.DTOs;

public sealed class PayoutBillDto(PayoutBill bill)
{
    public string Id => bill.Id.ToString();
    public string PaymentBillId => bill.PaymentBillId.ToString();
    public string UserId => bill.UserId.ToString();
    public IReadOnlyList<string> ItemIds => bill.ItemIds.Select(x => x.ToString()).ToList();
    public DateTime StartDate => bill.StartDate;
    public DateTime EndDate => bill.EndDate;
    public decimal ItemTotalAmount => bill.ItemTotalAmount;
    public int ItemCount => bill.ItemIds.Count;
    public int TotalUserCount => bill.TotalUserCount;
    public decimal PayoutAmount => bill.PayoutAmount;
    public DateTime CreatedAt => bill.CreatedAt;
    public string KeyId => bill.KeyId;
    public string PublicKey => bill.PublicKey;
    public string SignatureAlgorithm => bill.SignatureAlgorithm;
    public string VerificationMethod => bill.VerificationMethod;
    public string PayloadHash => bill.PayloadHash;
    public string Signature => bill.Signature;
    public PayoutBillSignStatus SignatureStatus => bill.SignatureStatus;
}
