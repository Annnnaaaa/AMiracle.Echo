using AMiracle.Echo.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AMiracle.Echo.Storage.EFCore;

public sealed class EchoDbContext : DbContext
{
    public EchoDbContext(DbContextOptions<EchoDbContext> options) : base(options) { }

    internal DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    internal DbSet<FeedbackEntity> Feedbacks => Set<FeedbackEntity>();

    // SQLite cannot ORDER BY DateTimeOffset; store as long ticks to keep ordering portable across providers.
    private static readonly ValueConverter<DateTimeOffset, long> _dtoConverter =
        new(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
    private static readonly ValueConverter<DateTimeOffset?, long?> _dtoNullableConverter =
        new(v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProjectEntity>().Property(p => p.CreatedAt).HasConversion(_dtoConverter);
        b.Entity<ProjectEntity>().Property(p => p.ArchivedAt).HasConversion(_dtoNullableConverter);
        b.Entity<FeedbackEntity>().Property(f => f.CreatedAt).HasConversion(_dtoConverter);
        b.Entity<FeedbackEntity>().Property(f => f.DeletedAt).HasConversion(_dtoNullableConverter);

        b.Entity<ProjectEntity>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.PublicKey).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.PublicKey).IsUnique();
            e.Property(x => x.AllowedOriginsJson).IsRequired();
            e.Property(x => x.RetentionDays);
            e.Property(x => x.CreatedAt);
            e.Property(x => x.ArchivedAt);
        });

        b.Entity<FeedbackEntity>(e =>
        {
            e.ToTable("feedbacks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(16).IsRequired();
            e.Property(x => x.Text);
            e.Property(x => x.AudioBlobKey).HasMaxLength(512);
            e.Property(x => x.ScreenshotKey).HasMaxLength(512);
            e.Property(x => x.PageUrl).HasMaxLength(2048);
            e.Property(x => x.UserAgent).HasMaxLength(1024);
            e.Property(x => x.SubmitterJson);
            e.Property(x => x.SubmitterId).HasMaxLength(256);
            e.Property(x => x.CustomMetadataJson);
            e.Property(x => x.Category).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
            e.Property(x => x.Priority);
            e.Property(x => x.ConsentText).HasMaxLength(2000);
            e.Property(x => x.CreatedAt);
            e.Property(x => x.DeletedAt);
            e.HasIndex(x => new { x.ProjectId, x.CreatedAt });
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.HasIndex(x => x.SubmitterId);
        });
    }
}
