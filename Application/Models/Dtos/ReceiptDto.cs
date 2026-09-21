using dotCheck.Domain.Entities;

namespace dotCheck.Application.DTOs;

public sealed class ReceiptDto(Receipt receipt)
{
    public string Id => receipt.Id.ToString();
    public string PaymentBillId => receipt.PaymentBillId.ToString();
    public string UserId => receipt.UserId.ToString();
    public string UserName => receipt.UserName;
    public decimal Amount => receipt.Amount;
    public DateTime StartDate => receipt.StartDate;
    public DateTime EndDate => receipt.EndDate;
    public DateTime IssuedAt => receipt.IssuedAt;
    public string KeyId => receipt.KeyId;
    public string PublicKey => receipt.PublicKey;
    public string SignatureAlgorithm => receipt.SignatureAlgorithm;
    public string VerificationMethod => receipt.VerificationMethod;
    public string Payload => receipt.Payload;
    public string Signature => receipt.Signature;
}
