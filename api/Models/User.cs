using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class User
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("username")]
  public string Username { get; set; } = string.Empty;

  [BsonElement("normalizedUsername")]
  public string NormalizedUsername { get; set; } = string.Empty;

  [BsonElement("passwordHash")]
  public string PasswordHash { get; set; } = string.Empty;
}
