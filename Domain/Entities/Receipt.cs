using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotCheck.Domain.Entities;

public sealed class Receipt
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("paymentBillId")]
    public ObjectId PaymentBillId { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("userName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("amount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    [BsonElement("startDate")]
    public DateTime StartDate { get; set; }

    [BsonElement("endDate")]
    public DateTime EndDate { get; set; }

    [BsonElement("issuedAt")]
    public DateTime IssuedAt { get; set; }

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

    [BsonElement("signature")]
    public string Signature { get; set; } = string.Empty;
}
