using Microsoft.EntityFrameworkCore;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Data;

/// <summary>
/// DbContext for the solution layer analysis database.
/// Data is persisted to a SQLite file in the local application data folder.
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
    /// Gets or sets the IndexOperations table.
    /// </summary>
    public DbSet<IndexOperation> IndexOperations => Set<IndexOperation>();

    /// <summary>
    /// Gets or sets the SavedIndexConfigs table.
    /// </summary>
    public DbSet<SavedIndexConfig> SavedIndexConfigs => Set<SavedIndexConfig>();

    /// <summary>
    /// Gets or sets the SavedFilterConfigs table.
    /// </summary>
    public DbSet<SavedFilterConfig> SavedFilterConfigs => Set<SavedFilterConfig>();

    /// <summary>
    /// Gets or sets the ComponentNameCache table.
    /// </summary>
    public DbSet<ComponentNameCache> ComponentNameCache => Set<ComponentNameCache>();

    /// <summary>
    /// Gets or sets the LayerAttributes table.
    /// </summary>
    public DbSet<LayerAttribute> LayerAttributes => Set<LayerAttribute>();

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
            // Use file-based SQLite database in local app data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbDirectory = Path.Combine(appDataPath, "DataverseDevKit", "SolutionLayerAnalyzer");
            Directory.CreateDirectory(dbDirectory);
            var dbPath = Path.Combine(dbDirectory, "analyzer.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
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
            entity.HasMany(e => e.Attributes)
                  .WithOne(e => e.Layer)
                  .HasForeignKey(e => e.LayerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure LayerAttribute entity
        modelBuilder.Entity<LayerAttribute>(entity =>
        {
            entity.HasKey(e => e.AttributeId);
            entity.HasIndex(e => e.LayerId);
            entity.HasIndex(e => e.AttributeName);
            entity.HasIndex(e => new { e.LayerId, e.AttributeName });
            entity.HasIndex(e => e.AttributeType);
        });

        // Configure Artifact entity
        modelBuilder.Entity<Artifact>(entity =>
        {
            entity.HasKey(e => e.ArtifactId);
            entity.HasIndex(e => new { e.ComponentId, e.SolutionId });
        });

        // Configure IndexOperation entity
        modelBuilder.Entity<IndexOperation>(entity =>
        {
            entity.HasKey(e => e.OperationId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
        });

        // Configure SavedIndexConfig entity
        modelBuilder.Entity<SavedIndexConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.ConfigHash);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure SavedFilterConfig entity
        modelBuilder.Entity<SavedFilterConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.OriginatingIndexHash);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure ComponentNameCache entity
        modelBuilder.Entity<ComponentNameCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ObjectId, e.ComponentTypeCode }).IsUnique();
            entity.HasIndex(e => e.LogicalName);
            entity.HasIndex(e => e.TableLogicalName);
        });
    }
}
