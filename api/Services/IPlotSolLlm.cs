using Api.Models;

namespace Api.Services;

public interface IPlotSolLlm
{
  Task<PlotSolLlmCompletionResult> CreateChatCompletionAsync(
    IReadOnlyCollection<ChatMessage> messages,
    CancellationToken cancellationToken = default);
}
