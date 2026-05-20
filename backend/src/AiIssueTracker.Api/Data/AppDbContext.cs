using AiIssueTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(254);
            b.HasIndex(x => x.Email).IsUnique();

            b.Property(x => x.PasswordHash).IsRequired();

            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Avatar).HasMaxLength(2048);
            b.Property(x => x.CreatedAt).IsRequired();
        });
    }
}
