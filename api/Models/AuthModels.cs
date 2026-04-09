namespace Api.Models;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string Token, string Username);
public sealed record UserProfile(string Id, string Username);
