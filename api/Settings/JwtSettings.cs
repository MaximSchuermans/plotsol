namespace Api.Settings;

public sealed class JwtSettings
{
  public string Issuer { get; set; } = null!;
  public string Audience { get; set; } = null!;
  public string Secret { get; set; } = null!;
}
