using Api.Models;

namespace Api.Services;

public interface IFileRepository
{
  Task CreateAsync(FileMetadata metadata);
  Task<List<FileMetadata>> GetByUserAsync(string userId);
  Task<FileMetadata?> GetByIdAsync(string id);
  Task DeleteAsync(string id);
}
