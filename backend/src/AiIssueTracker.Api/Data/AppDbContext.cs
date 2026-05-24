using AiIssueTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Label> Labels => Set<Label>();

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

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(50);
            b.HasIndex(x => x.Slug).IsUnique();

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(x => x.Description)
                .HasMaxLength(2000);

            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Issue>(b =>
        {
            b.ToTable("issues");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.HasOne<Project>()
                .WithMany(p => p.Issues)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Label>(b =>
        {
            b.ToTable("labels");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.HasOne<Project>()
                .WithMany(p => p.Labels)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
