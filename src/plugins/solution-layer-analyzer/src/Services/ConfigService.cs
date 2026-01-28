using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for managing saved configurations (index and filter)
/// </summary>
public class ConfigService
{
    private readonly AnalyzerDbContext _dbContext;
    private readonly ILogger _logger;

    public ConfigService(AnalyzerDbContext dbContext, ILogger logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Generates a hash for index configuration to enable smart matching
    /// </summary>
    public static string GenerateIndexHash(string connectionId, List<string> sourceSolutions, List<string> targetSolutions, List<string> componentTypes)
    {
        // Sort lists for consistent hashing
        var sortedSource = string.Join("|", sourceSolutions.OrderBy(s => s));
        var sortedTarget = string.Join("|", targetSolutions.OrderBy(s => s));
        var sortedTypes = string.Join("|", componentTypes.OrderBy(s => s));
        
        var hashInput = $"{connectionId}:{sortedSource}:{sortedTarget}:{sortedTypes}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes)[..16]; // First 16 chars of hash
    }

    /// <summary>
    /// Saves an index configuration
    /// </summary>
    public async Task<SaveIndexConfigResponse> SaveIndexConfigAsync(SaveIndexConfigRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving index config: {Name}", request.Name);

        var configHash = GenerateIndexHash(
            request.ConnectionId,
            request.SourceSolutions,
            request.TargetSolutions,
            request.ComponentTypes
        );

        var config = new SavedIndexConfig
        {
            Name = request.Name,
            ConnectionId = request.ConnectionId,
            SourceSolutions = string.Join(",", request.SourceSolutions),
            TargetSolutions = string.Join(",", request.TargetSolutions),
            ComponentTypes = string.Join(",", request.ComponentTypes),
            PayloadMode = request.PayloadMode,
            ConfigHash = configHash,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SavedIndexConfigs.Add(config);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveIndexConfigResponse
        {
            ConfigId = config.Id,
            ConfigHash = config.ConfigHash
        };
    }

    /// <summary>
    /// Loads all index configurations, prioritizing by environment match
    /// </summary>
    public async Task<LoadIndexConfigsResponse> LoadIndexConfigsAsync(LoadIndexConfigsRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading index configs for connection: {ConnectionId}", request.ConnectionId ?? "any");

        var configs = await _dbContext.SavedIndexConfigs
            .OrderByDescending(c => c.LastUsedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = configs.Select(c => new IndexConfigItem
        {
            Id = c.Id,
            Name = c.Name,
            ConnectionId = c.ConnectionId,
            SourceSolutions = c.SourceSolutions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            TargetSolutions = c.TargetSolutions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            ComponentTypes = c.ComponentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            PayloadMode = c.PayloadMode,
            ConfigHash = c.ConfigHash,
            CreatedAt = c.CreatedAt,
            LastUsedAt = c.LastUsedAt,
            IsSameEnvironment = request.ConnectionId != null && c.ConnectionId == request.ConnectionId
        }).ToList();

        return new LoadIndexConfigsResponse
        {
            Configs = items
        };
    }

    /// <summary>
    /// Saves a filter configuration
    /// </summary>
    public async Task<SaveFilterConfigResponse> SaveFilterConfigAsync(SaveFilterConfigRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving filter config: {Name}", request.Name);

        var config = new SavedFilterConfig
        {
            Name = request.Name,
            ConnectionId = request.ConnectionId ?? string.Empty,
            OriginatingIndexHash = request.OriginatingIndexHash,
            FilterJson = request.Filter != null ? System.Text.Json.JsonSerializer.Serialize(request.Filter) : null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SavedFilterConfigs.Add(config);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveFilterConfigResponse
        {
            ConfigId = config.Id
        };
    }

    /// <summary>
    /// Loads all filter configurations, prioritizing by environment and index hash match
    /// </summary>
    public async Task<LoadFilterConfigsResponse> LoadFilterConfigsAsync(LoadFilterConfigsRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading filter configs for connection: {ConnectionId}, indexHash: {IndexHash}", 
            request.ConnectionId ?? "any", request.CurrentIndexHash ?? "none");

        var configs = await _dbContext.SavedFilterConfigs
            .OrderByDescending(c => c.LastUsedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = configs.Select(c => new FilterConfigItem
        {
            Id = c.Id,
            Name = c.Name,
            ConnectionId = c.ConnectionId,
            OriginatingIndexHash = c.OriginatingIndexHash,
            Filter = c.FilterJson != null 
                ? System.Text.Json.JsonSerializer.Deserialize<Filters.FilterNode>(c.FilterJson) 
                : null,
            CreatedAt = c.CreatedAt,
            LastUsedAt = c.LastUsedAt,
            MatchesCurrentIndex = request.CurrentIndexHash != null && c.OriginatingIndexHash == request.CurrentIndexHash,
            IsSameEnvironment = request.ConnectionId != null && c.ConnectionId == request.ConnectionId
        }).ToList();

        return new LoadFilterConfigsResponse
        {
            Configs = items
        };
    }

    /// <summary>
    /// Updates the last used timestamp for a configuration
    /// </summary>
    public async Task UpdateIndexConfigLastUsedAsync(int configId, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.SavedIndexConfigs.FindAsync(new object[] { configId }, cancellationToken);
        if (config != null)
        {
            config.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Updates the last used timestamp for a filter configuration
    /// </summary>
    public async Task UpdateFilterConfigLastUsedAsync(int configId, CancellationToken cancellationToken = default)
    {
        var config = await _dbContext.SavedFilterConfigs.FindAsync(new object[] { configId }, cancellationToken);
        if (config != null)
        {
            config.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
