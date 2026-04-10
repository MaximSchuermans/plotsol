using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using Microsoft.AspNetCore.Http.Features;
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

var groqSection = builder.Configuration.GetSection("Groq");
builder.Services.Configure<GroqSettings>(groqSection);

var atlasEmbeddingSection = builder.Configuration.GetSection("AtlasEmbedding");
var atlasEmbeddingSettings = atlasEmbeddingSection.Get<AtlasEmbeddingSettings>()
  ?? throw new InvalidOperationException("Missing AtlasEmbedding configuration.");
builder.Services.Configure<AtlasEmbeddingSettings>(atlasEmbeddingSection);

EnsureSettings(mongoSettings, jwtSettings, blobSettings, atlasEmbeddingSettings);

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

builder.Services.AddScoped(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  var client = sp.GetRequiredService<IMongoClient>();
  var database = client.GetDatabase(settings.DatabaseName);
  return database.GetCollection<IngestedChunk>(settings.ChunksCollectionName);
});

builder.Services.AddScoped(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  var client = sp.GetRequiredService<IMongoClient>();
  var database = client.GetDatabase(settings.DatabaseName);
  return database.GetCollection<ChatThread>(settings.ChatThreadsCollectionName);
});

builder.Services.AddScoped(sp =>
{
  var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
  var client = sp.GetRequiredService<IMongoClient>();
  var database = client.GetDatabase(settings.DatabaseName);
  return database.GetCollection<PersistedChatMessage>(settings.ChatMessagesCollectionName);
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IAtlasEmbeddingClient, AtlasEmbeddingClient>();
builder.Services.AddScoped<IIngestion, Ingestion>();
builder.Services.AddScoped<IRetriever, Retriever>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPlotSolLlm, PlotSolLlm>();
builder.Services.AddHttpClient();

var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
      ValidateIssuer = false,
      ValidateAudience = false,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      ValidIssuer = jwtSettings.Issuer,
      ValidAudience = jwtSettings.Audience,
      IssuerSigningKey = new SymmetricSecurityKey(key),
      ClockSkew = TimeSpan.FromMinutes(5)
    };
  });

builder.Services.AddAuthorization();

builder.WebHost.ConfigureKestrel(options =>
{
  options.Limits.MaxRequestBodySize = 104857600; // 100MB
});

builder.Services.Configure<FormOptions>(options =>
{
  options.MultipartBodyLengthLimit = 104857600; // 100MB
});

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
  var userId = GetUserId(principal);
  var user = !string.IsNullOrEmpty(userId)
    ? await repository.GetByIdAsync(userId)
    : await GetUserByUsernameClaimAsync(principal, repository);
  if (user is null)
  {
    return Results.Unauthorized();
  }

  return Results.Ok(new UserProfile(user.Id!, user.Username));
});

app.MapGet("/auth/debug", [Authorize] (ClaimsPrincipal principal) =>
{
  var claims = principal.Claims
    .Select(c => new { c.Type, c.Value })
    .ToList();

  return Results.Ok(new
  {
    principal.Identity?.IsAuthenticated,
    principal.Identity?.AuthenticationType,
    principal.Identity?.Name,
    UserId = GetUserId(principal),
    Claims = claims,
  });
});

app.MapPost("/files/upload", [Authorize] async (
  HttpRequest request,
  ClaimsPrincipal principal,
  FileStorageService storageService,
  IUserRepository userRepository,
  IFileRepository fileRepository,
  IIngestion ingestion,
  CancellationToken cancellationToken) =>
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

  var userId = GetUserId(principal);
  var user = !string.IsNullOrEmpty(userId)
    ? await userRepository.GetByIdAsync(userId)
    : await GetUserByUsernameClaimAsync(principal, userRepository);
  if (user is null)
  {
    return Results.Unauthorized();
  }

  var sanitizedFileName = Path.GetFileName(file.FileName);
  var blobName = $"{user.Id}/{Guid.NewGuid():N}-{sanitizedFileName}";
  var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType;

  using var stream = file.OpenReadStream();
  var blobUri = await storageService.UploadAsync(stream, blobName, contentType, cancellationToken);

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

  try
  {
    await fileRepository.CreateAsync(metadata);

    if (string.IsNullOrWhiteSpace(metadata.Id))
    {
      throw new InvalidOperationException("File metadata id was not generated.");
    }

    using var ingestionStream = file.OpenReadStream();
    await ingestion.IngestPdfAsync(
      ingestionStream,
      metadata.Id,
      user.Id!,
      sanitizedFileName,
      cancellationToken);
  }
  catch (IngestionException ex)
  {
    if (!string.IsNullOrWhiteSpace(metadata.Id))
    {
      await fileRepository.DeleteAsync(metadata.Id);
    }

    await storageService.DeleteAsync(blobName, cancellationToken);

    return Results.Problem(
      detail: ex.Message,
      statusCode: ex.StatusCode,
      title: "Document ingestion failed");
  }
  catch (Exception ex)
  {
    if (!string.IsNullOrWhiteSpace(metadata.Id))
    {
      await fileRepository.DeleteAsync(metadata.Id);
    }

    await storageService.DeleteAsync(blobName, cancellationToken);

    return Results.Problem(
      detail: ex.Message,
      statusCode: StatusCodes.Status500InternalServerError,
      title: "Document ingestion failed");
  }

  return Results.Ok(new
  {
    metadata.Id,
    metadata.FileName,
    metadata.BlobUri,
    metadata.UploadedAt,
  });
});

app.MapGet("/files", [Authorize] async (ClaimsPrincipal principal, IFileRepository fileRepo, IUserRepository userRepository) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }

  if (string.IsNullOrWhiteSpace(userId))
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

app.MapDelete("/files/{id}", [Authorize] async (string id, ClaimsPrincipal principal, IFileRepository fileRepo, FileStorageService storageService, IUserRepository userRepository, IIngestion ingestion, CancellationToken cancellationToken) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }

  if (string.IsNullOrWhiteSpace(userId))
  {
    return Results.Unauthorized();
  }

  var file = await fileRepo.GetByIdAsync(id);
  if (file is null || file.UserId != userId)
  {
    return Results.NotFound();
  }

  await ingestion.DeleteChunksByFileIdAsync(file.Id!, cancellationToken);
  await storageService.DeleteAsync(file.BlobName, cancellationToken);
  await fileRepo.DeleteAsync(id);

  return Results.NoContent();
});

app.MapGet("/files/{id}/content", [Authorize] async (string id, ClaimsPrincipal principal, IFileRepository fileRepo, FileStorageService storageService, IUserRepository userRepository) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }

  if (string.IsNullOrWhiteSpace(userId))
  {
    return Results.Unauthorized();
  }

  var file = await fileRepo.GetByIdAsync(id);
  if (file is null || file.UserId != userId)
  {
    return Results.NotFound();
  }

  var stream = await storageService.OpenReadAsync(file.BlobName);
  return Results.File(stream, file.ContentType, file.FileName, enableRangeProcessing: true);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/chat/threads", [Authorize] async (ClaimsPrincipal principal, IUserRepository userRepository, IChatRepository chatRepository, CancellationToken cancellationToken) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }
  if (string.IsNullOrWhiteSpace(userId))
    return Results.Unauthorized();

  var threads = await chatRepository.GetThreadsByUserAsync(userId, cancellationToken);
  return Results.Ok(threads.Select(t => new { id = t.Id, title = t.Title, createdAt = t.CreatedAt, updatedAt = t.UpdatedAt }));
});

app.MapPost("/chat/threads", [Authorize] async (ClaimsPrincipal principal, IUserRepository userRepository, IChatRepository chatRepository, CancellationToken cancellationToken) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }
  if (string.IsNullOrWhiteSpace(userId))
    return Results.Unauthorized();

  var thread = await chatRepository.CreateThreadAsync(userId, cancellationToken);
  return Results.Created($"/chat/threads/{thread.Id}", new { id = thread.Id, title = thread.Title, createdAt = thread.CreatedAt, updatedAt = thread.UpdatedAt });
});

app.MapGet("/chat/threads/{threadId}/messages", [Authorize] async (string threadId, ClaimsPrincipal principal, IUserRepository userRepository, IChatRepository chatRepository, CancellationToken cancellationToken) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }
  if (string.IsNullOrWhiteSpace(userId))
    return Results.Unauthorized();

  var thread = await chatRepository.GetThreadByIdAsync(threadId, cancellationToken);
  if (thread is null || thread.UserId != userId)
    return Results.NotFound();

  var messages = await chatRepository.GetMessagesByThreadAsync(threadId, cancellationToken);
  return Results.Ok(messages.Select(m => new
  {
    id = m.Id,
    role = m.Role,
    content = m.Content,
    sources = string.IsNullOrWhiteSpace(m.SourcesJson) ? null : JsonNode.Parse(m.SourcesJson),
    fileId = m.FileId,
    createdAt = m.CreatedAt
  }));
});

app.MapDelete("/chat/threads/{threadId}", [Authorize] async (string threadId, ClaimsPrincipal principal, IUserRepository userRepository, IChatRepository chatRepository, CancellationToken cancellationToken) =>
{
  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }
  if (string.IsNullOrWhiteSpace(userId))
    return Results.Unauthorized();

  await chatRepository.DeleteThreadAsync(threadId, userId, cancellationToken);
  return Results.NoContent();
});

app.MapPost("/chat/completions", [Authorize] async (
  ChatCompletionRequest request,
  ClaimsPrincipal principal,
  IUserRepository userRepository,
  IRetriever retriever,
  IPlotSolLlm plotSolLlm,
  IChatRepository chatRepository,
  CancellationToken cancellationToken) =>
{
  if (request.Messages is null || request.Messages.Count == 0)
  {
    return Results.BadRequest(new { message = "At least one chat message is required." });
  }

  var hasInvalidMessage = request.Messages.Any(message =>
    string.IsNullOrWhiteSpace(message.Role)
    || string.IsNullOrWhiteSpace(message.Content)
    || (message.Role != "system" && message.Role != "assistant" && message.Role != "user"));
  if (hasInvalidMessage)
  {
    return Results.BadRequest(new { message = "Each message must include valid role and content." });
  }

  var userId = GetUserId(principal);
  if (string.IsNullOrWhiteSpace(userId))
  {
    var user = await GetUserByUsernameClaimAsync(principal, userRepository);
    userId = user?.Id;
  }

  if (string.IsNullOrWhiteSpace(userId))
  {
    return Results.Unauthorized();
  }

  string threadId = request.ThreadId ?? "";
  if (string.IsNullOrWhiteSpace(threadId))
  {
    var newThread = await chatRepository.CreateThreadAsync(userId, cancellationToken);
    threadId = newThread.Id!;
  }
  else
  {
    var existingThread = await chatRepository.GetThreadByIdAsync(threadId, cancellationToken);
    if (existingThread is null || existingThread.UserId != userId)
      return Results.NotFound();
  }

  var latestUserPrompt = request.Messages
    .LastOrDefault(m => m.Role == "user")?
    .Content
    .Trim();

  var topK = Math.Clamp(request.TopK ?? 8, 1, 20);
  var retrievedChunks = string.IsNullOrWhiteSpace(latestUserPrompt)
    ? []
    : await retriever.RetrieveAsync(
        latestUserPrompt,
        userId,
        topK,
        request.FileId,
        cancellationToken);

  var ragMessages = new List<ChatMessage>(request.Messages.Count + 1)
  {
    new("system", BuildRagSystemMessage(retrievedChunks))
  };
  ragMessages.AddRange(request.Messages);

  try
  {
    var completion = await plotSolLlm.CreateChatCompletionAsync(ragMessages, cancellationToken);
    if (completion.StatusCode is >= 200 and < 300
        && completion.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
    {
      var response = BuildChatCompletionResponse(completion.Content, retrievedChunks);
      if (response is not null)
      {
        var persisted = await PersistChatMessagesAsync(threadId, request.FileId, request.Messages, response, chatRepository, cancellationToken);
        response["persistedMessages"] = JsonSerializer.SerializeToNode(persisted);

        var firstUserMsg = request.Messages.FirstOrDefault(m => m.Role == "user");
        if (firstUserMsg is not null && !string.IsNullOrWhiteSpace(firstUserMsg.Content))
        {
          var title = firstUserMsg.Content.Length > 60
            ? firstUserMsg.Content[..60] + "..."
            : firstUserMsg.Content;
          await chatRepository.UpdateThreadTitleAsync(threadId, title, cancellationToken);
        }

        response["threadId"] = threadId;
        return Results.Json(response, statusCode: completion.StatusCode);
      }
    }

    return Results.Content(completion.Content, completion.ContentType, statusCode: completion.StatusCode);
  }
  catch (IngestionException ex)
  {
    return Results.Problem(detail: ex.Message, statusCode: ex.StatusCode, title: "RAG retrieval failed");
  }
  catch (InvalidOperationException ex)
  {
    return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
  }
});

static async Task<List<JsonObject>> PersistChatMessagesAsync(
  string threadId,
  string? fileId,
  IReadOnlyList<ChatMessage> messages,
  JsonObject response,
  IChatRepository chatRepository,
  CancellationToken cancellationToken)
{
  var persisted = new List<JsonObject>();
  var userMessages = messages.Where(m => m.Role == "user").ToList();
  var userTimestamp = DateTime.UtcNow;
  foreach (var msg in userMessages)
  {
    var doc = new PersistedChatMessage
    {
      ThreadId = threadId,
      Role = msg.Role,
      Content = msg.Content,
      FileId = fileId,
      CreatedAt = userTimestamp
    };
    await chatRepository.AddMessageAsync(doc, cancellationToken);
    persisted.Add(new JsonObject
    {
      ["id"] = doc.Id,
      ["role"] = doc.Role,
      ["content"] = doc.Content,
      ["sources"] = (JsonNode?)null,
      ["fileId"] = doc.FileId is null ? null : JsonValue.Create(doc.FileId),
      ["createdAt"] = doc.CreatedAt.ToString("o")
    });
  }

  var assistantContent = response["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
  if (!string.IsNullOrWhiteSpace(assistantContent))
  {
    var sourcesJson = response["sources"]?.ToJsonString();
    var assistantTimestamp = userTimestamp.AddMilliseconds(1);
    var doc = new PersistedChatMessage
    {
      ThreadId = threadId,
      Role = "assistant",
      Content = assistantContent,
      SourcesJson = sourcesJson,
      FileId = fileId,
      CreatedAt = assistantTimestamp
    };
    await chatRepository.AddMessageAsync(doc, cancellationToken);
    var sourcesNode = string.IsNullOrWhiteSpace(sourcesJson) ? null : JsonNode.Parse(sourcesJson);
    persisted.Add(new JsonObject
    {
      ["id"] = doc.Id,
      ["role"] = doc.Role,
      ["content"] = doc.Content,
      ["sources"] = sourcesNode,
      ["fileId"] = doc.FileId is null ? null : JsonValue.Create(doc.FileId),
      ["createdAt"] = doc.CreatedAt.ToString("o")
    });
  }

  return persisted;
}

app.MapFallbackToFile("index.html");

app.Run();

static void EnsureSettings(MongoDbSettings mongo, JwtSettings jwt, BlobStorageSettings blob, AtlasEmbeddingSettings atlasEmbedding)
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
  ThrowIfMissing(mongo.VectorIndexName, "MongoDb:VectorIndexName");
  ThrowIfMissing(jwt.Issuer, "Jwt:Issuer");
  ThrowIfMissing(jwt.Audience, "Jwt:Audience");
  ThrowIfMissing(jwt.Secret, "Jwt:Secret");
  ThrowIfMissing(blob.ConnectionString, "BlobStorage:ConnectionString");
  ThrowIfMissing(blob.ContainerName, "BlobStorage:ContainerName");
  ThrowIfMissing(atlasEmbedding.ApiKey, "AtlasEmbedding:ApiKey");
  ThrowIfMissing(atlasEmbedding.Endpoint, "AtlasEmbedding:Endpoint");
  ThrowIfMissing(atlasEmbedding.Model, "AtlasEmbedding:Model");
}

static string? GetUserId(ClaimsPrincipal principal)
{
  return principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}

static async Task<User?> GetUserByUsernameClaimAsync(ClaimsPrincipal principal, IUserRepository repository)
{
  var username = principal.FindFirstValue("username")
    ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
    ?? principal.Identity?.Name;

  if (string.IsNullOrWhiteSpace(username))
  {
    return null;
  }

  return await repository.GetByUsernameAsync(username);
}

static string BuildRagSystemMessage(IReadOnlyList<RetrievedChunk> chunks)
{
  const string instructions =
    "You are a retrieval-grounded assistant. Prefer the retrieved context when answering document questions. "
    + "If the context is insufficient, say what is missing instead of inventing details. "
    + "IMPORTANT: Cite sources EXACTLY as [source N] where N is the source number (e.g. [source 1], [source 2]). "
    + "Never use parentheses like (source 1) or plain text like 'source 1' for citations — only square brackets are recognized. "
    + "Do NOT mention page numbers in citations — page information shown in source buttons may not be accurate. "
    + "Do NOT use markdown tables. Use bullet lists or prose instead, especially when mathematical notation is involved.";

  if (chunks.Count == 0)
  {
    return instructions + "\n\nNo relevant retrieval context was found for this query.";
  }

  var contextItems = chunks.Select((chunk, index) =>
  {
    var normalizedText = chunk.Text.Replace("\r\n", "\n").Trim();
    return $"[source {index + 1}] file=\"{chunk.FileName}\"\n{normalizedText}";
  });

  return instructions + "\n\nRetrieved context:\n\n" + string.Join("\n\n---\n\n", contextItems);
}

static JsonObject? BuildChatCompletionResponse(string rawCompletionJson, IReadOnlyList<RetrievedChunk> chunks)
{
  JsonNode? parsed;
  try
  {
    parsed = JsonNode.Parse(rawCompletionJson);
  }
  catch (JsonException)
  {
    return null;
  }

  if (parsed is not JsonObject payload)
  {
    return null;
  }

  payload["sources"] = JsonSerializer.SerializeToNode(
    chunks.Select((chunk, index) => new
    {
      index = index + 1,
      chunk.FileId,
      chunk.FileName,
      chunk.PageNumber,
      chunk.ChunkIndex,
      chunk.Score
    }).ToList());

  return payload;
}
