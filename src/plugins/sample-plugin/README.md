# Sample Plugin

A reference implementation demonstrating the DDK plugin architecture with both backend (.NET) and frontend (React) components.

## Structure

```
sample-plugin/
├── plugin.manifest.json    # Plugin manifest (deployed to dist/)
├── README.md               # This file
├── src/                    # Backend source code (.NET)
│   ├── Ddk.SamplePlugin.csproj
│   └── SamplePlugin.cs
├── ui/                     # Frontend source code (React/TypeScript)
│   ├── package.json
│   ├── vite.config.ts
│   ├── index.html
│   └── src/
│       ├── dev.tsx         # Development entry
│       └── Plugin.tsx      # Main plugin component
└── dist/                   # Build output (git-ignored)
    ├── plugin.manifest.json
    ├── backend/
    │   └── Ddk.SamplePlugin.dll
    └── frontend/
        └── remoteEntry.js
```

## Building

### Backend (.NET)

From the `src/` directory:

```powershell
dotnet build
```

### Frontend (React)

From the `ui/` directory:

```powershell
# Install dependencies (first time only, from web/ workspace root)
cd ../../../../web
pnpm install

# Build the UI
cd ../src/plugins/sample-plugin/ui
pnpm build
```

Or for development with hot reloading:

```powershell
pnpm dev
```

The dev server runs at `http://localhost:5175`.

## Commands

| Command   | Description                              |
|-----------|------------------------------------------|
| `ping`    | Returns a pong response with timestamp   |
| `echo`    | Returns the same message that was sent   |
| `getInfo` | Returns information about this plugin    |

## Frontend Development

The UI uses `@ddk/host-sdk` to communicate with the backend:

```typescript
import { hostBridge } from '@ddk/host-sdk';

// Call a plugin command
const result = await hostBridge.invokePluginCommand(
  'com.ddk.sample',  // Plugin ID
  'echo',            // Command name  
  JSON.stringify({ message: 'Hello!' }) // Payload
);

// Result is already an object (no JSON.parse needed)
console.log(result);
```

### Workspace Integration

The UI can access `@ddk/host-sdk` via pnpm workspace because `web/pnpm-workspace.yaml` includes:

```yaml
packages:
  - '../src/plugins/*/ui'
```

This allows `workspace:*` protocol in package.json without publishing the SDK.

## Creating Your Own Plugin

### Backend

1. Copy `src/` as a template
2. Update the `.csproj` filename and namespaces
3. Implement your commands in the plugin class

### Frontend

1. Copy `ui/` as a template  
2. Update `package.json` name field
3. Implement your UI in `Plugin.tsx`
4. Update `vite.config.ts` federation name

### Manifest

Update `plugin.manifest.json` with:

- **id**: Unique plugin identifier (reverse-DNS notation)
- **backend.assembly**: Path to the DLL
- **backend.entryPoint**: Fully qualified plugin class name
- **ui.entry**: Path to production UI (`frontend/remoteEntry.js`)
- **ui.devEntry**: Dev server URL (`http://localhost:PORT/remoteEntry.js`)

## Plugin Lifecycle

1. Host discovers plugin via manifest
2. When invoked, host starts PluginHost process
3. PluginHost loads the assembly and creates plugin instance
4. Host calls `InitializeAsync` with context
5. Host can invoke commands via `ExecuteAsync`
6. UI is loaded via Module Federation using entry/devEntry URL
7. On shutdown, `DisposeAsync` is called
