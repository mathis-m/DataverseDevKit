import type {
  JsonRpcRequest,
  JsonRpcResponse,
  ConnectionInfo,
  AddConnectionParams,
  PluginMetadata,
  PluginCommand,
  EventCallback,
  PluginEvent,
  QueryResult,
  ExecuteResult,
} from './types';

export class HostBridge {
  private requestId = 0;
  private pendingRequests = new Map<number, {
    resolve: (value: unknown) => void;
    reject: (error: Error) => void;
  }>();
  private eventListeners = new Map<string, Set<EventCallback>>();

  constructor() {
    this.setupMessageListener();
  }

  private setupMessageListener(): void {
    // For MAUI HybridWebView
    if (typeof window !== 'undefined') {
      // Log what's available
      console.log('[HostBridge] Window APIs available:', {
        HybridWebView: !!(window as any).HybridWebView,
        SendRawMessage: !!(window as any).HybridWebView?.SendRawMessage,
        chromeWebview: !!(window as any).chrome?.webview,
      });

      // Expose handleResponse for MAUI to call back
      (window as any).__ddkBridge = {
        handleResponse: (data: string) => {
          this.handleMessage(data);
        }
      };

      console.log('[HostBridge] Registered __ddkBridge.handleResponse');
    }

    // For WebView2 (legacy)
    if (typeof window !== 'undefined' && (window as any).chrome?.webview) {
      (window as any).chrome.webview.addEventListener('message', (e: MessageEvent) => {
        this.handleMessage(e.data);
      });
    }
  }

  private handleMessage(data: string): void {
    try {
      console.log('[HostBridge] Received message:', data);
      const message = typeof data === 'string' ? JSON.parse(data) : data;

      // Check if it's an event
      if ('type' in message && 'pluginId' in message) {
        this.dispatchEvent(message as PluginEvent);
        return;
      }

      // Handle JSON-RPC response
      const response = message as JsonRpcResponse;
      const pending = this.pendingRequests.get(response.id as number);
      if (pending) {
        this.pendingRequests.delete(response.id as number);
        if (response.error) {
          pending.reject(new Error(response.error.message));
        } else {
          pending.resolve(response.result);
        }
      } else {
        console.warn('[HostBridge] No pending request for response id:', response.id);
      }
    } catch (error) {
      console.error('Failed to handle message:', error);
    }
  }

  private dispatchEvent(event: PluginEvent): void {
    const listeners = this.eventListeners.get(event.type);
    if (listeners) {
      listeners.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Event listener error:', error);
        }
      });
    }

    // Also dispatch to wildcard listeners
    const wildcardListeners = this.eventListeners.get('*');
    if (wildcardListeners) {
      wildcardListeners.forEach(callback => {
        try {
          callback(event);
        } catch (error) {
          console.error('Event listener error:', error);
        }
      });
    }
  }

  private async sendRequest<T>(method: string, params?: unknown): Promise<T> {
    const id = ++this.requestId;
    const request: JsonRpcRequest = {
      jsonrpc: '2.0',
      id,
      method,
      params,
    };

    return new Promise((resolve, reject) => {
      this.pendingRequests.set(id, { resolve: resolve as (value: unknown) => void, reject });

      const message = JSON.stringify(request);

      // Try MAUI HybridWebView first
      if (typeof window !== 'undefined' && (window as any).HybridWebView?.SendRawMessage) {
        console.log('[HostBridge] Sending via HybridWebView:', method, params);
        (window as any).HybridWebView.SendRawMessage(message);
      } 
      // Fall back to WebView2
      else if (typeof window !== 'undefined' && (window as any).chrome?.webview) {
        console.log('[HostBridge] Sending via chrome.webview:', method, params);
        (window as any).chrome.webview.postMessage(message);
      } 
      // Development mock
      else {
        console.warn('WebView not available, using mock response');
        setTimeout(() => {
          this.pendingRequests.delete(id);
          resolve({} as T);
        }, 100);
      }

      // Timeout after 30 seconds
      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id);
          reject(new Error('Request timeout'));
        }
      }, 30000);
    });
  }

  // Connection management
  async addConnection(params: AddConnectionParams): Promise<ConnectionInfo> {
    return this.sendRequest<ConnectionInfo>('connection.add', params);
  }

  async listConnections(): Promise<ConnectionInfo[]> {
    return this.sendRequest<ConnectionInfo[]>('connection.list');
  }

  async activateConnection(connectionId: string): Promise<void> {
    return this.sendRequest<void>('connection.setActive', { id: connectionId });
  }

  async removeConnection(connectionId: string): Promise<void> {
    return this.sendRequest<void>('connection.remove', { id: connectionId });
  }

  // Authentication
  async login(connectionId: string): Promise<void> {
    return this.sendRequest<void>('auth.login', { connectionId });
  }

  async logout(connectionId: string): Promise<void> {
    return this.sendRequest<void>('auth.logout', { connectionId });
  }

  // Plugin management
  async listPlugins(): Promise<PluginMetadata[]> {
    return this.sendRequest<PluginMetadata[]>('plugin.list');
  }

  async getPluginCommands(pluginId: string): Promise<PluginCommand[]> {
    return this.sendRequest<PluginCommand[]>('plugin.getCommands', { pluginId });
  }

  async invokePluginCommand(pluginId: string, command: string, payload?: string): Promise<string> {
    return this.sendRequest<string>('plugin.invoke', { pluginId, command, payload: payload ?? '' });
  }

  // Event subscription
  addEventListener(eventType: string, callback: EventCallback): () => void {
    if (!this.eventListeners.has(eventType)) {
      this.eventListeners.set(eventType, new Set());
    }
    this.eventListeners.get(eventType)!.add(callback);

    // Subscribe to backend events
    this.sendRequest('events.subscribe', { eventType }).catch(console.error);

    // Return unsubscribe function
    return () => {
      const listeners = this.eventListeners.get(eventType);
      if (listeners) {
        listeners.delete(callback);
        if (listeners.size === 0) {
          this.eventListeners.delete(eventType);
          this.sendRequest('events.unsubscribe', { eventType }).catch(console.error);
        }
      }
    };
  }

  // Dataverse operations
  async query(fetchXml: string): Promise<QueryResult> {
    return this.sendRequest<QueryResult>('dataverse.query', { fetchXml });
  }

  async execute(requestJson: string): Promise<ExecuteResult> {
    return this.sendRequest<ExecuteResult>('dataverse.execute', { requestJson });
  }

  // Storage
  async getStorage(pluginId: string, key: string): Promise<string | null> {
    return this.sendRequest<string | null>('storage.get', { pluginId, key });
  }

  async setStorage(pluginId: string, key: string, value: string): Promise<void> {
    return this.sendRequest<void>('storage.set', { pluginId, key, value });
  }
}

// Singleton instance
export const hostBridge = new HostBridge();
