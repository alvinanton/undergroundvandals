using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Entities;
using UndergroundVandals.Api.Models;

namespace UndergroundVandals.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<User> Users => Set<User>();
}