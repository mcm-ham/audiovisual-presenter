using Microsoft.EntityFrameworkCore;
using Presenter.Core.Models;

namespace Presenter.Data;

/// <summary>
/// EF Core context over SQLite. Schema mirrors the legacy SQL Server Compact database
/// (App_Code/Model.edmx): Schedules, Items, Flags with cascade deletes.
/// </summary>
public class PresenterDbContext(DbContextOptions<PresenterDbContext> options) : DbContext(options)
{
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Schedule>(e =>
        {
            e.ToTable("Schedules");
            e.HasKey(s => s.ID);
            e.Property(s => s.ID).ValueGeneratedNever();
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();
            e.Property(s => s.Date).IsRequired();
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("Items");
            e.HasKey(i => i.ID);
            e.Property(i => i.ID).ValueGeneratedNever();
            e.Property(i => i.Filename).HasMaxLength(200).IsRequired();
            e.HasOne(i => i.Schedule)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.ScheduleID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Flag>(e =>
        {
            e.ToTable("Flags");
            e.HasKey(f => new { f.ItemID, f.Index });
            e.Property(f => f.Colour).HasMaxLength(30).IsRequired();
            e.HasOne(f => f.Item)
                .WithMany(i => i.Flags)
                .HasForeignKey(f => f.ItemID)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
