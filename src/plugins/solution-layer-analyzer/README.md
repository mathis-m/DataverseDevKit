# Solution Layer Analyzer

A comprehensive plugin for analyzing solution component layering in Dataverse environments.

## Features

- **Solution Discovery**: Search and select solutions by name, publisher, managed status, and version
- **Layer Analysis**: Build complete layer stacks for components across multiple solutions
- **Advanced Filtering**: Filter components by existence rules, order constraints, and component properties
- **Diff Views**: Compare component payloads (XML/JSON) between different layers
- **Performance**: In-memory SQLite database with indexing for fast queries

## Commands

### `index`
Build an index of solutions, components, and their layers.

**Parameters:**
- `connectionId`: Environment connection ID
- `sourceSolutions`: List of source solution names (baseline)
- `targetSolutions`: List of target solution names to analyze
- `includeComponentTypes`: Component types to include (Form, View, Entity, Attribute, etc.)
- `maxParallel`: Maximum parallel operations (default: 8)
- `payloadMode`: "lazy" or "eager" payload loading

**Returns:** Statistics and warnings

### `query`
Query indexed components with advanced filtering.

**Parameters:**
- `filters`: Filter AST (HAS, ORDER_STRICT, ORDER_FLEX, etc.)
- `groupBy`: Fields to group by
- `select`: Fields to return
- `paging`: Pagination settings
- `sort`: Sort configuration

**Returns:** Filtered component list

### `details`
Get full layer stack for a specific component.

**Parameters:**
- `componentId`: Component GUID

**Returns:** Complete layer information

### `diff`
Compare payloads between two layers of a component.

**Parameters:**
- `componentId`: Component GUID
- `left`: Left layer (solution name, payload type)
- `right`: Right layer (solution name, payload type)

**Returns:** Normalized payload texts for diff

## Usage

This is a backend-only plugin (no UI in v1). It can be invoked via the DDK command interface or integrated with custom UIs.

## Technical Details

### Component Layer Query
The plugin queries the `msdyn_componentlayer` virtual entity with:
- Filter: `(msdyn_componentid eq '{guid}' and msdyn_solutioncomponentname eq '{type}')`
- Returns layer metadata and payload in `msdyn_changes` and `msdyn_componentjson` fields

### Data Model
- **Solutions**: Cached solution metadata
- **Components**: Component definitions with type, logical name, etc.
- **Layers**: Ordered layer stacks per component
- **Artifacts**: Optional payload cache for diff operations

### Performance
- In-memory SQLite database with indexes
- Lazy payload loading by default
- Parallel solution/component queries
- Progress events for long operations
