using System.Linq;
using DotNetEnv;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

var envFile = Path.Combine(Directory.GetCurrentDirectory(), "api/.env");
if (File.Exists(envFile))
{
  Env.Load(envFile);
}

var connectionString = GetEnv("MongoDb__ConnectionString");
var databaseName = GetEnv("MongoDb__DatabaseName");
var usersCollection = Environment.GetEnvironmentVariable("MongoDb__UsersCollectionName") ?? "users";

var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
var username = GetArgValue(cliArgs, new[] { "--username", "-u" }) ?? throw new InvalidOperationException("--username is required.");
var password = GetArgValue(cliArgs, new[] { "--password", "-p" }) ?? PromptPassword();

var client = new MongoClient(connectionString);
var collection = client.GetDatabase(databaseName).GetCollection<UserDocument>(usersCollection);
var normalized = username.Trim().ToLowerInvariant();

var existing = await collection.Find(u => u.NormalizedUsername == normalized).FirstOrDefaultAsync();
var hashed = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

var userDocument = new UserDocument
{
  Id = existing?.Id ?? ObjectId.GenerateNewId().ToString(),
  Username = username.Trim(),
  NormalizedUsername = normalized,
  PasswordHash = hashed,
  CreatedAt = DateTime.UtcNow,
  UpdatedAt = DateTime.UtcNow,
};

if (existing is null)
{
  await collection.InsertOneAsync(userDocument);
  Console.WriteLine($"Inserted user '{username}'.");
}
else
{
  await collection.ReplaceOneAsync(u => u.Id == existing.Id, userDocument);
  Console.WriteLine($"Updated password for '{username}'.");
}

static string GetEnv(string key)
  => Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException($"Environment variable '{key}' is missing.");

static string? GetArgValue(string[] args, string[] optionNames)
{
  for (var i = 0; i < args.Length; i++)
  {
    if (optionNames.Contains(args[i], StringComparer.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
      return args[i + 1];
    }
  }

  return null;
}

static string PromptPassword()
{
  Console.Write("Password: ");
  var password = string.Empty;
  while (true)
  {
    var key = Console.ReadKey(intercept: true);
    if (key.Key == ConsoleKey.Enter)
    {
      Console.WriteLine();
      break;
    }

    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
    {
      password = password[..^1];
      Console.Write("\b \b");
      continue;
    }

    password += key.KeyChar;
    Console.Write("*");
  }

  return password;
}

internal sealed class UserDocument
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string Id { get; init; } = null!;

  [BsonElement("username")]
  public string Username { get; init; } = null!;

  [BsonElement("normalizedUsername")]
  public string NormalizedUsername { get; init; } = null!;

  [BsonElement("passwordHash")]
  public string PasswordHash { get; init; } = null!;

  [BsonElement("createdAt")]
  public DateTime CreatedAt { get; init; }

  [BsonElement("updatedAt")]
  public DateTime UpdatedAt { get; init; }
}
