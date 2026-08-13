using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
}