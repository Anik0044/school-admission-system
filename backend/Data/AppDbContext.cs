using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // These DbSets represent the tables in our PostgreSQL database
    public DbSet<Class> Classes { get; set; }
    public DbSet<Hold> Holds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set primary keys for our entities
        modelBuilder.Entity<Class>().HasKey(c => c.Id);
        modelBuilder.Entity<Hold>().HasKey(h => h.Id);
    }
}