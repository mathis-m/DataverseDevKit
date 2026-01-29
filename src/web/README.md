# Dataverse DevKit - Shell UI & Plugins

## Overview

The Dataverse DevKit Shell UI is a modern, tab-based desktop application built with:
- **FluentUI v9** for design system and components
- **React 18** with TypeScript for UI development
- **Vite** for fast development and building
- **Module Federation** for dynamic plugin loading
- **pnpm workspaces** for monorepo management (from repository root)
- **Zustand** for state management
- **@fluentui/react-context-selector** for optimized context
- **@dnd-kit/react** for drag-and-drop tab management

## Architecture

### Monorepo Structure

```
/ (repository root)
├── pnpm-workspace.yaml      # Root workspace config
├── package.json             # Root package with scripts
└── src/
    ├── web/                 # Core UI packages
    │   ├── packages/        # Shared packages
    │   │   ├── host-sdk/   # TypeScript SDK for plugin developers
    │   │   └── ui-components/  # Shared FluentUI components
    │   └── apps/
    │       └── shell/       # Main Shell application
    └── plugins/             # Plugin source code
        ├── sample-plugin/
        │   └── ui/         # Plugin UI
        └── solution-layer-analyzer/
            └── ui/         # Plugin UI
```

### Key Features

#### 1. **Connection Management**
- Add/remove Dataverse environment connections
- OAuth and Client Secret authentication
- Active connection switching
- Persisted connection history

#### 2. **Plugin Marketplace**
- Searchable plugin catalog
- Filter by category, company, author
- Plugin metadata display
- One-click plugin launch

#### 3. **Tab-Based Multi-Instance**
- Multiple plugin instances in tabs
- Drag-and-drop tab reordering
- Per-instance connection switching
- Tab state management

#### 4. **Settings & Theming**
- Light/Dark/System theme modes
- Sidebar collapse/expand
- Persisted user preferences
- FluentUI v9 theme integration

#### 5. **Module Federation Plugin System**
- Dynamic runtime plugin loading
- Shared dependencies (React, FluentUI)
- Isolated plugin scopes
- Hot module replacement in dev mode

## Getting Started

### Prerequisites

```powershell
# Node.js 20+ and pnpm
node --version  # Should be >= 20
pnpm --version  # Should be >= 9

# If pnpm not installed:
npm install -g pnpm@latest
```

### Installation

```powershell
# Navigate to repository root
cd /path/to/DataverseDevKit

# Install all dependencies (workspaces)
pnpm install
```

### Development

#### Run Shell in Dev Mode

```powershell
# From repository root
pnpm dev
```

This starts the Shell at `http://localhost:5173` with hot module replacement.

#### Run a Plugin in Dev Mode

```powershell
# From repository root
pnpm --filter @ddk/plugin-sla-ui dev
```

This starts the plugin with hot module replacement for development testing.

### Building for Production

```powershell
# Build all packages and apps (from repository root)
pnpm build

# Or build specific projects
pnpm build:shell
pnpm build:plugins
```

### Integration with MAUI

After building the web assets, copy them to the MAUI app:

```powershell
# From repository root
.\build-web.ps1
```

This script:
1. Builds the Shell app
2. Builds all plugins
3. Copies built assets to `src/dotnet/Host/wwwroot/`

## Developing Plugins

### Plugin Structure

```
plugin-name/
├── package.json           # NPM package config
├── vite.config.ts         # Vite + Module Federation config
├── tsconfig.json          # TypeScript config
├── src/
│   ├── Plugin.tsx         # Main plugin component (exported)
│   └── dev.tsx            # Dev mode preview
└── index.html             # Dev mode HTML
```

### Plugin Component Interface

```typescript
interface PluginProps {
  instanceId: string;        // Unique instance identifier
  connectionId?: string;     // Active Dataverse connection ID
}

const MyPlugin: React.FC<PluginProps> = ({ instanceId, connectionId }) => {
  // Use Host SDK
  const handleCommand = async () => {
    const result = await hostBridge.executeCommand({
      pluginId: 'com.example.myplugin',
      command: 'myCommand',
      payload: { /* ... */ }
    });
  };

  return <div>Plugin UI</div>;
};

export default MyPlugin;
```

### Using Host SDK

The `@ddk/host-sdk` package provides TypeScript APIs for:

#### Connection Management

```typescript
import { hostBridge } from '@ddk/host-sdk';

// List connections
const connections = await hostBridge.listConnections();

// Add connection
const conn = await hostBridge.addConnection({
  name: 'My Environment',
  url: 'https://myorg.crm.dynamics.com',
  authType: 'OAuth',
});
```

#### Execute Backend Commands

```typescript
const result = await hostBridge.executeCommand({
  pluginId: 'com.contoso.ddk.helloworld',
  command: 'echo',
  payload: { message: 'Hello!' }
});
```

#### Subscribe to Events

```typescript
const unsubscribe = hostBridge.addEventListener('heartbeat', (event) => {
  console.log('Event:', event.type, event.payload);
});

// Clean up
return () => unsubscribe();
```

#### Dataverse Operations

```typescript
// Query data
const result = await hostBridge.query({
  entityLogicalName: 'account',
  filter: "name eq 'Contoso'",
  select: ['accountid', 'name'],
  top: 10
});

// Create record
const id = await hostBridge.create('account', {
  name: 'New Account',
  revenue: 100000
});
```

### Module Federation Configuration

In `vite.config.ts`:

```typescript
import federation from '@originjs/vite-plugin-federation';

export default defineConfig({
  plugins: [
    react(),
    federation({
      name: 'myPlugin',
      filename: 'remoteEntry.js',
      exposes: {
        './Plugin': './src/Plugin.tsx',  // Export the component
      },
      shared: {
        react: { singleton: true },
        'react-dom': { singleton: true },
        '@fluentui/react-components': { singleton: true },
        '@ddk/host-sdk': { singleton: true },
      },
    }),
  ],
});
```

### Plugin Manifest

Create `plugin.manifest.json` in your plugin root:

```json
{
  "id": "com.example.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "description": "Plugin description",
  "author": "Your Name",
  "company": "Your Company",
  "category": "productivity",
  "icon": "🔧",
  "backend": {
    "assembly": "MyCompany.Ddk.MyPlugin.dll",
    "entryPoint": "MyCompany.Ddk.MyPlugin.MyPlugin"
  },
  "ui": {
    "type": "module-federation",
    "remoteEntry": "http://localhost:5174/assets/remoteEntry.js",
    "module": "./Plugin",
    "scope": "myPlugin"
  }
}
```

## State Management

### Stores (Zustand)

The Shell uses Zustand for reactive state management:

- **SettingsStore** - User preferences, theme, sidebar state
- **ConnectionStore** - Dataverse connections
- **PluginStore** - Available plugins and instances

Example usage in components:

```typescript
import { useSettingsStore } from '../stores/settings';

function MyComponent() {
  const theme = useSettingsStore(state => state.settings.theme);
  const updateSettings = useSettingsStore(state => state.updateSettings);

  return (
    <Button onClick={() => updateSettings({ theme: 'dark' })}>
      Dark Mode
    </Button>
  );
}
```

## Styling with FluentUI v9

### Using Tokens

```typescript
import { makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
  },
});
```

### Theme-Aware Components

FluentUI automatically handles theme switching. Components update when theme changes.

```typescript
import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';

<FluentProvider theme={isDark ? webDarkTheme : webLightTheme}>
  <App />
</FluentProvider>
```

## Drag and Drop

The Shell uses `@dnd-kit/react` for tab reordering:

```typescript
import { DndContext, closestCenter } from '@dnd-kit/core';
import { SortableContext, useSortable } from '@dnd-kit/sortable';

function TabPanel() {
  const handleDragEnd = (event) => {
    // Reorder tabs
  };

  return (
    <DndContext onDragEnd={handleDragEnd} collisionDetection={closestCenter}>
      <SortableContext items={tabIds}>
        {tabs.map(tab => <SortableTab key={tab.id} {...tab} />)}
      </SortableContext>
    </DndContext>
  );
}
```

## Troubleshooting

### Module Federation Issues

If plugins fail to load:
1. Ensure plugin is running in dev mode or built
2. Check `remoteEntry` URL in plugin manifest
3. Verify shared dependencies versions match
4. Check browser console for CORS errors

### Build Errors

```powershell
# Clean install
rm -r node_modules
rm pnpm-lock.yaml
pnpm install

# Clean build
pnpm clean  # if script exists
pnpm build
```

### TypeScript Errors

```powershell
# Check types
pnpm type-check

# Build with verbose output
pnpm build --mode development
```

## Performance Tips

1. **Code Splitting** - Use dynamic imports for large components
2. **Memoization** - Use `React.memo` for expensive components
3. **Zustand Selectors** - Select only needed state slices
4. **FluentUI Tree Shaking** - Import only needed components

## Security Considerations

- Plugins run in the same JavaScript context as Shell
- Use `@ddk/host-sdk` API instead of direct native calls
- Backend validates all plugin commands
- Connection credentials never exposed to frontend
- OAuth tokens managed by MAUI host

## Contributing

When creating new plugins:
1. Follow the plugin template structure
2. Use TypeScript with strict mode
3. Include type definitions
4. Write plugin manifest
5. Test in both dev and production builds
6. Document plugin commands and features

## Resources

- [FluentUI v9 Documentation](https://react.fluentui.dev/)
- [Module Federation Guide](https://module-federation.io/)
- [Vite Documentation](https://vitejs.dev/)
- [pnpm Workspaces](https://pnpm.io/workspaces)
- [Zustand Documentation](https://zustand-demo.pmnd.rs/)
