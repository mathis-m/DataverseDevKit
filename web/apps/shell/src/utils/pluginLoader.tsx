import React from 'react';

export interface RemotePlugin {
  remoteEntry: string;
  scope: string;
  module: string;
}

// Cache for loaded scripts
const scriptCache = new Map<string, Promise<void>>();

// Initialize global federation shared scope if not already set
const initializeGlobalSharedScope = async () => {
  if (!(globalThis as any).__federation_shared__) {
    const React = await import('react');
    const ReactDOM = await import('react-dom');
    const FluentUI = await import('@fluentui/react-components');
    const FluentUIIcons = await import('@fluentui/react-icons');

    (globalThis as any).__federation_shared__ = {
      default: {
        react: {
          '18.3.1': {
            get: () => Promise.resolve(() => React),
            loaded: 1,
            from: 'shell',
            version: '18.3.1',
            scope: ['default'],
            useIn: [],
            shareConfig: {
              singleton: true,
              requiredVersion: '^18.3.1',
            },
          },
        },
        'react-dom': {
          '18.3.1': {
            get: () => Promise.resolve(() => ReactDOM),
            loaded: 1,
            from: 'shell',
            version: '18.3.1',
            scope: ['default'],
            useIn: [],
            shareConfig: {
              singleton: true,
              requiredVersion: '^18.3.1',
            },
          },
        },
        '@fluentui/react-components': {
          '9.54.21': {
            get: () => Promise.resolve(() => FluentUI),
            loaded: 1,
            from: 'shell',
            version: '9.54.21',
            scope: ['default'],
            useIn: [],
            shareConfig: {
              singleton: true,
            },
          },
        },
        '@fluentui/react-icons': {
          '2.0.277': {
            get: () => Promise.resolve(() => FluentUIIcons),
            loaded: 1,
            from: 'shell',
            version: '2.0.277',
            scope: ['default'],
            useIn: [],
            shareConfig: {
              singleton: true,
            },
          },
        },
      },
    };
    console.log('[PluginLoader] Initialized global federation shared scope');
  }
};

// Module Federation runtime helper
export const loadRemoteModule = async (
  remoteEntry: string,
  scope: string,
  module: string
): Promise<React.ComponentType<any>> => {
  console.log('[PluginLoader] Loading remote module:', { remoteEntry, scope, module });

  // Initialize global shared scope first
  await initializeGlobalSharedScope();

  // Load the remote entry script
  if (!scriptCache.has(remoteEntry)) {
    const promise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = remoteEntry;
      script.type = 'module';
      script.onload = () => {
        console.log('[PluginLoader] Script loaded successfully:', remoteEntry);
        resolve();
      };
      script.onerror = (error) => {
        console.error('[PluginLoader] Script load error:', error);
        reject(new Error(`Failed to load remote entry: ${remoteEntry}`));
      };
      document.head.appendChild(script);
    });
    scriptCache.set(remoteEntry, promise);
  }

  await scriptCache.get(remoteEntry);

  // For ES module remotes, we need to import the module instead of accessing window[scope]
  let container = (window as any)[scope];
  
  if (!container) {
    // If not on window, try dynamic import (for ES module remotes from Vite)
    console.log('[PluginLoader] Container not on window, trying dynamic import');
    try {
      const remoteModule = await import(/* @vite-ignore */ remoteEntry);
      console.log('[PluginLoader] Remote module imported:', remoteModule);
      
      // Vite plugin federation exports get, init functions
      if (remoteModule.get && remoteModule.init) {
        container = remoteModule;
        // Cache it on window for subsequent loads
        (window as any)[scope] = container;
      } else {
        console.error('[PluginLoader] Remote module does not have get/init exports');
        throw new Error(`Remote module ${remoteEntry} missing get/init exports`);
      }
    } catch (importError) {
      console.error('[PluginLoader] Failed to import remote:', importError);
      console.error('[PluginLoader] Container not found:', scope, 'Available:', Object.keys(window));
      throw new Error(`Remote container ${scope} not found`);
    }
  }

  console.log('[PluginLoader] Container found:', scope);

  // Initialize with global federation shared scope
  const globalShared = (globalThis as any).__federation_shared__;
  console.log('[PluginLoader] Initializing with global shared scope:', globalShared);
  await container.init?.(globalShared.default);

  console.log('[PluginLoader] Container initialized');

  // Get the module factory
  console.log('[PluginLoader] Getting module:', module);
  const factory = await container.get(module);
  const Module = factory();

  console.log('[PluginLoader] Module loaded successfully');
  return Module.default || Module;
};

export interface PluginLoaderProps {
  remoteEntry: string;
  scope: string;
  module: string;
  instanceId: string;
  connectionId?: string | null;
  onLoad?: () => void;
  onError?: (error: Error) => void;
}

export const PluginLoader: React.FC<PluginLoaderProps> = ({
  remoteEntry,
  scope,
  module,
  instanceId,
  connectionId,
  onLoad,
  onError,
}) => {
  const [Component, setComponent] = React.useState<React.ComponentType<any> | null>(null);
  const [error, setError] = React.useState<Error | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const LoadedComponent = await loadRemoteModule(remoteEntry, scope, module);
        if (mounted) {
          setComponent(() => LoadedComponent);
          onLoad?.();
        }
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Unknown error');
        if (mounted) {
          setError(error);
          onError?.(error);
        }
      }
    };

    load();

    return () => {
      mounted = false;
    };
  }, [remoteEntry, scope, module, onLoad, onError]);

  if (error) {
    return (
      <div style={{ padding: '20px', color: 'red' }}>
        <h3>Failed to load plugin</h3>
        <p>{error.message}</p>
      </div>
    );
  }

  if (!Component) {
    return (
      <div style={{ padding: '20px' }}>
        <p>Loading plugin...</p>
      </div>
    );
  }

  return <Component instanceId={instanceId} connectionId={connectionId} />;
};

// Webpack share scopes type (for Module Federation)
declare global {
  var __webpack_share_scopes__: any;
}
