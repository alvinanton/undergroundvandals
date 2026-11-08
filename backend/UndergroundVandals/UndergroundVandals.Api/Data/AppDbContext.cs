using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<WebsiteVisit> WebsiteVisits => Set<WebsiteVisit>();
    public DbSet<GalleryArticle> GalleryArticles => Set<GalleryArticle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<WebsiteVisit>()
            .HasIndex(v => v.CreatedAt);

        modelBuilder.Entity<GalleryArticle>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        modelBuilder.Entity<GalleryArticle>()
            .HasIndex(a => a.Category);

        modelBuilder.Entity<GalleryArticle>()
            .HasIndex(a => a.PublishedAt);
    }
}