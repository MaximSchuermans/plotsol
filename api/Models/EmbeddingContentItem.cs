namespace Api.Models;

public sealed record EmbeddingContentItem(
  string Type,
  string? Text = null,
  string? ImageUrl = null,
  string? ImageBase64 = null)
{
  public static EmbeddingContentItem FromText(string text)
    => new("text", Text: text);

  public static EmbeddingContentItem FromImageUrl(string imageUrl)
    => new("image_url", ImageUrl: imageUrl);

  public static EmbeddingContentItem FromImageBase64(string imageBase64)
    => new("image_base64", ImageBase64: imageBase64);
}
