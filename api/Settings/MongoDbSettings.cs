namespace Api.Settings;

public sealed class MongoDbSettings
{
  public string ConnectionString { get; set; } = null!;
  public string DatabaseName { get; set; } = null!;
  public string UsersCollectionName { get; set; } = "users";
  public string FilesCollectionName { get; set; } = "files";
}
