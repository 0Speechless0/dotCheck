using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotCheck.Domain.Entities;

public sealed class UncheckedReason
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("itemId")]
    public ObjectId ItemId { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("userName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
