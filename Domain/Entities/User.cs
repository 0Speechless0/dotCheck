using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using dotCheck.Domain.Enums;

namespace dotCheck.Domain.Entities;

public sealed class User
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userName")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("role")]
    public UserRole Role { get; set; } = UserRole.User;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
