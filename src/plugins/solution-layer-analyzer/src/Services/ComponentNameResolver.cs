using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Resolved component name information.
/// </summary>
public sealed record ResolvedComponentName(
    string LogicalName,
    string? DisplayName,
    string? TableLogicalName);

/// <summary>
/// Service for resolving component display names and logical names.
/// Uses a database cache to avoid repeated API calls.
/// </summary>
public sealed class ComponentNameResolver
{
    private readonly DbContextOptions<AnalyzerDbContext> _dbContextOptions;
    private readonly ILogger _logger;
    private readonly ServiceClient _serviceClient;

    // In-memory cache for current session to reduce DB lookups
    private readonly Dictionary<(Guid ObjectId, int TypeCode), ResolvedComponentName> _sessionCache = new();

    public ComponentNameResolver(
        DbContextOptions<AnalyzerDbContext> dbContextOptions,
        ILogger logger,
        ServiceClient serviceClient)
    {
        _dbContextOptions = dbContextOptions;
        _logger = logger;
        _serviceClient = serviceClient;
    }

    /// <summary>
    /// Resolves the display name and logical name for a component.
    /// </summary>
    /// <param name="objectId">The object ID of the component.</param>
    /// <param name="componentTypeCode">The component type code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved component name information.</returns>
    public async Task<ResolvedComponentName> ResolveAsync(
        Guid objectId,
        int componentTypeCode,
        CancellationToken cancellationToken)
    {
        // Check session cache first
        var cacheKey = (objectId, componentTypeCode);
        if (_sessionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Check database cache
        await using var dbContext = new AnalyzerDbContext(_dbContextOptions);
        var dbCached = await dbContext.ComponentNameCache
            .FirstOrDefaultAsync(c => c.ObjectId == objectId && c.ComponentTypeCode == componentTypeCode, cancellationToken);

        if (dbCached != null)
        {
            var result = new ResolvedComponentName(dbCached.LogicalName, dbCached.DisplayName, dbCached.TableLogicalName);
            _sessionCache[cacheKey] = result;
            return result;
        }

        // Resolve from Dataverse
        var resolved = await ResolveFromDataverseAsync(objectId, componentTypeCode, cancellationToken);

        // Cache in database
        var cacheEntry = new ComponentNameCache
        {
            Id = Guid.NewGuid(),
            ObjectId = objectId,
            ComponentTypeCode = componentTypeCode,
            LogicalName = resolved.LogicalName,
            DisplayName = resolved.DisplayName,
            TableLogicalName = resolved.TableLogicalName,
            CachedAt = DateTimeOffset.UtcNow
        };

        dbContext.ComponentNameCache.Add(cacheEntry);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Cache in session
        _sessionCache[cacheKey] = resolved;

        return resolved;
    }

    /// <summary>
    /// Resolves multiple components in a batch for efficiency.
    /// </summary>
    public async Task<Dictionary<Guid, ResolvedComponentName>> ResolveBatchAsync(
        IEnumerable<(Guid ObjectId, int TypeCode)> components,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<Guid, ResolvedComponentName>();
        var toResolve = new List<(Guid ObjectId, int TypeCode)>();

        await using var dbContext = new AnalyzerDbContext(_dbContextOptions);

        // Check session cache and database cache
        foreach (var (objectId, typeCode) in components)
        {
            var cacheKey = (objectId, typeCode);
            if (_sessionCache.TryGetValue(cacheKey, out var cached))
            {
                results[objectId] = cached;
                continue;
            }

            var dbCached = await dbContext.ComponentNameCache
                .FirstOrDefaultAsync(c => c.ObjectId == objectId && c.ComponentTypeCode == typeCode, cancellationToken);

            if (dbCached != null)
            {
                var result = new ResolvedComponentName(dbCached.LogicalName, dbCached.DisplayName, dbCached.TableLogicalName);
                _sessionCache[cacheKey] = result;
                results[objectId] = result;
            }
            else
            {
                toResolve.Add((objectId, typeCode));
            }
        }

        // Resolve remaining from Dataverse
        foreach (var (objectId, typeCode) in toResolve)
        {
            try
            {
                var resolved = await ResolveFromDataverseAsync(objectId, typeCode, cancellationToken);
                results[objectId] = resolved;

                // Cache in database
                var cacheEntry = new ComponentNameCache
                {
                    Id = Guid.NewGuid(),
                    ObjectId = objectId,
                    ComponentTypeCode = typeCode,
                    LogicalName = resolved.LogicalName,
                    DisplayName = resolved.DisplayName,
                    TableLogicalName = resolved.TableLogicalName,
                    CachedAt = DateTimeOffset.UtcNow
                };

                dbContext.ComponentNameCache.Add(cacheEntry);
                _sessionCache[(objectId, typeCode)] = resolved;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve component {ObjectId} of type {TypeCode}", objectId, typeCode);
                // Fall back to ID-based name
                var fallback = new ResolvedComponentName(objectId.ToString(), null, null);
                results[objectId] = fallback;
                _sessionCache[(objectId, typeCode)] = fallback;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    private async Task<ResolvedComponentName> ResolveFromDataverseAsync(
        Guid objectId,
        int componentTypeCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return componentTypeCode switch
            {
                ComponentTypeCodes.Entity => await ResolveEntityAsync(objectId, cancellationToken),
                ComponentTypeCodes.Attribute => await ResolveAttributeAsync(objectId, cancellationToken),
                ComponentTypeCodes.OptionSet => await ResolveOptionSetAsync(objectId, cancellationToken),
                ComponentTypeCodes.Form or ComponentTypeCodes.SystemForm => await ResolveFormAsync(objectId, cancellationToken),
                ComponentTypeCodes.SavedQuery => await ResolveViewAsync(objectId, cancellationToken),
                ComponentTypeCodes.SavedQueryVisualization => await ResolveChartAsync(objectId, cancellationToken),
                ComponentTypeCodes.WebResource => await ResolveWebResourceAsync(objectId, cancellationToken),
                ComponentTypeCodes.Workflow => await ResolveWorkflowAsync(objectId, cancellationToken),
                ComponentTypeCodes.AppModule => await ResolveAppModuleAsync(objectId, cancellationToken),
                ComponentTypeCodes.SDKMessageProcessingStep => await ResolvePluginStepAsync(objectId, cancellationToken),
                ComponentTypeCodes.Role => await ResolveRoleAsync(objectId, cancellationToken),
                ComponentTypeCodes.SiteMap => await ResolveSiteMapAsync(objectId, cancellationToken),
                ComponentTypeCodes.CustomAPI => await ResolveCustomApiAsync(objectId, cancellationToken),
                ComponentTypeCodes.ConnectionRole => await ResolveConnectionRoleAsync(objectId, cancellationToken),
                ComponentTypeCodes.EmailTemplate => await ResolveEmailTemplateAsync(objectId, cancellationToken),
                ComponentTypeCodes.Report => await ResolveReportAsync(objectId, cancellationToken),
                ComponentTypeCodes.DuplicateRule => await ResolveDuplicateRuleAsync(objectId, cancellationToken),
                ComponentTypeCodes.SLA => await ResolveSlaAsync(objectId, cancellationToken),
                ComponentTypeCodes.RoutingRule => await ResolveRoutingRuleAsync(objectId, cancellationToken),
                ComponentTypeCodes.CustomControl => await ResolveCustomControlAsync(objectId, cancellationToken),
                ComponentTypeCodes.Relationship or ComponentTypeCodes.EntityRelationship => await ResolveRelationshipAsync(objectId, cancellationToken),
                _ => FallbackToId(objectId, componentTypeCode)
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve component {ObjectId} of type {TypeCode}, using fallback", objectId, componentTypeCode);
            return FallbackToId(objectId, componentTypeCode);
        }
    }

    private static ResolvedComponentName FallbackToId(Guid objectId, int componentTypeCode)
    {
        var typeName = GetTypeName(componentTypeCode);
        return new ResolvedComponentName(
            objectId.ToString(),
            $"{typeName} ({objectId:N})",
            null);
    }

    private static string GetTypeName(int componentTypeCode)
    {
        return componentTypeCode switch
        {
            ComponentTypeCodes.Entity => "Entity",
            ComponentTypeCodes.Attribute => "Attribute",
            ComponentTypeCodes.OptionSet => "OptionSet",
            ComponentTypeCodes.Form or ComponentTypeCodes.SystemForm => "Form",
            ComponentTypeCodes.SavedQuery => "View",
            ComponentTypeCodes.SavedQueryVisualization => "Chart",
            ComponentTypeCodes.WebResource => "WebResource",
            ComponentTypeCodes.Workflow => "Workflow",
            ComponentTypeCodes.AppModule => "AppModule",
            ComponentTypeCodes.SDKMessageProcessingStep => "PluginStep",
            ComponentTypeCodes.Role => "Role",
            ComponentTypeCodes.SiteMap => "SiteMap",
            _ => $"Component_{componentTypeCode}"
        };
    }

    private async Task<ResolvedComponentName> ResolveEntityAsync(Guid metadataId, CancellationToken cancellationToken)
    {
        var request = new RetrieveEntityRequest
        {
            MetadataId = metadataId,
            EntityFilters = EntityFilters.Entity
        };

        var response = await Task.Run(
            () => (RetrieveEntityResponse)_serviceClient.Execute(request),
            cancellationToken);

        var metadata = response.EntityMetadata;
        var displayName = metadata.DisplayName?.UserLocalizedLabel?.Label ?? metadata.LogicalName;

        return new ResolvedComponentName(metadata.LogicalName, displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveAttributeAsync(Guid metadataId, CancellationToken cancellationToken)
    {
        var request = new RetrieveAttributeRequest
        {
            MetadataId = metadataId
        };

        var response = await Task.Run(
            () => (RetrieveAttributeResponse)_serviceClient.Execute(request),
            cancellationToken);

        var metadata = response.AttributeMetadata;
        var displayName = metadata.DisplayName?.UserLocalizedLabel?.Label ?? metadata.LogicalName;

        return new ResolvedComponentName(metadata.LogicalName, displayName, metadata.EntityLogicalName);
    }

    private async Task<ResolvedComponentName> ResolveOptionSetAsync(Guid metadataId, CancellationToken cancellationToken)
    {
        var request = new RetrieveOptionSetRequest
        {
            MetadataId = metadataId
        };

        var response = await Task.Run(
            () => (RetrieveOptionSetResponse)_serviceClient.Execute(request),
            cancellationToken);

        var metadata = response.OptionSetMetadata;
        var displayName = metadata.DisplayName?.UserLocalizedLabel?.Label ?? metadata.Name;

        return new ResolvedComponentName(metadata.Name ?? metadataId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveFormAsync(Guid formId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("systemform", formId, new ColumnSet("name", "objecttypecode")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? formId.ToString();
        var tableName = entity.GetAttributeValue<string>("objecttypecode");

        return new ResolvedComponentName(formId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveViewAsync(Guid viewId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("savedquery", viewId, new ColumnSet("name", "returnedtypecode")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? viewId.ToString();
        var tableName = entity.GetAttributeValue<string>("returnedtypecode");

        return new ResolvedComponentName(viewId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveChartAsync(Guid chartId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("savedqueryvisualization", chartId, new ColumnSet("name", "primaryentitytypecode")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? chartId.ToString();
        var tableName = entity.GetAttributeValue<string>("primaryentitytypecode");

        return new ResolvedComponentName(chartId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveWebResourceAsync(Guid webResourceId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("webresource", webResourceId, new ColumnSet("name", "displayname")),
            cancellationToken);

        var logicalName = entity.GetAttributeValue<string>("name") ?? webResourceId.ToString();
        var displayName = entity.GetAttributeValue<string>("displayname") ?? logicalName;

        return new ResolvedComponentName(logicalName, displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveWorkflowAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("workflow", workflowId, new ColumnSet("name", "primaryentity")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? workflowId.ToString();
        var tableName = entity.GetAttributeValue<string>("primaryentity");

        return new ResolvedComponentName(workflowId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveAppModuleAsync(Guid appModuleId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("appmodule", appModuleId, new ColumnSet("uniquename", "name")),
            cancellationToken);

        var uniqueName = entity.GetAttributeValue<string>("uniquename") ?? appModuleId.ToString();
        var displayName = entity.GetAttributeValue<string>("name") ?? uniqueName;

        return new ResolvedComponentName(uniqueName, displayName, null);
    }

    private async Task<ResolvedComponentName> ResolvePluginStepAsync(Guid stepId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("sdkmessageprocessingstep", stepId,
                new ColumnSet("name", "sdkmessageid")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? stepId.ToString();

        return new ResolvedComponentName(stepId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("role", roleId, new ColumnSet("name")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? roleId.ToString();

        return new ResolvedComponentName(roleId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveSiteMapAsync(Guid siteMapId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("sitemap", siteMapId, new ColumnSet("sitemapname")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("sitemapname") ?? siteMapId.ToString();

        return new ResolvedComponentName(siteMapId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveCustomApiAsync(Guid customApiId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("customapi", customApiId, new ColumnSet("uniquename", "displayname")),
            cancellationToken);

        var uniqueName = entity.GetAttributeValue<string>("uniquename") ?? customApiId.ToString();
        var displayName = entity.GetAttributeValue<string>("displayname") ?? uniqueName;

        return new ResolvedComponentName(uniqueName, displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveConnectionRoleAsync(Guid connectionRoleId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("connectionrole", connectionRoleId, new ColumnSet("name")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? connectionRoleId.ToString();

        return new ResolvedComponentName(connectionRoleId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveEmailTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("template", templateId, new ColumnSet("title")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("title") ?? templateId.ToString();

        return new ResolvedComponentName(templateId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveReportAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("report", reportId, new ColumnSet("name")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? reportId.ToString();

        return new ResolvedComponentName(reportId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveDuplicateRuleAsync(Guid duplicateRuleId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("duplicaterule", duplicateRuleId, new ColumnSet("name", "baseentityname")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? duplicateRuleId.ToString();
        var tableName = entity.GetAttributeValue<string>("baseentityname");

        return new ResolvedComponentName(duplicateRuleId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveSlaAsync(Guid slaId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("sla", slaId, new ColumnSet("name", "objecttypecode")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? slaId.ToString();
        var tableName = entity.GetAttributeValue<string>("objecttypecode");

        return new ResolvedComponentName(slaId.ToString(), displayName, tableName);
    }

    private async Task<ResolvedComponentName> ResolveRoutingRuleAsync(Guid routingRuleId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("routingrule", routingRuleId, new ColumnSet("name")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? routingRuleId.ToString();

        return new ResolvedComponentName(routingRuleId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveCustomControlAsync(Guid customControlId, CancellationToken cancellationToken)
    {
        var entity = await Task.Run(
            () => _serviceClient.Retrieve("customcontrol", customControlId, new ColumnSet("name")),
            cancellationToken);

        var displayName = entity.GetAttributeValue<string>("name") ?? customControlId.ToString();

        return new ResolvedComponentName(customControlId.ToString(), displayName, null);
    }

    private async Task<ResolvedComponentName> ResolveRelationshipAsync(Guid metadataId, CancellationToken cancellationToken)
    {
        var request = new RetrieveRelationshipRequest
        {
            MetadataId = metadataId
        };

        var response = await Task.Run(
            () => (RetrieveRelationshipResponse)_serviceClient.Execute(request),
            cancellationToken);

        var metadata = response.RelationshipMetadata;
        var schemaName = metadata.SchemaName;

        return new ResolvedComponentName(schemaName, schemaName, null);
    }

    /// <summary>
    /// Clears the session cache (useful for testing or when switching connections).
    /// </summary>
    public void ClearSessionCache()
    {
        _sessionCache.Clear();
    }
}
