using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace dotCheck.Domain.Entities;

public sealed class UserLoginLog
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("loginAt")]
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [BsonElement("userAgent")]
    public string UserAgent { get; set; } = string.Empty;

    [BsonElement("accept")]
    public string Accept { get; set; } = string.Empty;

    [BsonElement("acceptLanguage")]
    public string AcceptLanguage { get; set; } = string.Empty;

    [BsonElement("acceptEncoding")]
    public string AcceptEncoding { get; set; } = string.Empty;

    [BsonElement("secChUa")]
    public string SecChUa { get; set; } = string.Empty;

    [BsonElement("secChUaMobile")]
    public string SecChUaMobile { get; set; } = string.Empty;

    [BsonElement("secChUaPlatform")]
    public string SecChUaPlatform { get; set; } = string.Empty;

    [BsonElement("httpFingerprint")]
    public string HttpFingerprint { get; set; } = string.Empty;
}
