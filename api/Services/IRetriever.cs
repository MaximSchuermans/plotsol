using Api.Models;

namespace Api.Services;

public interface IRetriever
{
  Task<List<RetrievedChunk>> RetrieveAsync(
    string query,
    string userId,
    int limit = 8,
    string? fileId = null,
    CancellationToken cancellationToken = default);

  Task<List<RetrievedChunk>> RetrieveAsync(
    IReadOnlyList<EmbeddingContentItem> queryContent,
    string userId,
    int limit = 8,
    string? fileId = null,
    CancellationToken cancellationToken = default);
}
