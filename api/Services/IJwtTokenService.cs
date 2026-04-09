using Api.Models;

namespace Api.Services;

public interface IJwtTokenService
{
  string GenerateToken(User user);
}
