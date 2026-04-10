using Api.Models;

namespace Api.Services;

public interface IChatRepository
{
  Task<ChatThread> CreateThreadAsync(string userId, CancellationToken cancellationToken = default);
  Task<List<ChatThread>> GetThreadsByUserAsync(string userId, CancellationToken cancellationToken = default);
  Task<ChatThread?> GetThreadByIdAsync(string threadId, CancellationToken cancellationToken = default);
  Task DeleteThreadAsync(string threadId, string userId, CancellationToken cancellationToken = default);
  Task AddMessageAsync(PersistedChatMessage message, CancellationToken cancellationToken = default);
  Task<List<PersistedChatMessage>> GetMessagesByThreadAsync(string threadId, CancellationToken cancellationToken = default);
  Task UpdateThreadTitleAsync(string threadId, string title, CancellationToken cancellationToken = default);
}
