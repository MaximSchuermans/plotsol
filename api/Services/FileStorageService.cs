using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Api.Services;

public sealed class FileStorageService
{
  private readonly BlobContainerClient _container;

  public FileStorageService(BlobContainerClient container)
  {
    _container = container;
  }

  public async Task<Uri> UploadAsync(Stream content, string blobName, string contentType, CancellationToken cancellationToken = default)
  {
    await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    var blobClient = _container.GetBlobClient(blobName);
    await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
    return blobClient.Uri;
  }
}
