using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Simple service client factory for CLI usage
/// </summary>
internal class CliServiceClientFactory : IServiceClientFactory
{
    private readonly string _environmentUrl;
    private readonly string? _connectionString;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _tenantId;

    public CliServiceClientFactory(
        string environmentUrl,
        string? connectionString = null,
        string? clientId = null,
        string? clientSecret = null,
        string? tenantId = null)
    {
        _environmentUrl = environmentUrl;
        _connectionString = connectionString;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
    }

    public ServiceClient GetServiceClient(string? connectionId)
    {
        // Build connection string based on provided auth method
        string connString;

        if (!string.IsNullOrEmpty(_connectionString))
        {
            // Use provided connection string
            connString = _connectionString;
        }
        else if (!string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSecret) && !string.IsNullOrEmpty(_tenantId))
        {
            // Service principal authentication
            connString = $"AuthType=ClientSecret;Url={_environmentUrl};ClientId={_clientId};ClientSecret={_clientSecret};TenantId={_tenantId}";
        }
        else
        {
            // Interactive authentication
            connString = $"AuthType=OAuth;Url={_environmentUrl};LoginPrompt=Auto;RedirectUri=http://localhost;RequireNewInstance=True";
        }

        return new ServiceClient(connString);
    }
}
