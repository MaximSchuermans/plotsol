using Api.Models;

namespace Api.Services;

public interface IAtlasEmbeddingClient
{
  Task<List<float[]>> EmbedDocumentsAsync(
    IReadOnlyList<EmbeddingInput> inputs,
    CancellationToken cancellationToken = default);

  Task<float[]> EmbedQueryAsync(
    IReadOnlyList<EmbeddingContentItem> content,
    CancellationToken cancellationToken = default);
}
