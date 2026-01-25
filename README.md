# Dataverse DevKit

A comprehensive, extensible development toolkit for Microsoft Dataverse with a modern plugin architecture.

## 🚀 Features

- **Cross-Platform Desktop App** - Built with .NET MAUI for Windows and macOS
- **Modern Web UI** - React 18 + TypeScript + FluentUI v9
- **Extensible Plugin System** - Out-of-process plugins with sandboxed execution
- **Module Federation** - Dynamic UI plugin loading at runtime
- **Tab-Based Workspace** - Multiple plugin instances with drag-and-drop
- **Connection Management** - Multiple Dataverse environments with OAuth
- **Dark Mode** - FluentUI theme integration (light/dark/system)
- **Plugin Marketplace** - Searchable catalog with categories and filters

## 📁 Project Structure

```
DataverseDevKit/
├── src/dotnet/                    # .NET Backend
│   ├── Shared/                    # Core abstractions
│   ├── PluginRuntime/             # Out-of-process plugin worker
│   ├── Host/                      # MAUI desktop application
│   └── DataverseDevKit.slnx       # .NET solution
├── web/                           # Web Frontend (pnpm monorepo)
│   ├── packages/
│   │   ├── host-sdk/              # TypeScript SDK for plugins
│   │   └── ui-components/         # Shared FluentUI components
│   ├── apps/
│   │   └── shell/                 # Main Shell app
│   └── plugins/
│       └── first-party/
│           └── hello-world-ui/    # Sample plugin
└── plugins/                       # Plugin projects
    └── first-party/
        └── hello-world/           # Hello World backend + manifest
```

## 🛠️ Tech Stack

### Backend (.NET 10)
- **.NET MAUI** - Cross-platform UI framework
- **gRPC** - Inter-process communication
- **Microsoft.PowerPlatform.Dataverse.Client** - Official SDK
- **Protocol Buffers** - Efficient serialization

### Frontend (Web)
- **React 18** - UI library
- **TypeScript 5.7** - Type safety
- **FluentUI v9** - Microsoft design system
- **Vite 6** - Build tool
- **pnpm** - Fast, efficient package manager
- **Zustand** - State management
- **@dnd-kit** - Drag and drop
- **Module Federation** - Dynamic plugin loading

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

### Build & Run

#### 1. Build .NET Backend

```powershell
cd src/dotnet
dotnet restore DataverseDevKit.slnx
dotnet build DataverseDevKit.slnx
```

#### 2. Install Web Dependencies

```powershell
cd web
pnpm install
```

#### 3. Development Mode

**Option A: Run Shell UI Standalone**
```powershell
cd web
pnpm dev
# Shell runs at http://localhost:5173
# HelloWorld plugin at http://localhost:5174 (in separate terminal)
```

**Option B: Run MAUI App with Web UI**
```powershell
# Build web assets
.\build-web.ps1

# Run MAUI app
cd src/dotnet
dotnet run --project Host/DataverseDevKit.Host.csproj
```

## 📦 Building for Production

### Build Everything

```powershell
# From repository root

# 1. Build web frontend
.\build-web.ps1

# 2. Build .NET solution
cd src\dotnet
dotnet build DataverseDevKit.slnx -c Release

# 3. Publish MAUI app
dotnet publish Host/DataverseDevKit.Host.csproj -c Release -f net10.0-windows10.0.19041.0
```

### Build Web Only

```powershell
cd web
pnpm build              # Build all
pnpm build:shell        # Build Shell only
pnpm build:plugins      # Build plugins only
```

## 🔌 Creating Plugins

See [web/README.md](web/README.md) for detailed plugin development guide.

### Quick Example

**Backend (.NET)**
```csharp
public class MyPlugin : IToolPlugin
{
    public string PluginId => "com.mycompany.myplugin";
    public string Name => "My Plugin";
    
    public async Task<object?> ExecuteAsync(string command, string? payload)
    {
        return new { success = true, message = "Hello from plugin!" };
    }
}
```

**Frontend (React)**
```typescript
const MyPlugin: React.FC<PluginProps> = ({ instanceId, connectionId }) => {
  const handleAction = () => {
    await hostBridge.executeCommand({
      pluginId: 'com.mycompany.myplugin',
      command: 'myCommand',
    });
  };
  
  return <Button onClick={handleAction}>Execute</Button>;
};

export default MyPlugin;
```

## 🏗️ Architecture

### Communication Flow

```
Shell UI (React) 
  ↕ JSON-RPC over HybridWebView
MAUI Host (.NET)
  ↕ gRPC
Plugin Worker (Process)
  ↕ Loading/Execution
Plugin DLL (IToolPlugin)
```

### Key Components

- **Host SDK** - TypeScript API for plugins to communicate with backend
- **JSON-RPC Bridge** - Marshals calls between web UI and .NET
- **Plugin Runtime** - Isolated worker process for plugin execution
- **Module Federation** - Dynamic loading of plugin UI components

## 📖 Documentation

- [Web Frontend Guide](web/README.md) - Comprehensive web development documentation
- Shell UI Features - Connection management, marketplace, tabs, settings
- Plugin Development - Backend (.NET) and frontend (React) plugin creation

## 🧪 Testing

```powershell
# .NET tests
cd src/dotnet
dotnet test

# Web type checking
cd web
pnpm type-check
```

## 🐛 Troubleshooting

### Web Build Issues

```powershell
cd web
rm -r node_modules
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

## 📝 What's Included

✅ .NET MAUI Host Application  
✅ Plugin Runtime Worker with gRPC  
✅ React Shell UI with FluentUI v9  
✅ Connection Management  
✅ Plugin Marketplace  
✅ Tab-based Multi-Instance System  
✅ Settings & Theme Management  
✅ HelloWorld Sample Plugin (Backend + UI)  
✅ Module Federation Configuration  
✅ Host SDK for Plugin Developers  

## 📄 License

See [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the Dataverse community**
