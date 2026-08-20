using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MediaAsset> MediaAssets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();
    }
}