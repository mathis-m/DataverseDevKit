// JSON-RPC types
export interface JsonRpcRequest {
  jsonrpc: '2.0';
  id: string | number;
  method: string;
  params?: unknown;
}

export interface JsonRpcResponse<T = unknown> {
  jsonrpc: '2.0';
  id: string | number;
  result?: T;
  error?: JsonRpcError;
}

export interface JsonRpcError {
  code: number;
  message: string;
  data?: unknown;
}

// Connection types
export interface ConnectionInfo {
  id: string;
  name: string;
  url: string;
  isActive: boolean;
  isAuthenticated: boolean;
  authenticatedUser?: string;
}

export interface AddConnectionParams {
  name: string;
  url: string;
  authType: 'OAuth' | 'ClientSecret';
  clientId?: string;
  clientSecret?: string;
  tenantId?: string;
}

// Plugin types
export interface PluginMetadata {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;
  category: string;
  company?: string;
  icon?: string;
  commands: PluginCommand[];
  uiEntry: string;
  uiModule?: string;
  uiScope?: string;
  isRunning: boolean;
}

export interface PluginCommand {
  name: string;
  label: string;
  description: string;
  payloadSchema?: Record<string, unknown>;
}

export interface PluginInstance {
  instanceId: string;
  pluginId: string;
  connectionId: string | null;
  state: 'loading' | 'ready' | 'error';
  title: string;
}

export interface ExecuteCommandParams {
  pluginId: string;
  command: string;
  payload?: unknown;
}

export interface ExecuteCommandResult {
  success: boolean;
  result?: unknown;
  error?: string;
}

// Event types
export interface PluginEvent {
  type: string;
  pluginId: string;
  payload: unknown;
  timestamp: string;
  metadata?: Record<string, unknown>;
}

/** Payload for session:expired event */
export interface SessionExpiredPayload {
  connectionId: string;
  connectionName?: string;
  message: string;
}

export type EventCallback = (event: PluginEvent) => void;

// Settings types
export interface UserSettings {
  theme: 'light' | 'dark' | 'system';
  [key: string]: unknown;
}

// Authentication types
export interface AuthResult {
  success: boolean;
  user?: string;
  expiresOn?: string;
  error?: string;
}

export interface AuthStatus {
  isAuthenticated: boolean;
  user?: string;
}
