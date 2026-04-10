using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class PersistedChatMessage
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("threadId")]
  public string ThreadId { get; set; } = string.Empty;

  [BsonElement("role")]
  public string Role { get; set; } = string.Empty;

  [BsonElement("content")]
  public string Content { get; set; } = string.Empty;

  [BsonElement("sources")]
  public string? SourcesJson { get; set; }

  [BsonElement("fileId")]
  public string? FileId { get; set; }

  [BsonElement("createdAt")]
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
