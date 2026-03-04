using Microsoft.EntityFrameworkCore;

using TelegramAggregator.Common.Data.Entities;

namespace TelegramAggregator.Common.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Channel> Channels { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Image> Images { get; set; }
    public DbSet<Summary> Summaries { get; set; }
    public DbSet<PostImage> PostImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Channels
        modelBuilder.Entity<Channel>(entity =>
        {
            entity.ToTable("channels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();
            entity.HasIndex(e => e.TelegramChannelId).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(256);
            entity.Property(e => e.Title).HasMaxLength(512);
        });

        // Posts
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("posts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();
            entity.HasIndex(e => e.ChannelId);
            entity.HasIndex(e => e.TelegramMessageId);
            entity.HasIndex(e => e.Fingerprint);
            entity.HasIndex(e => e.IngestedAt);
            entity.HasIndex(e => e.IsSummarized);
            entity.Property(e => e.Text).HasColumnType("text");
            entity.Property(e => e.RawJson).HasColumnType("jsonb");
            entity.HasOne(e => e.Channel).WithMany(c => c.Posts).HasForeignKey(e => e.ChannelId).OnDelete(DeleteBehavior.Cascade);
        });

        // Images
        modelBuilder.Entity<Image>(entity =>
        {
            entity.ToTable("images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChecksumSha256).IsUnique();
            entity.HasIndex(e => e.PerceptualHash);
            entity.HasIndex(e => e.UsedAt);
            entity.Property(e => e.Content).HasColumnType("bytea");
            entity.Property(e => e.ChecksumSha256).HasMaxLength(64);
            entity.Property(e => e.TelegramFileId).HasMaxLength(256);
        });

        // PostImages (junction table)
        modelBuilder.Entity<PostImage>(entity =>
        {
            entity.ToTable("post_images");
            entity.HasKey(e => new { e.PostId, e.ImageId });
            entity.HasOne(e => e.Post).WithMany(p => p.PostImages).HasForeignKey(e => e.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Image).WithMany(i => i.PostImages).HasForeignKey(e => e.ImageId).OnDelete(DeleteBehavior.Cascade);
        });

        // Summaries
        modelBuilder.Entity<Summary>(entity =>
        {
            entity.ToTable("summaries");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WindowStart);
            entity.HasIndex(e => e.PublishedAt);
            entity.Property(e => e.SummaryText).HasColumnType("text");
            entity.Property(e => e.IncludedPostIds).HasColumnType("jsonb");
        });
    }
}
