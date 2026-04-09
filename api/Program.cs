using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Api.Models;
using Api.Services;
using Api.Settings;
using Azure.Storage.Blobs;
using BCrypt.Net;
using DotNetEnv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
  Env.Load(envFilePath);
}

var builder = WebApplication.CreateBuilder(args);

var mongoSection = builder.Configuration.GetSection("MongoDb");
var mongoSettings = mongoSection.Get<MongoDbSettings>()
  ?? throw new InvalidOperationException("Missing MongoDb configuration.");
builder.Services.Configure<MongoDbSettings>(mongoSection);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSettings = jwtSection.Get<JwtSettings>()
  ?? throw new InvalidOperationException("Missing JWT configuration.");
builder.Services.Configure<JwtSettings>(jwtSection);

var blobSection = builder.Configuration.GetSection("BlobStorage");
var blobSettings = blobSection.Get<BlobStorageSettings>()
  ?? throw new InvalidOperationException("Missing BlobStorage configuration.");
builder.Services.Configure<BlobStorageSettings>(blobSection);

EnsureSettings(mongoSettings, jwtSettings, blobSettings);

builder.Services.AddSingleton(sp => new BlobServiceClient(blobSettings.ConnectionString));
builder.Services.AddSingleton(sp =>
{
  var client = sp.GetRequiredService<BlobServiceClient>();
  return client.GetBlobContainerClient(blobSettings.ContainerName);
});
builder.Services.AddScoped<FileStorageService>();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  return new MongoClient(settings.ConnectionString);
});

builder.Services.AddScoped(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  var client = sp.GetRequiredService<IMongoClient>();
  var database = client.GetDatabase(settings.DatabaseName);
  return database.GetCollection<User>(settings.UsersCollectionName);
});

builder.Services.AddScoped(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  var client = sp.GetRequiredService<IMongoClient>();
  var database = client.GetDatabase(settings.DatabaseName);
  return database.GetCollection<FileMetadata>(settings.FilesCollectionName);
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidateAudience = true,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      ValidIssuer = jwtSettings.Issuer,
      ValidAudience = jwtSettings.Audience,
      IssuerSigningKey = new SymmetricSecurityKey(key),
      ClockSkew = TimeSpan.FromMinutes(5)
    };
  });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/login", async (LoginRequest request, IUserRepository repository, IJwtTokenService tokenService) =>
{
  if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
  {
    return Results.BadRequest(new { message = "Username and password are required." });
  }

  var user = await repository.GetByUsernameAsync(request.Username);
  if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
  {
    return Results.Unauthorized();
  }

  var token = tokenService.GenerateToken(user!);
  return Results.Ok(new LoginResponse(token, user.Username));
});

app.MapGet("/auth/me", [Authorize] async (ClaimsPrincipal principal, IUserRepository repository) =>
{
  var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
  if (string.IsNullOrEmpty(userId))
  {
    return Results.Unauthorized();
  }

  var user = await repository.GetByIdAsync(userId);
  if (user is null)
  {
    return Results.NotFound();
  }

  return Results.Ok(new UserProfile(user.Id!, user.Username));
});

app.MapPost("/files/upload", [Authorize] async (
  HttpRequest request,
  ClaimsPrincipal principal,
  FileStorageService storageService,
  IUserRepository userRepository,
  IFileRepository fileRepository) =>
{
  if (!request.HasFormContentType)
  {
    return Results.BadRequest(new { message = "Multipart form data is required." });
  }

  var form = await request.ReadFormAsync();
  var file = form.Files.FirstOrDefault();
  if (file is null)
  {
    return Results.BadRequest(new { message = "Attach a PDF file to upload." });
  }

  var isPdf = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
    || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
  if (!isPdf)
  {
    return Results.BadRequest(new { message = "Only PDF uploads are supported." });
  }

  var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
  if (string.IsNullOrEmpty(userId))
  {
    return Results.Unauthorized();
  }

  var user = await userRepository.GetByIdAsync(userId);
  if (user is null)
  {
    return Results.Unauthorized();
  }

  var sanitizedFileName = Path.GetFileName(file.FileName);
  var blobName = $"{user.Id}/{Guid.NewGuid():N}-{sanitizedFileName}";
  var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType;

  using var stream = file.OpenReadStream();
  var blobUri = await storageService.UploadAsync(stream, blobName, contentType);

  var metadata = new FileMetadata
  {
    UserId = user.Id!,
    Username = user.Username,
    FileName = sanitizedFileName,
    BlobName = blobName,
    BlobUri = blobUri.ToString(),
    ContentType = contentType,
    Size = file.Length,
    UploadedAt = DateTime.UtcNow,
  };

  await fileRepository.CreateAsync(metadata);

  return Results.Ok(new
  {
    metadata.Id,
    metadata.FileName,
    metadata.BlobUri,
    metadata.UploadedAt,
  });
});

app.MapGet("/files", [Authorize] async (ClaimsPrincipal principal, IFileRepository fileRepo) =>
{
  var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
  if (string.IsNullOrEmpty(userId))
  {
    return Results.Unauthorized();
  }

  var files = await fileRepo.GetByUserAsync(userId);
  return Results.Ok(files.Select(f => new
  {
    f.Id,
    f.FileName,
    f.BlobUri,
    f.UploadedAt
  }));
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapFallbackToFile("index.html");

app.Run();

static void EnsureSettings(MongoDbSettings mongo, JwtSettings jwt, BlobStorageSettings blob)
{
  static void ThrowIfMissing(string value, string path)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new InvalidOperationException($"Configuration value '{path}' must be provided.");
    }
  }

  ThrowIfMissing(mongo.ConnectionString, "MongoDb:ConnectionString");
  ThrowIfMissing(mongo.DatabaseName, "MongoDb:DatabaseName");
  ThrowIfMissing(jwt.Issuer, "Jwt:Issuer");
  ThrowIfMissing(jwt.Audience, "Jwt:Audience");
  ThrowIfMissing(jwt.Secret, "Jwt:Secret");
  ThrowIfMissing(blob.ConnectionString, "BlobStorage:ConnectionString");
  ThrowIfMissing(blob.ContainerName, "BlobStorage:ContainerName");
}
