using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AutoFlow.Infrastructure.Persistence;

public class AutoFlowDbContext : DbContext
{
    public AutoFlowDbContext(DbContextOptions<AutoFlowDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Automation> Automations => Set<Automation>();
    public DbSet<AutomationVersion> AutomationVersions => Set<AutomationVersion>();
    public DbSet<AutomationAsset> AutomationAssets => Set<AutomationAsset>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<RunStepLog> RunStepLogs => Set<RunStepLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enum <-> snake_case text converters that match the DB CHECK constraints.
        var versionStatus = new ValueConverter<VersionStatus, string>(
            v => SnakeCase.From(v.ToString()),
            s => Enum.Parse<VersionStatus>(SnakeCase.ToPascal(s)));
        var runStatus = new ValueConverter<RunStatus, string>(
            v => SnakeCase.From(v.ToString()),
            s => Enum.Parse<RunStatus>(SnakeCase.ToPascal(s)));

        b.Entity<User>(e => e.ToTable("users"));

        b.Entity<Automation>(e =>
        {
            e.ToTable("automations");
            e.HasMany(a => a.Versions)
                .WithOne(v => v.Automation!)
                .HasForeignKey(v => v.AutomationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.CurrentVersion)
                .WithMany()
                .HasForeignKey(a => a.CurrentVersionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<AutomationVersion>(e =>
        {
            e.ToTable("automation_versions");
            e.Property(v => v.Definition).HasColumnType("jsonb");
            e.Property(v => v.Status).HasConversion(versionStatus);
            e.HasMany(v => v.Assets)
                .WithOne()
                .HasForeignKey(a => a.AutomationVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AutomationAsset>(e => e.ToTable("automation_assets"));
        b.Entity<Agent>(e => e.ToTable("agents"));

        b.Entity<AutomationRun>(e =>
        {
            e.ToTable("automation_runs");
            e.Property(r => r.Status).HasConversion(runStatus);
            e.HasOne(r => r.AutomationVersion)
                .WithMany()
                .HasForeignKey(r => r.AutomationVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.StepLogs)
                .WithOne()
                .HasForeignKey(l => l.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RunStepLog>(e =>
        {
            e.ToTable("run_step_logs");
            e.Property(l => l.Status).HasConversion(runStatus);
        });
    }
}

/// <summary>Bridges PascalCase enum names and the snake_case tokens used in the DB.</summary>
internal static class SnakeCase
{
    public static string From(string pascal)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string ToPascal(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
