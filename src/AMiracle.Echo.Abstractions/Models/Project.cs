namespace AMiracle.Echo.Abstractions.Models;

public sealed class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public List<string> AllowedOrigins { get; set; } = new();
    public int? RetentionDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
