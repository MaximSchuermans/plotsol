using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Models;
using Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public sealed class JwtTokenService : IJwtTokenService
{
  private readonly JwtSettings _jwtSettings;

  public JwtTokenService(IOptions<JwtSettings> jwtOptions)
  {
    _jwtSettings = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
  }

  public string GenerateToken(User user)
  {
    var secret = _jwtSettings.Secret;
    var key = Encoding.UTF8.GetBytes(secret);
    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
      new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
      new Claim("username", user.Username),
    };
    var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
      issuer: _jwtSettings.Issuer,
      audience: _jwtSettings.Audience,
      claims: claims,
      expires: DateTime.UtcNow.AddHours(8),
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}
