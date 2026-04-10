using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class ChatThread
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("userId")]
  public string UserId { get; set; } = string.Empty;

  [BsonElement("title")]
  public string Title { get; set; } = "New chat";

  [BsonElement("createdAt")]
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  [BsonElement("updatedAt")]
  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
