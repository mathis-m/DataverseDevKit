# Implementation Plan: Authentication & Connection Sharing Refactor

## Overview

This plan outlines the changes needed to:
1. Share authenticated sessions between the MAUI host and plugin sandboxes securely
2. Enable interactive OAuth authentication flow initiated from the frontend
3. Implement token management with automatic refresh and persistence
4. Remove the unused permission model from plugin manifests
5. Remove the DataverseService (no longer needed)

## Architecture Design

### Current State
```
┌─────────────────┐     gRPC/UDS      ┌─────────────────────────┐
│   MAUI Host     │ ◄───────────────► │   Plugin Sandbox        │
│                 │                   │   (PluginHost process)  │
│ - ConnectionSvc │                   │                         │
│ - AuthService   │                   │ - MockServiceClient     │
│ - DataverseSvc  │                   │   Factory (hardcoded)   │
└─────────────────┘                   └─────────────────────────┘
```

### Target State
```
┌─────────────────────────┐    gRPC/UDS     ┌─────────────────────────┐
│      MAUI Host          │ ◄─────────────► │   Plugin Sandbox        │
│                         │                 │   (PluginHost process)  │
│ ┌─────────────────────┐ │                 │                         │
│ │    AuthService      │ │ Token Request   │ ┌─────────────────────┐ │
│ │  (MSAL Interactive) │ │ ◄───────────────│ │TokenProxyClient     │ │
│ └──────────┬──────────┘ │                 │ │ Factory             │ │
│            │            │                 │ └─────────┬───────────┘ │
│ ┌──────────▼──────────┐ │                 │           │             │
│ │  TokenCacheService  │ │                 │ ┌─────────▼───────────┐ │
│ │  (Encrypted on disk)│ │                 │ │  ServiceClient      │ │
│ └──────────┬──────────┘ │                 │ │  (Real Dataverse)   │ │
│            │            │                 │ └─────────────────────┘ │
│ ┌──────────▼──────────┐ │                 │                         │
│ │ TokenProvider       │ │                 │                         │
│ │ (Background Refresh)│ │                 │                         │
│ └─────────────────────┘ │                 │                         │
└─────────────────────────┘                 └─────────────────────────┘
```

## Security Model

### Token Handling
- **Tokens NEVER passed via command-line arguments** (visible in process lists)
- **Tokens NEVER stored in socket file names** (filesystem exposure)
- **Tokens transmitted only over Unix Domain Socket** (local-only, no network)
- **Token cache encrypted at rest** using DPAPI (Windows) / Keychain (macOS)
- **Tokens are short-lived** with automatic refresh

### Token Proxy Pattern
Instead of passing tokens to plugins, plugins request tokens on-demand from the host via gRPC:
1. Plugin calls `ServiceClient(uri, tokenProvider, ...)` 
2. `tokenProvider` is a delegate that calls back to host via gRPC
3. Host returns access token (already authenticated)
4. Token is used for single request, not stored in plugin

---

## Phase 1: Cleanup (Remove Unused Features)

### 1.1 Remove Permission Model from Plugin Manifests

**Files to modify:**
- `tools/schema/plugin.manifest.schema.json` - Remove `permissions` property
- `src/plugins/sample-plugin/plugin.manifest.json` - Remove `permissions` block
- `src/plugins/solution-layer-analyzer/plugin.manifest.json` - Remove `permissions` block
- `src/dotnet/Host/Services/PluginHostManager.cs` - Remove any permission handling (if any)

**Rationale:** The permission model was never implemented and adds complexity. Plugins are already isolated by process.

### 1.2 Remove DataverseService

**Files to delete:**
- `src/dotnet/Host/Services/DataverseService.cs`

**Files to modify:**
- `src/dotnet/Host/Bridge/JsonRpcBridge.cs`:
  - Remove `_dataverseService` field
  - Remove `HandleDataverseMethodAsync` method
  - Remove `"dataverse"` case from namespace switch
- `src/dotnet/Host/MauiProgram.cs` - Remove `DataverseService` registration (if registered)
- `web/packages/host-sdk/src/HostBridge.ts` - Remove `query()` and `execute()` methods
- `web/packages/host-sdk/src/types.ts` - Remove `QueryResult` and `ExecuteResult` types

**Rationale:** Direct Dataverse access from frontend is not needed. Plugins handle Dataverse operations through ServiceClient.

---

## Phase 2: Token Cache Service

### 2.1 Create TokenCacheService

**New file:** `src/dotnet/Host/Services/TokenCacheService.cs`

```csharp
/// <summary>
/// Manages encrypted token cache persistence for MSAL.
/// Uses platform-specific encryption (DPAPI on Windows, Keychain on macOS).
/// </summary>
public class TokenCacheService
{
    // Token cache stored encrypted at: 
    // Windows: %LOCALAPPDATA%/DataverseDevKit/tokencache.bin
    // macOS: ~/Library/Application Support/DataverseDevKit/tokencache.bin
    
    Task<byte[]?> LoadCacheAsync();
    Task SaveCacheAsync(byte[] cacheData);
    Task ClearCacheAsync();
    
    // MSAL integration helpers
    void RegisterCache(ITokenCache tokenCache);
}
```

**Implementation details:**
- Use `System.Security.Cryptography.ProtectedData` on Windows (DPAPI)
- Use `Security.SecKeychain` via P/Invoke on macOS (or use MAUI SecureStorage)
- Implement MSAL `ITokenCache` callbacks for automatic persistence

### 2.2 Update Connection Model

**Modify:** `src/dotnet/Host/Services/ConnectionService.cs`

```csharp
public record Connection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }  // e.g., https://org.crm.dynamics.com
    public bool IsActive { get; set; }
    
    // New: Authentication state (not persisted - derived from token cache)
    public bool IsAuthenticated { get; set; }
    public string? AuthenticatedUser { get; set; }
}
```

---

## Phase 3: Token Provider Service

### 3.1 Create TokenProviderService

**New file:** `src/dotnet/Host/Services/TokenProviderService.cs`

```csharp
/// <summary>
/// Background service that manages access tokens for all connections.
/// Provides tokens on-demand with automatic refresh.
/// </summary>
public class TokenProviderService : IDisposable
{
    private readonly IConfidentialClientApplication _msalClient;
    private readonly TokenCacheService _tokenCache;
    private readonly ConnectionService _connectionService;
    
    /// <summary>
    /// Gets an access token for the specified connection, refreshing if needed.
    /// This is the method plugins will call via gRPC.
    /// </summary>
    Task<string> GetAccessTokenAsync(string connectionId, CancellationToken ct);
    
    /// <summary>
    /// Triggers interactive login for a connection.
    /// Opens system browser for OAuth flow.
    /// </summary>
    Task<AuthResult> LoginInteractiveAsync(string connectionId, CancellationToken ct);
    
    /// <summary>
    /// Signs out from a connection, clearing cached tokens.
    /// </summary>
    Task LogoutAsync(string connectionId, CancellationToken ct);
    
    /// <summary>
    /// Checks if a connection has valid (or refreshable) tokens.
    /// </summary>
    Task<bool> HasValidTokenAsync(string connectionId, CancellationToken ct);
}
```

**MSAL Configuration:**
```csharp
var options = new PublicClientApplicationOptions
{
    ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d", // Dataverse SDK app ID
    RedirectUri = "http://localhost", // For interactive browser flow
    TenantId = "common" // Multi-tenant
};
```

### 3.2 Update AuthService

**Modify:** `src/dotnet/Host/Services/AuthService.cs`

```csharp
/// <summary>
/// Manages authentication state and provides auth operations to the frontend.
/// Delegates actual token management to TokenProviderService.
/// </summary>
public class AuthService
{
    private readonly TokenProviderService _tokenProvider;

    /// <summary>
    /// Initiates interactive OAuth login for a connection.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string connectionId)
    {
        return await _tokenProvider.LoginInteractiveAsync(connectionId, CancellationToken.None);
    }
    
    /// <summary>
    /// Gets auth status including token validity check.
    /// </summary>
    public async Task<AuthStatus> GetStatusAsync(string? connectionId = null)
    {
        var hasToken = await _tokenProvider.HasValidTokenAsync(connectionId, ...);
        return new AuthStatus { IsAuthenticated = hasToken, ... };
    }
}
```

---

## Phase 4: gRPC Protocol for Token Sharing

### 4.1 Update Proto Definition

**Modify:** `src/dotnet/Contracts/pluginhost.proto`

```protobuf
service PluginHostService {
  // ... existing methods ...
  
  // Request access token from host (called by plugin's TokenProxy)
  rpc GetAccessToken (GetAccessTokenRequest) returns (GetAccessTokenResponse);
  
  // Subscribe to connection state changes
  rpc SubscribeConnectionState (ConnectionStateRequest) returns (stream ConnectionStateEvent);
}

message GetAccessTokenRequest {
  string connection_id = 1;  // Empty = active connection
  string resource = 2;       // Target resource (e.g., org URL)
}

message GetAccessTokenResponse {
  bool success = 1;
  string access_token = 2;   // The OAuth access token
  int64 expires_at = 3;      // Unix timestamp when token expires
  string error_message = 4;
}

message ConnectionStateRequest {
}

message ConnectionStateEvent {
  string connection_id = 1;
  string connection_name = 2;
  string connection_url = 3;
  bool is_authenticated = 4;
  bool is_active = 5;
  string user_name = 6;
}
```

### 4.2 Update Initialize Message

**Modify:** Proto `InitializeRequest`:
```protobuf
message InitializeRequest {
  string plugin_id = 1;
  string storage_path = 2;
  map<string, string> config = 3;
  
  // New: Initial connection context
  string active_connection_id = 4;
  string active_connection_url = 5;
}
```

---

## Phase 5: Plugin Runtime Token Proxy

### 5.1 Create TokenProxyServiceClientFactory

**New file:** `src/dotnet/PluginRuntime/Services/TokenProxyServiceClientFactory.cs`

```csharp
/// <summary>
/// IServiceClientFactory implementation that proxies token requests to the host.
/// Uses the ServiceClient overload that accepts a token provider delegate.
/// </summary>
public class TokenProxyServiceClientFactory : IServiceClientFactory
{
    private readonly PluginHostService.PluginHostServiceClient _hostClient;
    private readonly string _activeConnectionUrl;
    private readonly ILogger _logger;

    public ServiceClient GetServiceClient(string? connectionId = null)
    {
        var serviceUri = new Uri(_activeConnectionUrl);
        
        // Create ServiceClient with token provider delegate
        return new ServiceClient(
            serviceUri,
            async (resourceUrl) => await GetTokenFromHostAsync(connectionId, resourceUrl),
            useUniqueInstance: true,
            logger: _logger);
    }

    private async Task<string> GetTokenFromHostAsync(string? connectionId, string resource)
    {
        var request = new GetAccessTokenRequest
        {
            ConnectionId = connectionId ?? string.Empty,
            Resource = resource
        };
        
        var response = await _hostClient.GetAccessTokenAsync(request);
        
        if (!response.Success)
        {
            throw new InvalidOperationException($"Failed to get access token: {response.ErrorMessage}");
        }
        
        return response.AccessToken;
    }
}
```

### 5.2 Update Plugin Host Startup

**Modify:** `src/dotnet/PluginRuntime/Program.cs`

- Pass gRPC client to TokenProxyServiceClientFactory
- Store connection URL from Initialize message
- Register TokenProxyServiceClientFactory instead of MockServiceClientFactory

### 5.3 Delete MockServiceClientFactory

**Delete:** `src/dotnet/PluginRuntime/Services/MockServiceClientFactory.cs`

---

## Phase 6: Host-Side Token Request Handler

### 6.1 Add Token Request Handler to gRPC Service

**Modify:** `src/dotnet/Host/Services/PluginHostManager.cs`

Need to create a reverse channel - Host needs to handle token requests from plugins.

**Option A: Bidirectional gRPC**
- Complex, requires host to also be a gRPC server

**Option B (Recommended): Extend existing worker communication**
- Plugin worker already has a gRPC server (PluginHostService)
- Add a callback mechanism where plugin can request tokens

**Implementation:**
Create a gRPC client in the plugin that calls back to a lightweight HTTP endpoint in the PluginHostManager for token requests.

Actually, simpler approach:

### 6.2 Create Token Request Callback Server

The plugin sandbox needs to request tokens from the host. Options:

**Recommended: Named Pipe/UDS reverse channel**

Each plugin instance gets:
1. Forward channel: Host → Plugin (existing gRPC over UDS)
2. Reverse channel: Plugin → Host (new UDS for token requests)

**Implementation:**

**Modify:** `src/dotnet/Host/Services/PluginHostManager.cs`
- Start a minimal gRPC server for each plugin on a second UDS
- Pass the token-request socket path to the plugin via Initialize

**New file:** `src/dotnet/Host/Services/TokenProviderGrpcService.cs`
```csharp
/// <summary>
/// Lightweight gRPC service that plugins call to get access tokens.
/// Each plugin instance has its own isolated connection to this service.
/// </summary>
public class TokenProviderGrpcService
{
    private readonly TokenProviderService _tokenProvider;
    
    public Task<GetAccessTokenResponse> GetAccessToken(GetAccessTokenRequest request, ServerCallContext context)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(request.ConnectionId, context.CancellationToken);
        return new GetAccessTokenResponse { Success = true, AccessToken = token };
    }
}
```

---

## Phase 7: Frontend Integration

### 7.1 Update Auth Flow in Frontend

**Modify:** `web/packages/host-sdk/src/HostBridge.ts`

```typescript
// Enhanced auth methods
async loginInteractive(connectionId: string): Promise<AuthResult> {
  return this.sendRequest<AuthResult>('auth.loginInteractive', { connectionId });
}

async getAuthStatus(connectionId?: string): Promise<AuthStatus> {
  return this.sendRequest<AuthStatus>('auth.getStatus', { connectionId });
}

// Remove direct Dataverse methods (query, execute) - plugins handle this
```

### 7.2 Update Connection Manager UI

**Modify:** `web/apps/shell/src/components/ConnectionManager.tsx`

- Add "Login" button per connection
- Show authentication status
- Handle login flow (host opens browser, returns result)

### 7.3 Update AddConnectionDialog

**Modify:** `web/apps/shell/src/components/AddConnectionDialog.tsx`

- Add field for Organization URL
- Optionally trigger auth immediately after adding

---

## Phase 8: Testing & Validation

### 8.1 Unit Tests

- TokenCacheService encryption/decryption
- TokenProviderService token refresh logic
- TokenProxyServiceClientFactory callback

### 8.2 Integration Tests

- Full auth flow from frontend → host → MSAL
- Plugin requesting token via proxy
- Token refresh during long operations

### 8.3 Security Review

- Verify no tokens in process arguments
- Verify no tokens in file names
- Verify encrypted cache on disk
- Verify UDS-only token transmission

---

## File Change Summary

### New Files
| File | Description |
|------|-------------|
| `src/dotnet/Host/Services/TokenCacheService.cs` | Encrypted token persistence |
| `src/dotnet/Host/Services/TokenProviderService.cs` | MSAL integration & token management |
| `src/dotnet/Host/Services/TokenProviderGrpcService.cs` | Handle plugin token requests |
| `src/dotnet/PluginRuntime/Services/TokenProxyServiceClientFactory.cs` | Plugin-side token proxy |

### Modified Files
| File | Changes |
|------|---------|
| `src/dotnet/Contracts/pluginhost.proto` | Add token request messages |
| `src/dotnet/Host/Services/AuthService.cs` | Integrate with TokenProviderService |
| `src/dotnet/Host/Services/ConnectionService.cs` | Add auth state to Connection |
| `src/dotnet/Host/Services/PluginHostManager.cs` | Start token callback server |
| `src/dotnet/Host/Bridge/JsonRpcBridge.cs` | Remove DataverseService |
| `src/dotnet/PluginRuntime/Program.cs` | Use TokenProxyServiceClientFactory |
| `tools/schema/plugin.manifest.schema.json` | Remove permissions |
| `src/plugins/*/plugin.manifest.json` | Remove permissions blocks |
| `web/packages/host-sdk/src/HostBridge.ts` | Update auth methods |
| `web/packages/host-sdk/src/types.ts` | Update types |
| `web/apps/shell/src/components/*` | Auth UI updates |

### Deleted Files
| File | Reason |
|------|--------|
| `src/dotnet/Host/Services/DataverseService.cs` | Not needed |
| `src/dotnet/PluginRuntime/Services/MockServiceClientFactory.cs` | Replaced by TokenProxy |

---

## Implementation Order

1. **Phase 1**: Cleanup (permissions, DataverseService) - Low risk
2. **Phase 2**: TokenCacheService - Foundation for persistence
3. **Phase 3**: TokenProviderService - Core auth logic
4. **Phase 4**: Proto updates - Define contract
5. **Phase 5**: TokenProxyServiceClientFactory - Plugin-side implementation
6. **Phase 6**: Host token callback - Complete the loop
7. **Phase 7**: Frontend - User-facing integration
8. **Phase 8**: Testing - Validation

---

## Dependencies

### NuGet Packages (Host)
- `Microsoft.Identity.Client` (MSAL) - Already likely present
- `Microsoft.Identity.Client.Extensions.Msal` - For token cache helpers

### NuGet Packages (PluginRuntime)
- Already has `Microsoft.PowerPlatform.Dataverse.Client`

---

## Notes

- The ServiceClient constructor with token provider delegate handles all the complexity of token refresh, retries, etc.
- MSAL handles interactive browser auth, token refresh, and cache management
- The token proxy pattern ensures plugins never store tokens - they request on-demand
- UDS communication ensures tokens never traverse the network
