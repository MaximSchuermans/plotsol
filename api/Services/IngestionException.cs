namespace Api.Services;

public sealed class IngestionException : Exception
{
  public int StatusCode { get; }

  public IngestionException(int statusCode, string message)
    : base(message)
  {
    StatusCode = statusCode;
  }
}
