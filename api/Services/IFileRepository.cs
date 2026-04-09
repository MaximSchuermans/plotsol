using Api.Models;

namespace Api.Services;

public interface IFileRepository
{
  Task CreateAsync(FileMetadata metadata);
  Task<List<FileMetadata>> GetByUserAsync(string userId);
}
