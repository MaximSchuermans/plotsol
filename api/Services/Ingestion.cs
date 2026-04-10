using System.Text.RegularExpressions;
using Api.Models;
using Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Text;
using MongoDB.Driver;
using UglyToad.PdfPig;

namespace Api.Services;

public sealed class Ingestion : IIngestion
{
  private const int MaxTokensPerLine = 120;
  private const int MaxTokensPerParagraph = 320;

  private readonly IMongoCollection<IngestedChunk> _chunks;
  private readonly IAtlasEmbeddingClient _embeddingClient;
  private readonly string _embeddingModel;

  public Ingestion(
    IMongoCollection<IngestedChunk> chunks,
    IAtlasEmbeddingClient embeddingClient,
    IOptions<AtlasEmbeddingSettings> settings)
  {
    _chunks = chunks;
    _embeddingClient = embeddingClient;
    _embeddingModel = settings?.Value?.Model ?? throw new ArgumentNullException(nameof(settings));
  }

  public async Task IngestPdfAsync(
    Stream pdfStream,
    string fileId,
    string userId,
    string fileName,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(fileId))
    {
      throw new ArgumentException("File id is required.", nameof(fileId));
    }

    if (string.IsNullOrWhiteSpace(userId))
    {
      throw new ArgumentException("User id is required.", nameof(userId));
    }

    using var bufferedStream = new MemoryStream();
    await pdfStream.CopyToAsync(bufferedStream, cancellationToken);
    bufferedStream.Position = 0;

    var pageTexts = ExtractPageTexts(bufferedStream);
    var chunkPayloads = ChunkPages(pageTexts);

    await DeleteChunksByFileIdAsync(fileId, cancellationToken);

    if (chunkPayloads.Count == 0)
    {
      return;
    }

    var chunkInputs = chunkPayloads
      .Select(static c => EmbeddingInput.FromText(c.Text))
      .ToList();
    var embeddings = await _embeddingClient.EmbedDocumentsAsync(chunkInputs, cancellationToken);
    if (embeddings.Count != chunkPayloads.Count)
    {
      throw new InvalidOperationException("Embedding response count did not match chunk count.");
    }

    var utcNow = DateTime.UtcNow;
    var documents = chunkPayloads.Select((chunkPayload, index) => new IngestedChunk
    {
      FileId = fileId,
      UserId = userId,
      FileName = fileName,
      ChunkIndex = index,
      PageNumber = chunkPayload.PageNumber,
      Text = chunkPayload.Text,
      Embedding = embeddings[index],
      Model = _embeddingModel,
      CreatedAt = utcNow
    }).ToList();

    await _chunks.InsertManyAsync(documents, cancellationToken: cancellationToken);
  }

  public async Task DeleteChunksByFileIdAsync(string fileId, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(fileId))
    {
      return;
    }

    await _chunks.DeleteManyAsync(c => c.FileId == fileId, cancellationToken);
  }

  private static List<PageText> ExtractPageTexts(Stream pdfStream)
  {
    using var document = PdfDocument.Open(pdfStream);
    var pages = new List<PageText>();

    foreach (var page in document.GetPages())
    {
      var normalizedText = NormalizeText(page.Text);
      if (!string.IsNullOrWhiteSpace(normalizedText))
      {
        pages.Add(new PageText(page.Number, normalizedText));
      }
    }

    return pages;
  }

  private static List<ChunkPayload> ChunkPages(IReadOnlyList<PageText> pages)
  {
    if (pages.Count == 0)
    {
      return [];
    }

    var chunks = new List<ChunkPayload>();
    foreach (var page in pages)
    {
#pragma warning disable SKEXP0050
      var lines = TextChunker.SplitPlainTextLines(page.Text, MaxTokensPerLine);
      var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokensPerParagraph);
#pragma warning restore SKEXP0050

      chunks.AddRange(
        paragraphs
          .Select(static chunk => chunk.Trim())
          .Where(static chunk => chunk.Length >= 30)
          .Select(chunk => new ChunkPayload(chunk, page.Number)));
    }

    return chunks;
  }

  private static string NormalizeText(string text)
    => Regex.Replace(text, "\\r\\n?", "\n").Trim();

  private sealed record PageText(int Number, string Text);

  private sealed record ChunkPayload(string Text, int PageNumber);
}
