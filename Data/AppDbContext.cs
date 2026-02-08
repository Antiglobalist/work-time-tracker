using System.IO;
using Microsoft.EntityFrameworkCore;
using WorkTimeTracking.Models;

namespace WorkTimeTracking.Data;

public class AppDbContext : DbContext
{
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<TaskSession> TaskSessions { get; set; } = null!;
    public DbSet<GitBranchSession> GitBranchSessions { get; set; } = null!;

    private readonly string _dbPath;

    public AppDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "WorkTimeTracking");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "worktimetracking.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Reason).HasConversion<string>();
        });

        modelBuilder.Entity<TaskSession>(entity =>
        {
            entity.HasIndex(e => e.StartTime);
        });

        modelBuilder.Entity<GitBranchSession>(entity =>
        {
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.BranchName);
            entity.HasIndex(e => e.RepoPath);
        });
    }
}
