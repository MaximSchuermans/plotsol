using Api.Models;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Api.Services;

public sealed class FileRepository : IFileRepository
{
  private readonly IMongoCollection<FileMetadata> _files;

  public FileRepository(IMongoCollection<FileMetadata> files)
  {
    _files = files;
  }

  public Task CreateAsync(FileMetadata metadata)
    => _files.InsertOneAsync(metadata);

  public async Task<List<FileMetadata>> GetByUserAsync(string userId)
    => await _files.Find(f => f.UserId == userId)
                   .SortByDescending(f => f.UploadedAt)
                   .ToListAsync();
}
