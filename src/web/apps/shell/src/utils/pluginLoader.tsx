import React from "react";
import * as ReactIs from "react-is";

import { registerRemotes, loadRemote } from "@module-federation/enhanced/runtime";

export interface RemotePlugin {
  remoteEntry: string;
  scope: string;
  module: string;
}

function assertIsReactComponent(
  value: unknown,
  key?: string
): asserts value is React.ComponentType<any> {
  if (
    typeof value === 'function' ||
    ReactIs.isValidElementType(value)
  ) {
    return;
  }

  throw new Error(
    `Remote${key ? ` "${key}"` : ''} did not resolve to a React component`
  );
}

// Module Federation runtime helper using official virtual:__federation__ API
export const loadRemoteModule = async (
  remoteEntry: string,
  scope: string,
  module: string,
): Promise<React.ComponentType<any>> => {
  console.log("[PluginLoader] Loading remote module:", {
    remoteEntry,
    scope,
    module,
  });

  try {
    registerRemotes([
      {
        name: scope,
        entry: remoteEntry,
        type: "esm"
      },
    ]);

    console.log("[PluginLoader] Remote registered:", scope);

    // Get the remote module using the federation API
    const remoteModule = await loadRemote<unknown>(`${scope}/${module}`);
    console.log("[PluginLoader] Remote module fetched");

    // Unwrap to get the actual component
    const Component = typeof remoteModule === "object" && remoteModule !== null && "default" in remoteModule ? remoteModule.default : remoteModule;
    assertIsReactComponent(Component);
    console.log("[PluginLoader] Module loaded successfully");

    return Component;
  } catch (error) {
    console.error("[PluginLoader] Failed to load remote module:", error);
    throw error;
  }
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
  const [Component, setComponent] =
    React.useState<React.ComponentType<any> | null>(null);
  const [error, setError] = React.useState<Error | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const LoadedComponent = await loadRemoteModule(
          remoteEntry,
          scope,
          module,
        );
        if (mounted) {
          setComponent(() => LoadedComponent);
          onLoad?.();
        }
      } catch (err) {
        const error = err instanceof Error ? err : new Error("Unknown error");
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
      <div style={{ padding: "20px", color: "red" }}>
        <h3>Failed to load plugin</h3>
        <p>{error.message}</p>
      </div>
    );
  }

  if (!Component) {
    return (
      <div style={{ padding: "20px" }}>
        <p>Loading plugin...</p>
      </div>
    );
  }

  return <Component instanceId={instanceId} connectionId={connectionId} />;
};
