namespace Bisp.Api.Models;

public sealed class ExternalGameId
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;

    public Game? Game { get; set; }
}
