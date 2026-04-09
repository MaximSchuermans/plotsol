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
    return await _users.Find(u => u.Username.ToLower() == username.ToLower()).FirstOrDefaultAsync();
  }

  public Task CreateAsync(User user)
    => _users.InsertOneAsync(user);
}
