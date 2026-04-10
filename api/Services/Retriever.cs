using Api.Models;
using Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Api.Services;

public sealed class Retriever : IRetriever
{
  private readonly IMongoCollection<IngestedChunk> _chunks;
  private readonly IAtlasEmbeddingClient _embeddingClient;
  private readonly MongoDbSettings _mongoSettings;

  public Retriever(
    IMongoCollection<IngestedChunk> chunks,
    IAtlasEmbeddingClient embeddingClient,
    IOptions<MongoDbSettings> mongoSettings)
  {
    _chunks = chunks;
    _embeddingClient = embeddingClient;
    _mongoSettings = mongoSettings?.Value ?? throw new ArgumentNullException(nameof(mongoSettings));
  }

  public async Task<List<RetrievedChunk>> RetrieveAsync(
    string query,
    string userId,
    int limit = 8,
    string? fileId = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(query))
    {
      return [];
    }

    return await RetrieveAsync(
      [EmbeddingContentItem.FromText(query)],
      userId,
      limit,
      fileId,
      cancellationToken);
  }

  public async Task<List<RetrievedChunk>> RetrieveAsync(
    IReadOnlyList<EmbeddingContentItem> queryContent,
    string userId,
    int limit = 8,
    string? fileId = null,
    CancellationToken cancellationToken = default)
  {
    if (queryContent.Count == 0)
    {
      return [];
    }

    if (string.IsNullOrWhiteSpace(userId))
    {
      throw new ArgumentException("User id is required.", nameof(userId));
    }

    var effectiveLimit = Math.Clamp(limit, 1, 50);
    var queryEmbedding = await _embeddingClient.EmbedQueryAsync(queryContent, cancellationToken);

    var filter = new BsonDocument("userId", userId);
    if (!string.IsNullOrWhiteSpace(fileId))
    {
      filter.Add("fileId", fileId);
    }

    var vectorSearchStage = BuildVectorSearchStage(
      queryEmbedding,
      effectiveLimit,
      Math.Max(effectiveLimit * 20, 100),
      filter);

    var projectStage = new BsonDocument("$project", new BsonDocument
    {
      { "chunkId", new BsonDocument("$toString", "$_id") },
      { "fileId", 1 },
      { "fileName", 1 },
      { "chunkIndex", 1 },
      { "pageNumber", 1 },
      { "text", 1 },
      { "score", new BsonDocument("$meta", "vectorSearchScore") }
    });

    List<BsonDocument> documents;
    try
    {
      var pipeline = new[] { vectorSearchStage, projectStage };
      documents = await _chunks.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
    }
    catch (MongoCommandException ex) when (ex.Message.Contains("needs to be indexed as filter", StringComparison.OrdinalIgnoreCase))
    {
      var fallbackLimit = Math.Clamp(effectiveLimit * 15, 100, 1000);
      var fallbackVectorStage = BuildVectorSearchStage(
        queryEmbedding,
        fallbackLimit,
        Math.Max(fallbackLimit * 20, 100),
        filter: null);

      var matchStage = new BsonDocument("$match", filter);
      var limitStage = new BsonDocument("$limit", effectiveLimit);
      var fallbackPipeline = new[] { fallbackVectorStage, matchStage, limitStage, projectStage };
      documents = await _chunks.Aggregate<BsonDocument>(fallbackPipeline).ToListAsync(cancellationToken);
    }

    return documents.Select(static doc => new RetrievedChunk(
      ChunkId: doc.GetValue("chunkId", string.Empty).AsString,
      FileId: doc.GetValue("fileId", string.Empty).AsString,
      FileName: doc.GetValue("fileName", string.Empty).AsString,
      ChunkIndex: doc.GetValue("chunkIndex", 0).ToInt32(),
      PageNumber: doc.GetValue("pageNumber", 0).ToInt32(),
      Text: doc.GetValue("text", string.Empty).AsString,
      Score: doc.GetValue("score", 0d).ToDouble())).ToList();
  }

  private BsonDocument BuildVectorSearchStage(
    IReadOnlyList<float> queryEmbedding,
    int limit,
    int numCandidates,
    BsonDocument? filter)
  {
    var vectorSearch = new BsonDocument
    {
      { "index", _mongoSettings.VectorIndexName },
      { "path", "embedding" },
      { "queryVector", new BsonArray(queryEmbedding.Select(static v => (BsonValue)v)) },
      { "numCandidates", numCandidates },
      { "limit", limit }
    };

    if (filter is not null)
    {
      vectorSearch.Add("filter", filter);
    }

    return new BsonDocument("$vectorSearch", vectorSearch);
  }

}
