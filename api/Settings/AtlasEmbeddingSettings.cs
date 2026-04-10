namespace Api.Settings;

public sealed class AtlasEmbeddingSettings
{
  public string ApiKey { get; set; } = null!;
  public string Endpoint { get; set; } = "https://ai.mongodb.com/v1/embeddings";
  public string Model { get; set; } = "voyage-4";
}
