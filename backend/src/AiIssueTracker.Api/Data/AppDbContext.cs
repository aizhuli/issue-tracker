using AiIssueTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<IssueLabel> IssueLabels => Set<IssueLabel>();

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

            b.Property(x => x.NextIssueNumber).HasDefaultValue(1);

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

            b.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.Description)
                .HasMaxLength(10_000);

            b.Property(x => x.AcceptanceCriteria)
                .HasMaxLength(10_000);

            b.Property(x => x.Status)
                .HasConversion<string>();

            b.Property(x => x.Priority)
                .HasConversion<string>();

            // Unique issue number per project
            b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();

            // Covering index for per-project paginated list queries (ORDER BY created_at DESC, id DESC)
            b.HasIndex(x => new { x.ProjectId, x.Status, x.CreatedAt, x.Id })
                .IsDescending(false, false, true, true);

            b.HasOne(i => i.Project)
                .WithMany(p => p.Issues)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(i => i.Reporter)
                .WithMany()
                .HasForeignKey(i => i.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(i => i.Assignee)
                .WithMany()
                .HasForeignKey(i => i.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Label>(b =>
        {
            b.ToTable("labels");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(40);

            b.Property(x => x.Color)
                .IsRequired()
                .HasMaxLength(7);

            // Label name unique within a project (case-insensitive enforced at app layer)
            b.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();

            b.HasOne(l => l.Project)
                .WithMany(p => p.Labels)
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IssueLabel>(b =>
        {
            b.ToTable("issue_labels");

            b.HasKey(x => new { x.IssueId, x.LabelId });

            b.HasOne(il => il.Issue)
                .WithMany(i => i.IssueLabels)
                .HasForeignKey(il => il.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(il => il.Label)
                .WithMany(l => l.IssueLabels)
                .HasForeignKey(il => il.LabelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(b =>
        {
            b.ToTable("comments");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            b.Property(x => x.Body)
                .IsRequired()
                .HasMaxLength(10_000);

            b.HasOne(c => c.Issue)
                .WithMany(i => i.Comments)
                .HasForeignKey(c => c.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Stable ordering index for comments on an issue
            b.HasIndex(x => new { x.IssueId, x.CreatedAt, x.Id });
        });
    }
}
