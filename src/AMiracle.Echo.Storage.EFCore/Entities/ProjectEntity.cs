namespace AMiracle.Echo.Storage.EFCore.Entities;

internal sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string AllowedOriginsJson { get; set; } = "[]";
    public int? RetentionDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
