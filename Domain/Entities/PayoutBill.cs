using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotCheck.Domain.Entities;

public sealed class PayoutBill
{


    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("paymentBillId")]
    public ObjectId PaymentBillId { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("itemIds")]
    public List<ObjectId> ItemIds { get; set; } = [];

    [BsonElement("startDate")]
    public DateTime StartDate { get; set; }

    [BsonElement("endDate")]
    public DateTime EndDate { get; set; }

    [BsonElement("itemTotalAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ItemTotalAmount { get; set; }

    [BsonElement("totalUserCount")]
    public int TotalUserCount { get; set; }

    [BsonElement("payoutAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PayoutAmount { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("signedAt")]
    public DateTime? SignedAt { get; set; }

    [BsonElement("keyId")]
    public string KeyId { get; set; } = string.Empty;

    [BsonElement("publicKey")]
    public string PublicKey { get; set; } = string.Empty;

    [BsonElement("signatureAlgorithm")]
    public string SignatureAlgorithm { get; set; } = string.Empty;

    [BsonElement("verificationMethod")]
    public string VerificationMethod { get; set; } = string.Empty;

    [BsonElement("payload")]
    public string Payload { get; set; } = string.Empty;

    [BsonElement("payloadHash")]
    public string PayloadHash { get; set; } = string.Empty;

    [BsonElement("signature")]
    public string Signature { get; set; } = string.Empty;

    [BsonElement("signatureStatus")]
    public PayoutBillSignStatus SignatureStatus { get; set; } = PayoutBillSignStatus.Pending;

}
