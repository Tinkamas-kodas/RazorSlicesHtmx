using Microsoft.EntityFrameworkCore;
using razr_slices_htmx2.Features.Items.Models;

namespace razr_slices_htmx2.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(32);
            entity.Property(item => item.Name).HasMaxLength(128);
            entity.HasIndex(item => item.Code).IsUnique();
        });
    }
}