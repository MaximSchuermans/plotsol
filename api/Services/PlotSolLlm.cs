using System.Text;
using System.Text.Json;
using Api.Models;
using Api.Settings;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed class PlotSolLlm : IPlotSolLlm
{
  private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
  private const string GroqModel = "openai/gpt-oss-20b";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly GroqSettings _groqSettings;

  public PlotSolLlm(IHttpClientFactory httpClientFactory, IOptions<GroqSettings> groqSettings)
  {
    _httpClientFactory = httpClientFactory;
    _groqSettings = groqSettings?.Value ?? throw new ArgumentNullException(nameof(groqSettings));
  }

  public async Task<PlotSolLlmCompletionResult> CreateChatCompletionAsync(
    IReadOnlyCollection<ChatMessage> messages,
    CancellationToken cancellationToken = default)
  {
    var groqApiKey = string.IsNullOrWhiteSpace(_groqSettings.Api)
      ? _groqSettings.ApiKey
      : _groqSettings.Api;

    if (string.IsNullOrWhiteSpace(groqApiKey))
    {
      throw new InvalidOperationException("Groq API key is not configured on the server.");
    }

    var payload = new
    {
      model = GroqModel,
      messages,
      temperature = 0.3
    };

    var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint)
    {
      Content = new StringContent(
        JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        Encoding.UTF8,
        "application/json")
    };
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groqApiKey);

    var client = _httpClientFactory.CreateClient();
    var response = await client.SendAsync(request, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

    return new PlotSolLlmCompletionResult(content, contentType, (int)response.StatusCode);
  }
}
