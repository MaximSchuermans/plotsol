namespace Api.Models;

public record ChatMessage(string Role, string Content);

public sealed class ChatCompletionRequest
{
  public List<ChatMessage> Messages { get; init; } = [];
  public int? TopK { get; init; }
  public string? FileId { get; init; }
  public string? ThreadId { get; init; }
}

public record PlotSolLlmCompletionResult(string Content, string ContentType, int StatusCode);
