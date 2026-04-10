namespace Api.Models;

public sealed record EmbeddingInput(IReadOnlyList<EmbeddingContentItem> Content)
{
  public static EmbeddingInput FromText(string text)
    => new([EmbeddingContentItem.FromText(text)]);
}
