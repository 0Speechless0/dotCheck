using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using dotCheck.Domain.Enums;

namespace dotCheck.Domain.Entities;

public sealed class CheckItem
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("formDate")]
    public DateTime FormDate { get; set; }

    [BsonElement("ownerUserId")]
    public ObjectId OwnerUserId { get; set; }

    [BsonElement("ownerUserName")]
    public string OwnerUserName { get; set; } = string.Empty;

    [BsonElement("itemName")]
    public string ItemName { get; set; } = string.Empty;
    [BsonElement("forThing")]
    public string ForThing { get; set; } = string.Empty;

    [BsonElement("amount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    [BsonElement("invoiceFile")]
    public InvoiceFileInfo? InvoiceFile { get; set; }

    [BsonElement("status")]
    public ItemStatus Status { get; set; } = ItemStatus.Pending;

    [BsonElement("approvedUserIds")]
    public List<ObjectId> ApprovedUserIds { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("confirmedAt")]
    public DateTime? ConfirmedAt { get; set; }

    [BsonElement("settledAt")]
    public DateTime? SettledAt { get; set; }
}
