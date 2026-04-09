using Api.Models;

namespace Api.Services;

public interface IUserRepository
{
  Task<User?> GetByIdAsync(string id);
  Task<User?> GetByUsernameAsync(string username);
  Task CreateAsync(User user);
}
