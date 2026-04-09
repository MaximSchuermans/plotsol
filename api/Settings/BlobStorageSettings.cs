namespace Api.Settings;

public sealed class BlobStorageSettings
{
  public string ConnectionString { get; set; } = null!;
  public string ContainerName { get; set; } = "pdf-uploads";
}
