namespace Api.Services;

public interface IIngestion
{
  Task IngestPdfAsync(
    Stream pdfStream,
    string fileId,
    string userId,
    string fileName,
    CancellationToken cancellationToken = default);

  Task DeleteChunksByFileIdAsync(string fileId, CancellationToken cancellationToken = default);
}
