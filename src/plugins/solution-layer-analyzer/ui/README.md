# Solution Layer Analyzer - UI

React TypeScript UI for the Solution Layer Analyzer plugin.

## Features

- **Index Tab**: Build index of solutions and components
- **Query Tab**: View and filter indexed components with layer sequences
- **Diff Tab**: Compare component payloads between solutions using Monaco diff editor

## Development

```bash
npm install
npm run dev
```

The UI will be available at http://localhost:5174

## Build

```bash
npm run build
```

Outputs to `dist/` directory as a Module Federation remote.

## Architecture

- **Framework**: React 18 + TypeScript
- **UI Library**: Fluent UI v9
- **Diff Editor**: Monaco Editor
- **Communication**: @ddk/host-sdk for plugin commands
- **Module Federation**: Vite plugin for micro-frontend

## Components

- `Plugin.tsx` - Main component with tabs for Index, Query, and Diff
- `dev.tsx` - Development entry point

## Integration

The UI is loaded as a Module Federation remote by the host application. Commands are executed via:

```typescript
import { hostBridge } from '@ddk/host-sdk';

const result = await hostBridge.invokePluginCommand(
  'com.ddk.solutionlayeranalyzer',
  'index',
  JSON.stringify({ sourceSolutions: ['Core'], targetSolutions: ['ProjectA'] })
);
```
