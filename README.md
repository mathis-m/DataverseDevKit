# Dataverse DevKit

A comprehensive development toolkit for Microsoft Dataverse, featuring an extensible plugin architecture and modern cross-platform UI.

## ✨ Features

- **Cross-Platform Desktop** - Native .NET MAUI application for Windows and macOS
- **Modern Web UI** - React 18 + TypeScript with FluentUI v9 design system
- **Extensible Plugins** - Out-of-process plugin architecture with sandboxed execution
- **Connection Management** - Manage multiple Dataverse environments with OAuth authentication
- **Tab Workspace** - Run multiple plugin instances simultaneously with drag-and-drop tabs
- **Solution Analysis** - Deep analysis of solution component layering across environments
- **Module Federation** - Dynamic plugin loading at runtime
- **Dark Mode** - Full theme support (light/dark/system)

## 🔌 Plugins

DataverseDevKit includes a powerful plugin system that allows extending functionality. Plugins run in isolated processes with their own UI components loaded dynamically.

### Solution Layer Analyzer

**Purpose**: Perform in-depth analysis of solution components and their layering across Dataverse environments.

**Key Features**:
- **Solution Discovery** - Search and filter solutions by name, publisher, version, and managed status
- **Layer Stack Analysis** - Build complete layer stacks for components across multiple solutions
- **Advanced Filtering** - Query components with complex rules (HAS, ORDER_STRICT, ORDER_FLEX)
- **Diff Views** - Compare component payloads (XML/JSON) between different layers
- **High Performance** - In-memory SQLite database with parallel queries and indexing

**Supported Component Types**:
- Forms (systemform)
- Views (savedquery)
- Entities (entity)
- Attributes (attribute)
- Ribbons (ribboncustomization)
- Web Resources (webresource)
- Plugin Steps (sdkmessageprocessingstep)
- App Modules (appmodule)

**Commands**:
- `index` - Build an index of solutions, components, and layers
- `query` - Query indexed components with advanced filtering
- `details` - Get full layer stack for a specific component
- `diff` - Compare payloads between two layers

**Status**: Fully implemented with comprehensive React UI including dashboards, visualizations (tree, Sankey, heatmap, network graph, etc.), filtering, and diff views.

## 🏗️ Architecture

DataverseDevKit uses a layered architecture with clear separation of concerns and secure inter-process communication.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Interface                          │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              Shell UI (React + TypeScript)                │  │ 
│  │  • Connection Management  • Plugin Marketplace            │  │
│  │  • Tab Workspace         • Settings & Themes              │  │
│  │                                                           │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │  │
│  │  │  Plugin UI 1 │  │  Plugin UI 2 │  │  Plugin UI n │     │  │
│  │  │ (Module Fed) │  │ (Module Fed) │  │ (Module Fed) │     │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘     │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              ↕
                      JSON-RPC over HybridWebView
                              ↕
┌─────────────────────────────────────────────────────────────────┐
│                       MAUI Host (.NET)                          │
│                                                                 │
│  ┌──────────────────┐     ┌─────────────────────────────────┐   │
│  │  JSON-RPC Bridge │     │   Core Services                 │   │
│  │  • Method Router │     │   • PluginHostManager           │   │
│  │  • Event Emitter │     │   • ConnectionService           │   │
│  └──────────────────┘     │   • TokenCallbackServer         │   │
│                           └─────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↕
                    gRPC over Unix Domain Socket
                              ↕
┌─────────────────────────────────────────────────────────────────┐
│                  Plugin Runtime Worker (.NET)                   │
│                                                                 │
│  ┌──────────────────┐     ┌─────────────────────────────────┐   │
│  │  gRPC Server     │     │   Plugin Loader                 │   │
│  │  • PluginHost    │     │   • Assembly Isolation          │   │
│  │  • TokenProvider │     │   • Dependency Management       │   │
│  └──────────────────┘     │   • Plugin Load Context         │   │
│                           └─────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↕
                       Plugin Interface (IToolPlugin)
                              ↕
┌─────────────────────────────────────────────────────────────────┐
│                         Plugin DLLs                             │
│                                                                 │
│  ┌──────────────────────┐    ┌──────────────────────────────┐   │
│  │ Solution Layer       │    │ Custom Plugins               │   │
│  │ Analyzer             │    │ • Community                  │   │
│  │ • In-memory SQLite   │    │ • Third-party                │   │
│  │ • EF Core            │    │ • Organization-specific      │   │
│  └──────────────────────┘    └──────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              ↕
                     Microsoft Dataverse APIs
                              ↕
┌─────────────────────────────────────────────────────────────────┐
│                    Microsoft Dataverse                          │
│               (Power Platform Environment)                      │
└─────────────────────────────────────────────────────────────────┘
```

### Layer Interactions

#### 1. Shell UI Layer (Web Frontend)

**Technology**: React 18, TypeScript, FluentUI v9, Vite, Module Federation

**Responsibilities**:
- Render application shell (tabs, sidebar, marketplace, settings)
- Load plugin UI components dynamically via Module Federation
- Manage UI state with Zustand
- Handle user interactions and route commands to backend

**Communication**: Uses JSON-RPC protocol over MAUI's HybridWebView to communicate with the Host layer. Messages are serialized as JSON and transmitted bidirectionally.

#### 2. MAUI Host Layer (.NET)

**Technology**: .NET 10, .NET MAUI, HybridWebView

**Responsibilities**:
- Provide native desktop window and WebView host
- Implement JSON-RPC bridge for UI ↔ Host communication
- Manage plugin lifecycle (start, stop, health checks)
- Handle OAuth token management and storage
- Route plugin commands to appropriate Plugin Runtime workers
- Emit events back to UI (progress updates, notifications)

**Key Components**:
- `JsonRpcBridge.cs` - Dispatcher for method calls from UI
- `PluginHostManager.cs` - Manages plugin worker processes
- `ConnectionService.cs` - Stores and retrieves connection credentials
- `TokenCallbackServer.cs` - OAuth callback listener

**Communication**: 
- **Upstream** (to UI): JSON-RPC responses and events via HybridWebView
- **Downstream** (to Plugins): gRPC calls over Unix domain sockets

#### 3. Plugin Runtime Layer (.NET)

**Technology**: .NET 10, gRPC, Protocol Buffers

**Responsibilities**:
- Run as separate process for each plugin instance (sandboxing)
- Load plugin assemblies with isolated AssemblyLoadContext
- Expose gRPC server for Host to invoke plugin commands
- Call back to Host for Dataverse authentication tokens
- Manage plugin dependencies without affecting Host

**Key Components**:
- `Program.cs` - gRPC server startup
- `PluginLoader.cs` - Dynamic assembly loading
- `PluginHostGrpcService.cs` - Implements gRPC service contract

**Communication**:
- **Upstream** (to Host): gRPC responses with command results
- **Downstream** (to Plugins): Direct method invocation on `IToolPlugin` interface

#### 4. Plugin DLL Layer

**Technology**: .NET 10, Custom plugin logic

**Responsibilities**:
- Implement `IToolPlugin` interface
- Execute plugin-specific business logic
- Interact with Dataverse APIs using provided tokens
- Return structured data to Runtime layer

**Interface**:
```csharp
public interface IToolPlugin
{
    string PluginId { get; }
    string Name { get; }
    Task InitializeAsync(IPluginContext context, CancellationToken ct);
    Task<object?> ExecuteAsync(string command, string? payload, CancellationToken ct);
    Task ShutdownAsync(CancellationToken ct);
}
```

**Communication**: Synchronous method calls from Plugin Runtime layer

### Communication Protocols

#### JSON-RPC (UI ↔ Host)

Used for **bidirectional** communication between React UI and .NET Host. The UI can send commands to the host, and the host can push events to the UI.

**Request Format** (UI → Host):
```json
{
  "jsonrpc": "2.0",
  "id": "unique-request-id",
  "method": "plugin.execute",
  "params": {
    "pluginId": "com.ddk.solutionlayeranalyzer",
    "instanceId": "instance-123",
    "command": "index",
    "payload": "{...}"
  }
}
```

**Response Format** (Host → UI):
```json
{
  "jsonrpc": "2.0",
  "id": "unique-request-id",
  "result": {
    "success": true,
    "data": {...}
  }
}
```

**Events** (Host → UI, Push Notifications):
```json
{
  "jsonrpc": "2.0",
  "method": "plugin.event",
  "params": {
    "event": "plugin:sla:progress",
    "data": {"phase": "layers", "percent": 45}
  }
}
```

**Event Subscription** (UI):
Plugins subscribe to events using the singleton `hostBridge`:
```typescript
import { hostBridge } from '@ddk/host-sdk';

// Subscribe to events from plugin backend
hostBridge.addEventListener('plugin:sla:progress', (data) => {
  console.log('Progress:', data.phase, data.percent);
});

hostBridge.addEventListener('plugin:sla:index-complete', (data) => {
  console.log('Indexing complete:', data.stats);
});
```

#### gRPC (Host ↔ Plugin Runtime)

Defined in `src/dotnet/Contracts/pluginhost.proto`:

**Services**:
- `PluginHostService` - Host calls plugin commands
- `TokenProviderHostService` - Plugin requests auth tokens

**Key RPCs**:
```protobuf
service PluginHostService {
  rpc Initialize(InitializeRequest) returns (InitializeResponse);
  rpc Execute(ExecuteRequest) returns (ExecuteResponse);
  rpc Shutdown(ShutdownRequest) returns (ShutdownResponse);
}

service TokenProviderHostService {
  rpc GetToken(GetTokenRequest) returns (GetTokenResponse);
}
```

### Module Federation (Plugin UI)

Plugin UI components are loaded dynamically at runtime using Vite Module Federation. The shell loads plugin remotes dynamically based on plugin manifests, rather than having them hardcoded in configuration.

**Shell Configuration** (`web/apps/shell/vite.config.ts`):
```typescript
federation({
  name: 'shell',
  remotes: {},  // Empty - plugins loaded dynamically at runtime
  shared: {
    'react': { version: '18.3.1' },
    'react-dom': { version: '18.3.1' },
    '@fluentui/react-components': {},
    '@fluentui/react-icons': {},
    '@ddk/host-sdk': {},  // Includes singleton hostBridge instance
  }
})
```

**Plugin Configuration** (example `vite.config.ts`):
```typescript
federation({
  name: 'solutionLayerAnalyzer',
  filename: 'remoteEntry.js',
  exposes: {
    './Plugin': './src/Plugin.tsx'
  },
  shared: ['react', 'react-dom', '@fluentui/react-components', '@ddk/host-sdk']
})
```

**HostBridge Singleton**: The `@ddk/host-sdk` package exports a singleton `hostBridge` instance that all plugins share for communication with the host. This ensures consistent JSON-RPC messaging and event handling across all plugin instances.

```typescript
// From @ddk/host-sdk
import { hostBridge } from '@ddk/host-sdk';

// All plugins use the same instance
const result = await hostBridge.executeCommand({ pluginId, command, payload });
```

This architecture ensures:
- Shared dependencies are loaded once
- Plugins are isolated in their own bundles
- Dynamic loading without rebuilding the Shell
- Consistent communication layer via singleton hostBridge

### Data Flow Example: Running a Plugin Command

1. **User Action**: User clicks "Analyze Solutions" in the Solution Layer Analyzer UI
2. **UI → Host**: Plugin UI calls `hostBridge.executeCommand()`, which sends JSON-RPC request to Host
3. **Host Processing**: `JsonRpcBridge` receives request, routes to `PluginHostManager`
4. **Host → Runtime**: `PluginHostManager` sends gRPC `Execute` call to Plugin Runtime worker
5. **Runtime → Plugin**: Plugin Runtime invokes `plugin.ExecuteAsync("index", payload)`
6. **Plugin Execution**: 
   - Plugin calls back to Host via gRPC to get Dataverse token
   - Plugin queries Dataverse APIs
   - Plugin processes data (e.g., builds SQLite index)
   - Plugin emits progress events back through chain
7. **Response Chain**: Result flows back: Plugin → Runtime → Host → UI
8. **UI Update**: React component receives result and updates display

## 📁 Project Structure

```
DataverseDevKit/
├── src/
│   ├── dotnet/                           # .NET Backend
│   │   ├── Host/                         # MAUI Desktop Application
│   │   │   ├── Bridge/                   # JSON-RPC implementation
│   │   │   ├── Services/                 # Core services
│   │   │   └── wwwroot/                  # Embedded web assets
│   │   ├── PluginRuntime/                # Out-of-process plugin worker
│   │   │   ├── Services/                 # gRPC service implementation
│   │   │   └── Runtime/                  # Plugin loader
│   │   ├── Shared/                       # Core abstractions
│   │   │   ├── Abstractions/             # Plugin interfaces
│   │   │   └── Models/                   # Shared DTOs
│   │   ├── Contracts/                    # gRPC Protocol Buffers
│   │   └── DataverseDevKit.slnx          # .NET Solution
│   └── plugins/                          # Plugin Projects
│       └── solution-layer-analyzer/      # Solution Layer Analyzer
│           ├── src/                      # Backend implementation (.NET)
│           ├── ui/                       # Frontend UI (React, fully implemented)
│           └── plugin.manifest.json      # Plugin metadata
├── web/                                  # Web Frontend (pnpm monorepo)
│   ├── packages/
│   │   ├── host-sdk/                     # TypeScript SDK for plugins
│   │   └── ui-components/                # Shared FluentUI components
│   └── apps/
│       └── shell/                        # Main Shell application
├── tools/                                # Build tools and schemas
├── docs/                                 # Additional documentation
├── build-web.ps1                         # Web build script
└── README.md                             # This file
```

## 🛠️ Technology Stack

### Backend (.NET 10)
- **.NET MAUI** - Cross-platform desktop framework
- **HybridWebView** - Native WebView with JavaScript interop
- **gRPC** - High-performance inter-process communication
- **Protocol Buffers** - Efficient binary serialization
- **Microsoft.PowerPlatform.Dataverse.Client** - Official Dataverse SDK
- **SQLite + EF Core** - In-memory data processing (plugins)

### Frontend (Web)
- **React 18** - UI library with concurrent features
- **TypeScript 5.7** - Type-safe JavaScript
- **FluentUI v9** - Microsoft's design system
- **Vite 6** - Next-generation build tool
- **pnpm** - Fast, disk-efficient package manager
- **Zustand** - Lightweight state management
- **@dnd-kit** - Modern drag-and-drop library
- **Module Federation** - Dynamic remote module loading

## 🚀 Getting Started

### Prerequisites

```powershell
# .NET 10 SDK
dotnet --version  # Should be 10.0.0 or higher

# Node.js 20+ and pnpm
node --version    # Should be >= 20
pnpm --version    # Should be >= 9

# Install pnpm if needed
npm install -g pnpm@latest
```

### Quick Start

#### 1. Install Dependencies

```powershell
# .NET dependencies
cd src/dotnet
dotnet restore DataverseDevKit.slnx

# Web dependencies
cd ../../web
pnpm install
```

#### 2. Development Mode

Run the full application in development mode:

```powershell
# Terminal 1: Build web assets and run MAUI app
./build-web.ps1
cd src/dotnet
dotnet run --project Host/DataverseDevKit.Host.csproj
```

Or run UI standalone for faster development:

```powershell
# Shell UI (hot reload enabled)
cd web/apps/shell
pnpm dev
# Opens at http://localhost:5173
```

#### 3. Build for Production

```powershell
# Build web frontend
./build-web.ps1

# Build .NET solution
cd src/dotnet
dotnet build DataverseDevKit.slnx -c Release

# Publish MAUI app
dotnet publish Host/DataverseDevKit.Host.csproj -c Release
```

## 🔧 Creating Plugins

Plugins consist of two parts: backend (.NET) and frontend (React). Both are optional depending on your needs.

### Backend Plugin (.NET)

1. Create new plugin project:
```powershell
mkdir src/plugins/my-plugin
cd src/plugins/my-plugin
dotnet new classlib -f net10.0
```

2. Add reference to Shared:
```xml
<ItemGroup>
  <ProjectReference Include="../../dotnet/Shared/DataverseDevKit.Shared.csproj" />
</ItemGroup>
```

3. Implement `IToolPlugin`:
```csharp
using DataverseDevKit.Shared.Abstractions;

public class MyPlugin : IToolPlugin
{
    public string PluginId => "com.mycompany.myplugin";
    public string Name => "My Plugin";
    
    public async Task InitializeAsync(IPluginContext context, CancellationToken ct)
    {
        // Setup resources
    }
    
    public async Task<object?> ExecuteAsync(string command, string? payload, CancellationToken ct)
    {
        return command switch
        {
            "myCommand" => await HandleMyCommand(payload, ct),
            _ => throw new ArgumentException($"Unknown command: {command}")
        };
    }
    
    public async Task ShutdownAsync(CancellationToken ct)
    {
        // Cleanup
    }
}
```

4. Create `plugin.manifest.json`:
```json
{
  "id": "com.mycompany.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "description": "Does amazing things",
  "backend": {
    "assembly": "backend/MyPlugin.dll",
    "entryPoint": "MyCompany.MyPlugin"
  }
}
```

### Frontend Plugin (React)

1. Create plugin UI project:
```powershell
cd web/plugins/first-party
pnpm create vite my-plugin-ui --template react-ts
cd my-plugin-ui
```

2. Configure Module Federation in `vite.config.ts`:
```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import federation from '@originjs/vite-plugin-federation'

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'myPluginUi',
      filename: 'remoteEntry.js',
      exposes: {
        './Plugin': './src/Plugin.tsx'
      },
      shared: ['react', 'react-dom', '@fluentui/react-components']
    })
  ],
  build: {
    target: 'esnext'
  }
})
```

3. Create plugin component:
```typescript
import { FC } from 'react';
import { Button } from '@fluentui/react-components';
import { useHostBridge } from '@ddk/host-sdk';

interface PluginProps {
  instanceId: string;
  connectionId?: string;
}

const MyPlugin: FC<PluginProps> = ({ instanceId, connectionId }) => {
  const hostBridge = useHostBridge();
  
  const handleAction = async () => {
    const result = await hostBridge.executeCommand({
      pluginId: 'com.mycompany.myplugin',
      command: 'myCommand',
      payload: JSON.stringify({ data: 'test' })
    });
    console.log('Result:', result);
  };
  
  return (
    <div>
      <h1>My Plugin</h1>
      <Button onClick={handleAction}>Execute Command</Button>
    </div>
  );
};

export default MyPlugin;
```

4. Update manifest to include UI:
```json
{
  "ui": {
    "devEntry": "http://localhost:5175/dist/assets/remoteEntry.js",
    "entry": "frontend/assets/remoteEntry.js",
    "module": "./Plugin",
    "scope": "myPluginUi"
  }
}
```

For comprehensive plugin development guide, see [web/README.md](web/README.md).

## 🧪 Testing

```powershell
# .NET tests
cd src/dotnet
dotnet test

# Web type checking
cd web
pnpm type-check

# Lint
pnpm lint
```

## 📖 Documentation

- [Web Frontend Guide](web/README.md) - Detailed frontend development documentation
- [Plugin Development](src/plugins/README.md) - Plugin structure and build instructions
- [Solution Layer Analyzer](src/plugins/solution-layer-analyzer/README.md) - Plugin-specific documentation

## 🐛 Troubleshooting

### Web Build Issues

```powershell
cd web
rm -rf node_modules
rm pnpm-lock.yaml
pnpm install
pnpm build
```

### .NET Build Issues

```powershell
cd src/dotnet
dotnet clean DataverseDevKit.slnx
dotnet restore DataverseDevKit.slnx
dotnet build DataverseDevKit.slnx
```

### Plugin Not Loading

1. Check plugin manifest syntax
2. Verify plugin DLL is built and in correct location
3. Check MAUI Host logs for plugin startup errors
4. Ensure plugin implements `IToolPlugin` interface correctly

## 📄 License

See [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the Dataverse community**
