# Solution Layer Analyzer - Implementation Guide

This document provides guidance for implementing the remaining phases of the Solution Layer Analyzer plugin.

## Current Status

### ✅ Completed (Phase 1)
- Plugin structure and manifest
- Data models (Solution, Component, Layer, Artifact)
- In-memory SQLite database with EF Core
- DTO models for all commands
- Plugin class with command stubs
- Component type constants
- Build configuration

### 🔄 Next Steps (Phase 2-6)

## Phase 2: Core Backend Implementation

### 2.1 Solution Discovery

**Requirement**: Query Dataverse to discover solutions matching the source/target criteria.

**Implementation Steps**:
1. Add Dataverse client capability to plugin context
2. Query the `solution` table with filters:
   ```csharp
   // Pseudo-code
   var query = new QueryExpression("solution");
   query.Criteria.AddCondition("uniquename", ConditionOperator.In, targetSolutions);
   query.ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", 
                                    "publisherid", "ismanaged", "version");
   ```
3. Map results to `Solution` entity and save to database
4. Emit progress events: `plugin:sla:progress` with phase: "solutions"

### 2.2 Component Layer Discovery

**Requirement**: For each component in target solutions, query the `msdyn_componentlayer` virtual entity.

**Key Query Pattern**:
```
GET [org]/api/data/v9.2/msdyn_componentlayers?
  $filter=msdyn_componentid eq '{guid}' and msdyn_solutioncomponentname eq 'SavedQuery'
  &$select=msdyn_componentlayerid,msdyn_solutionname,msdyn_name,msdyn_order,
           msdyn_changes,msdyn_componentjson
  &$orderby=msdyn_order asc
```

**Response Fields**:
- `msdyn_componentid`: Component GUID
- `msdyn_solutioncomponentname`: Component type name (e.g., "SavedQuery")
- `msdyn_solutionname`: Solution unique name
- `msdyn_order`: Layer ordinal (0 = base)
- `msdyn_changes`: JSON string with attribute changes (for diff)
- `msdyn_componentjson`: Full component JSON

**Implementation Steps**:
1. For each solution, query `solutioncomponent` table to get all components
2. Filter by `includeComponentTypes` from request
3. For each component, query `msdyn_componentlayer` with filter
4. Parse response and create `Layer` entities ordered by `msdyn_order`
5. Store in database with proper foreign keys
6. Emit progress: phase: "layers", percent: calculated

**Challenges**:
- Virtual entity may have throttling limits → implement retry with exponential backoff
- Large solutions may have 10k+ components → batch queries
- Not all component types support layering → handle gracefully with warnings

### 2.3 Payload Retrieval (Lazy Mode)

For `payloadMode: "lazy"`, skip payload retrieval during indexing. Only fetch when `diff` command is called.

For `payloadMode: "eager"`, fetch payloads during indexing:

**Form Payloads**:
```csharp
// Query systemform table
var form = await client.Retrieve("systemform", formId, 
    new ColumnSet("formxml", "objecttypecode", "name"));
var formXml = form.GetAttributeValue<string>("formxml");
```

**View Payloads**:
```csharp
// Query savedquery table
var view = await client.Retrieve("savedquery", viewId,
    new ColumnSet("fetchxml", "layoutxml", "returnedtypecode", "name"));
var fetchXml = view.GetAttributeValue<string>("fetchxml");
var layoutXml = view.GetAttributeValue<string>("layoutxml");
```

**Ribbon Payloads**:
From `msdyn_componentjson` field in layer response (already includes RibbonDiffXml).

## Phase 3: Filter & Query Engine

### 3.1 Filter AST Models

Create filter AST classes in `Models/Filters/`:

```csharp
public abstract record FilterNode;

public record HasFilter(string SolutionName) : FilterNode;
public record HasAnyFilter(List<string> SolutionNames) : FilterNode;
public record HasAllFilter(List<string> SolutionNames) : FilterNode;
public record HasNoneFilter(List<string> SolutionNames) : FilterNode;

public record OrderStrictFilter(List<FilterNode> Sequence) : FilterNode;
public record OrderFlexFilter(List<FilterNode> Sequence) : FilterNode;

public record AndFilter(List<FilterNode> Children) : FilterNode;
public record OrFilter(List<FilterNode> Children) : FilterNode;

public record ComponentTypeFilter(string ComponentType) : FilterNode;
public record ManagedFilter(bool IsManaged) : FilterNode;
public record PublisherFilter(string Publisher) : FilterNode;
```

### 3.2 Filter Evaluation

Create `Services/FilterEvaluator.cs`:

```csharp
public class FilterEvaluator
{
    public bool Evaluate(FilterNode filter, Component component)
    {
        return filter switch
        {
            HasFilter has => component.Layers.Any(l => l.SolutionName == has.SolutionName),
            HasAnyFilter any => any.SolutionNames.Any(s => 
                component.Layers.Any(l => l.SolutionName == s)),
            OrderStrictFilter strict => EvaluateOrderStrict(strict, component),
            OrderFlexFilter flex => EvaluateOrderFlex(flex, component),
            AndFilter and => and.Children.All(c => Evaluate(c, component)),
            OrFilter or => or.Children.Any(c => Evaluate(c, component)),
            // ... other cases
        };
    }

    private bool EvaluateOrderStrict(OrderStrictFilter filter, Component component)
    {
        var sequence = component.Layers.OrderBy(l => l.Ordinal)
            .Select(l => l.SolutionName).ToList();
        
        // Find subsequence with no gaps
        for (int i = 0; i <= sequence.Count - filter.Sequence.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < filter.Sequence.Count; j++)
            {
                if (!MatchesFilterNode(filter.Sequence[j], sequence[i + j]))
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }
}
```

### 3.3 Query Implementation

Update `ExecuteQueryAsync` to:
1. Deserialize filters as `FilterNode`
2. Apply `FilterEvaluator` to all components
3. Apply grouping (LINQ `GroupBy`)
4. Apply sorting (LINQ `OrderBy`/`ThenBy`)
5. Apply paging (`Skip`/`Take`)

## Phase 4: Component Layer Retrieval Details

### 4.1 Diff Command Implementation

```csharp
private async Task<JsonElement> ExecuteDiffAsync(string payload, CancellationToken ct)
{
    var request = JsonSerializer.Deserialize<DiffRequest>(payload);
    
    // Get component
    var component = await _dbContext.Components
        .Include(c => c.Layers)
        .FirstAsync(c => c.ComponentId == request.ComponentId, ct);
    
    // Find layers
    var leftLayer = component.Layers.First(l => l.SolutionName == request.Left.SolutionName);
    var rightLayer = component.Layers.First(l => l.SolutionName == request.Right.SolutionName);
    
    // Get or fetch payloads
    var leftText = await GetPayload(component, leftLayer, request.Left.PayloadType, ct);
    var rightText = await GetPayload(component, rightLayer, request.Right.PayloadType, ct);
    
    // Normalize
    leftText = NormalizePayload(leftText, DeterminePayloadType(component.ComponentType));
    rightText = NormalizePayload(rightText, DeterminePayloadType(component.ComponentType));
    
    return new DiffResponse
    {
        LeftText = leftText,
        RightText = rightText,
        Mime = GetMimeType(component.ComponentType)
    };
}
```

### 4.2 Payload Normalization

**XML Normalization**:
```csharp
private string NormalizeXml(string xml)
{
    var doc = XDocument.Parse(xml);
    // Sort attributes
    foreach (var element in doc.Descendants())
    {
        var attrs = element.Attributes().OrderBy(a => a.Name.ToString()).ToList();
        element.RemoveAttributes();
        element.Add(attrs);
    }
    return doc.ToString(SaveOptions.None);
}
```

**JSON Normalization**:
```csharp
private string NormalizeJson(string json)
{
    var obj = JsonSerializer.Deserialize<JsonElement>(json);
    return JsonSerializer.Serialize(obj, new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
}
```

## Phase 5: Frontend UI (Future)

The UI will be a React TypeScript module federation remote. Create in `ui/` folder:

```
solution-layer-analyzer/ui/
├── package.json
├── vite.config.ts
├── src/
│   ├── Plugin.tsx              # Main entry point
│   ├── components/
│   │   ├── SolutionSelector.tsx
│   │   ├── FilterBuilder.tsx
│   │   ├── ResultsGrid.tsx
│   │   ├── DetailsDrawer.tsx
│   │   └── DiffView.tsx
│   ├── hooks/
│   │   └── useSolutionLayerAnalyzer.ts
│   └── types/
│       └── index.ts
```

**Key Dependencies**:
- `@fluentui/react-components` (UI)
- `@tanstack/react-table` (grid)
- `@tanstack/react-query` (data fetching)
- `@monaco-editor/react` (diff view)
- `recharts` (charts)

## Phase 6: Testing

### Unit Tests

Create `tests/` folder:

```csharp
public class FilterEvaluatorTests
{
    [Fact]
    public void HasFilter_ShouldMatchComponent()
    {
        var component = new Component
        {
            Layers = new List<Layer>
            {
                new Layer { SolutionName = "Core" }
            }
        };
        
        var evaluator = new FilterEvaluator();
        var result = evaluator.Evaluate(new HasFilter("Core"), component);
        
        Assert.True(result);
    }
}
```

### Integration Tests

Mock Dataverse responses:

```csharp
public class IndexCommandTests
{
    [Fact]
    public async Task Index_ShouldPopulateDatabase()
    {
        // Arrange
        var mockClient = new Mock<IDataverseClient>();
        mockClient.Setup(x => x.RetrieveMultiple(It.IsAny<QueryExpression>()))
            .Returns(CreateMockSolutions());
        
        var plugin = new SolutionLayerAnalyzerPlugin();
        await plugin.InitializeAsync(mockContext);
        
        // Act
        var result = await plugin.ExecuteAsync("index", indexRequest);
        
        // Assert
        var stats = JsonSerializer.Deserialize<IndexResponse>(result.ToString());
        Assert.Equal(2, stats.Stats.Solutions);
    }
}
```

## Key Decisions & Constraints

1. **In-Memory Only**: Database is in-memory; cleared on plugin restart. This is by design for:
   - Security (no persistent data in plugin storage)
   - Performance (SQLite in-memory is fast)
   - Simplicity (no migration/versioning)

2. **Read-Only**: Plugin does NOT modify Dataverse. All operations are read-only queries.

3. **Virtual Entity Limitations**:
   - `msdyn_componentlayer` may not support all component types
   - Payload extraction depends on component type
   - Some payloads require additional queries to entity tables

4. **Performance Considerations**:
   - Batch queries where possible (max 1000 per batch)
   - Use `$select` to limit returned columns
   - Implement caching for frequently accessed data
   - Consider pagination for large result sets (10k+ components)

5. **Error Handling**:
   - Retry transient Dataverse errors (throttling, network)
   - Log warnings for unsupported component types
   - Continue indexing if individual components fail
   - Report all warnings in response

## API Reference

### Dataverse Tables

- **solution**: Solution metadata
- **solutioncomponent**: Components in solutions
- **msdyn_componentlayer**: Virtual entity for layer information
- **systemform**: Forms
- **savedquery**: Views
- **webresource**: Web resources
- **plugintype** / **sdkmessageprocessingstep**: Plugins

### Component Type Mapping

| Display Name | Table Name | Type Code | Supports Layers |
|--------------|------------|-----------|-----------------|
| Form | systemform | 60 | Yes |
| View | savedquery | 26 | Yes |
| Entity | entity | 1 | Yes |
| Attribute | attribute | 2 | Yes |
| Ribbon | ribboncustomization | 50 | Yes |
| Web Resource | webresource | 61 | Yes |
| Plugin Step | sdkmessageprocessingstep | 92 | Yes |
| App Module | appmodule | 80 | Yes |

## Useful Links

- [Component Layering Documentation](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/solution-layers)
- [Solution Component Reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/solutioncomponent)
- [Dataverse Web API](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/overview)
- [EF Core In-Memory Database](https://learn.microsoft.com/en-us/ef/core/providers/in-memory/)

## Contact & Support

For questions or issues with implementation, refer to the main repository documentation or create an issue in the GitHub repository.
