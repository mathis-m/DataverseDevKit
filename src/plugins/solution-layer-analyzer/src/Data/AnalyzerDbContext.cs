using Microsoft.EntityFrameworkCore;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Data;

/// <summary>
/// DbContext for the in-memory solution layer analysis database.
/// </summary>
public sealed class AnalyzerDbContext : DbContext
{
    /// <summary>
    /// Gets or sets the Solutions table.
    /// </summary>
    public DbSet<Solution> Solutions => Set<Solution>();

    /// <summary>
    /// Gets or sets the Components table.
    /// </summary>
    public DbSet<Component> Components => Set<Component>();

    /// <summary>
    /// Gets or sets the Layers table.
    /// </summary>
    public DbSet<Layer> Layers => Set<Layer>();

    /// <summary>
    /// Gets or sets the Artifacts table.
    /// </summary>
    public DbSet<Artifact> Artifacts => Set<Artifact>();

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzerDbContext"/> class.
    /// </summary>
    public AnalyzerDbContext()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzerDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public AnalyzerDbContext(DbContextOptions<AnalyzerDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Use in-memory SQLite database
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Solution entity
        modelBuilder.Entity<Solution>(entity =>
        {
            entity.HasKey(e => e.SolutionId);
            entity.HasIndex(e => e.UniqueName);
            entity.HasIndex(e => new { e.Publisher, e.IsManaged });
        });

        // Configure Component entity
        modelBuilder.Entity<Component>(entity =>
        {
            entity.HasKey(e => e.ComponentId);
            entity.HasIndex(e => e.ComponentType);
            entity.HasIndex(e => e.LogicalName);
            entity.HasIndex(e => e.TableLogicalName);
            entity.HasMany(e => e.Layers)
                  .WithOne(e => e.Component)
                  .HasForeignKey(e => e.ComponentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Layer entity
        modelBuilder.Entity<Layer>(entity =>
        {
            entity.HasKey(e => e.LayerId);
            entity.HasIndex(e => new { e.ComponentId, e.Ordinal });
            entity.HasIndex(e => e.SolutionId);
        });

        // Configure Artifact entity
        modelBuilder.Entity<Artifact>(entity =>
        {
            entity.HasKey(e => e.ArtifactId);
            entity.HasIndex(e => new { e.ComponentId, e.SolutionId });
        });
    }
}
