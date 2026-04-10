using Api.Models;
using MongoDB.Driver;

namespace Api.Services;

public sealed class UserRepository : IUserRepository
{
  private readonly IMongoCollection<User> _users;

  public UserRepository(IMongoCollection<User> users)
  {
    _users = users;
  }

  public async Task<User?> GetByIdAsync(string id)
  {
    return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
  }

  public async Task<User?> GetByUsernameAsync(string username)
  {
    if (string.IsNullOrWhiteSpace(username))
    {
      return null;
    }

    var normalized = username.Trim().ToLowerInvariant();
    return await _users.Find(u => u.NormalizedUsername == normalized).FirstOrDefaultAsync();
  }

  public Task CreateAsync(User user)
    => _users.InsertOneAsync(user);
}
