namespace Api.Settings;

public sealed class MongoDbSettings
{
  public string ConnectionString { get; set; } = null!;
  public string DatabaseName { get; set; } = null!;
  public string UsersCollectionName { get; set; } = "users";
  public string FilesCollectionName { get; set; } = "files";
  public string ChunksCollectionName { get; set; } = "file_chunks";
  public string VectorIndexName { get; set; } = "vector_index";
  public string ChatThreadsCollectionName { get; set; } = "chat_threads";
  public string ChatMessagesCollectionName { get; set; } = "chat_messages";
}
