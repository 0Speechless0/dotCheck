using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotCheck.Domain.Entities;

public sealed class PaymentBill
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("startDate")]
    public DateTime StartDate { get; set; }

    [BsonElement("endDate")]
    public DateTime EndDate { get; set; }

    [BsonElement("itemIds")]
    public List<ObjectId> ItemIds { get; set; } = [];

    [BsonElement("totalAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalAmount { get; set; }

    [BsonElement("payers")]
    public List<PaymentPayer> Payers { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PaymentPayer
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("payableAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PayableAmount { get; set; }
}
