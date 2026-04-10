using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Models;
using Api.Settings;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed class AtlasEmbeddingClient : IAtlasEmbeddingClient
{
  private const int EmbeddingBatchSize = 64;

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    PropertyNameCaseInsensitive = true
  };

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly AtlasEmbeddingSettings _settings;

  public AtlasEmbeddingClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AtlasEmbeddingSettings> settings)
  {
    _httpClientFactory = httpClientFactory;
    _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
  }

  public async Task<List<float[]>> EmbedDocumentsAsync(
    IReadOnlyList<EmbeddingInput> inputs,
    CancellationToken cancellationToken = default)
  {
    if (inputs.Count == 0)
    {
      return [];
    }

    var allEmbeddings = new List<float[]>(inputs.Count);
    for (var i = 0; i < inputs.Count; i += EmbeddingBatchSize)
    {
      var batch = inputs.Skip(i).Take(EmbeddingBatchSize).ToList();
      var embeddings = await CreateTextEmbeddingsAsync(batch, "document", cancellationToken);
      allEmbeddings.AddRange(embeddings);
    }

    return allEmbeddings;
  }

  public async Task<float[]> EmbedQueryAsync(
    IReadOnlyList<EmbeddingContentItem> content,
    CancellationToken cancellationToken = default)
  {
    if (content.Count == 0)
    {
      throw new ArgumentException("At least one query content item is required.", nameof(content));
    }

    var input = new EmbeddingInput(content);
    var embeddings = await CreateTextEmbeddingsAsync([input], "query", cancellationToken);
    var vector = embeddings.FirstOrDefault();
    return vector ?? throw new InvalidOperationException("Query embedding was empty.");
  }

  private async Task<List<float[]>> CreateTextEmbeddingsAsync(
    IReadOnlyList<EmbeddingInput> inputs,
    string inputType,
    CancellationToken cancellationToken)
  {
    var texts = inputs.Select(ToTextOnlyInput).ToList();
    var payload = new TextEmbeddingRequest(texts, _settings.Model, inputType);

    using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
    {
      Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

    var content = await SendAsync(request, cancellationToken);
    var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(content, JsonOptions)
      ?? throw new InvalidOperationException("Embedding response was empty.");

    return embeddingResponse.Data
      .OrderBy(static d => d.Index)
      .Select(static d => d.Embedding)
      .ToList();
  }

  private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var client = _httpClientFactory.CreateClient();
    using var response = await client.SendAsync(request, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new IngestionException((int)response.StatusCode, $"Embedding request failed: {(int)response.StatusCode} {content}");
    }

    return content;
  }

  private static string ToTextOnlyInput(EmbeddingInput input)
  {
    if (input.Content.Count == 0)
    {
      throw new ArgumentException("Embedding input content cannot be empty.", nameof(input));
    }

    var hasNonTextContent = input.Content.Any(static item => !string.Equals(item.Type, "text", StringComparison.OrdinalIgnoreCase));
    if (hasNonTextContent)
    {
      throw new InvalidOperationException("Only text embedding is supported.");
    }

    var text = string.Join("\n", input.Content.Select(static item => item.Text).Where(static value => !string.IsNullOrWhiteSpace(value)));
    if (string.IsNullOrWhiteSpace(text))
    {
      throw new ArgumentException("Text embedding input cannot be empty.", nameof(input));
    }

    return text;
  }

  private sealed record TextEmbeddingRequest(
    IReadOnlyList<string> Input,
    string Model,
    [property: JsonPropertyName("input_type")] string InputType);

  private sealed record EmbeddingResponse(List<EmbeddingData> Data);

  private sealed record EmbeddingData(int Index, float[] Embedding);
}
