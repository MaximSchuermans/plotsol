using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class FileMetadata
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string? Id { get; set; }

  [BsonElement("userId")]
  public string UserId { get; set; } = string.Empty;

  [BsonElement("username")]
  public string Username { get; set; } = string.Empty;

  [BsonElement("fileName")]
  public string FileName { get; set; } = string.Empty;

  [BsonElement("blobName")]
  public string BlobName { get; set; } = string.Empty;

  [BsonElement("blobUri")]
  public string BlobUri { get; set; } = string.Empty;

  [BsonElement("contentType")]
  public string ContentType { get; set; } = string.Empty;

  [BsonElement("size")]
  public long Size { get; set; }

  [BsonElement("uploadedAt")]
  public DateTime UploadedAt { get; set; }
}
