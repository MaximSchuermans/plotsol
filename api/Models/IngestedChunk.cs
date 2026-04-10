using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class IngestedChunk
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("fileId")]
  public string FileId { get; set; } = string.Empty;

  [BsonElement("userId")]
  public string UserId { get; set; } = string.Empty;

  [BsonElement("fileName")]
  public string FileName { get; set; } = string.Empty;

  [BsonElement("chunkIndex")]
  public int ChunkIndex { get; set; }

  [BsonElement("pageNumber")]
  public int PageNumber { get; set; }

  [BsonElement("text")]
  public string Text { get; set; } = string.Empty;

  [BsonElement("embedding")]
  public float[] Embedding { get; set; } = [];

  [BsonElement("model")]
  public string Model { get; set; } = string.Empty;

  [BsonElement("createdAt")]
  public DateTime CreatedAt { get; set; }
}
