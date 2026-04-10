using Api.Models;
using MongoDB.Driver;

namespace Api.Services;

public sealed class ChatRepository : IChatRepository
{
  private readonly IMongoCollection<ChatThread> _threads;
  private readonly IMongoCollection<PersistedChatMessage> _messages;

  public ChatRepository(
    IMongoCollection<ChatThread> threads,
    IMongoCollection<PersistedChatMessage> messages)
  {
    _threads = threads;
    _messages = messages;
  }

  public async Task<ChatThread> CreateThreadAsync(string userId, CancellationToken cancellationToken = default)
  {
    var thread = new ChatThread
    {
      UserId = userId,
      Title = "New chat",
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };
    await _threads.InsertOneAsync(thread, cancellationToken: cancellationToken);
    return thread;
  }

  public async Task<List<ChatThread>> GetThreadsByUserAsync(string userId, CancellationToken cancellationToken = default)
    => await _threads
      .Find(t => t.UserId == userId)
      .SortByDescending(t => t.UpdatedAt)
      .Limit(5)
      .ToListAsync(cancellationToken);

  public async Task<ChatThread?> GetThreadByIdAsync(string threadId, CancellationToken cancellationToken = default)
    => await _threads.Find(t => t.Id == threadId).FirstOrDefaultAsync(cancellationToken);

  public async Task DeleteThreadAsync(string threadId, string userId, CancellationToken cancellationToken = default)
  {
    await _threads.DeleteOneAsync(t => t.Id == threadId && t.UserId == userId, cancellationToken);
    await _messages.DeleteManyAsync(m => m.ThreadId == threadId, cancellationToken);
  }

  public async Task AddMessageAsync(PersistedChatMessage message, CancellationToken cancellationToken = default)
    => await _messages.InsertOneAsync(message, cancellationToken: cancellationToken);

  public async Task<List<PersistedChatMessage>> GetMessagesByThreadAsync(string threadId, CancellationToken cancellationToken = default)
    => await _messages
      .Find(m => m.ThreadId == threadId)
      .SortBy(m => m.CreatedAt)
      .ToListAsync(cancellationToken);

  public async Task UpdateThreadTitleAsync(string threadId, string title, CancellationToken cancellationToken = default)
  {
    var update = Builders<ChatThread>.Update
      .Set(t => t.Title, title)
      .Set(t => t.UpdatedAt, DateTime.UtcNow);
    await _threads.UpdateOneAsync(t => t.Id == threadId, update, cancellationToken: cancellationToken);
  }
}
