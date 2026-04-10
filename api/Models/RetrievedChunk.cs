namespace Api.Models;

public sealed record RetrievedChunk(
  string ChunkId,
  string FileId,
  string FileName,
  int ChunkIndex,
  int PageNumber,
  string Text,
  double Score);
