# Plugins

This folder contains plugins that ship with the DataverseDevKit.

## Structure

Each plugin follows this structure:

```
{plugin-name}/
├── plugin.manifest.json   # Plugin manifest
├── README.md              # Plugin documentation
├── .gitignore             # Ignore build outputs
├── src/                   # Source code
│   ├── {Plugin}.csproj
│   └── {Plugin}.cs
└── dist/                  # Build output (git-ignored)
    ├── plugin.manifest.json
    └── backend/
        └── {Plugin}.dll
```

## Building Plugins

### Build all plugins

```powershell
dotnet build Plugins.slnx
```

### Build a single plugin

```powershell
cd {plugin-name}/src
dotnet build
```

## Build Output

Each plugin builds to its own `dist/` folder with the structure expected by the Host application:

```
dist/
├── plugin.manifest.json
├── backend/
│   └── {Plugin}.dll (and dependencies)
└── ui/              # (if plugin has UI)
    └── remoteEntry.js
```

## Host Integration

The Host application's post-build step copies all `dist/` folders to `wwwroot/plugins/` for deployment.

## Creating a New Plugin

1. Copy the `sample-plugin` folder
2. Rename the folder to your plugin name
3. Update `plugin.manifest.json`
4. Rename and update the `.csproj` file
5. Implement your plugin logic
6. Build and test
