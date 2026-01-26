using System.Text.Json;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Mock implementation of IDataverseClient for development.
/// In production, this would use Microsoft.PowerPlatform.Dataverse.Client.
/// </summary>
public class MockDataverseClient : IDataverseClient
{
    private readonly ILogger<MockDataverseClient> _logger;
    private readonly string _connectionId;

    public MockDataverseClient(ILogger<MockDataverseClient> logger, string connectionId)
    {
        _logger = logger;
        _connectionId = connectionId;
    }

    public string ConnectionId => _connectionId;

    public Task<DataverseQueryResult> QueryAsync(string entityName, string? query = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Query: {EntityName} with query: {Query}", entityName, query);

        // Return mock data based on entity name
        var data = entityName switch
        {
            "solution" => GenerateMockSolutions(),
            "solutioncomponent" => GenerateMockSolutionComponents(),
            "msdyn_componentlayer" => GenerateMockComponentLayers(),
            "systemform" => GenerateMockForms(),
            "savedquery" => GenerateMockViews(),
            _ => "[]"
        };

        return Task.FromResult(new DataverseQueryResult
        {
            Success = true,
            Data = data,
            RecordCount = CountJsonArray(data)
        });
    }

    public Task<DataverseQueryResult> RetrieveAsync(string entityName, Guid id, string? selectClause = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieve: {EntityName} with ID: {Id}", entityName, id);

        // Return a mock single record
        var record = $"{{\"@odata.context\":\"#$metadata/{entityName}/$entity\",\"{entityName}id\":\"{id}\"}}";

        return Task.FromResult(new DataverseQueryResult
        {
            Success = true,
            Data = record,
            RecordCount = 1
        });
    }

    public Task<DataverseQueryResult> ExecuteAsync(string requestJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Execute request: {Request}", requestJson);

        return Task.FromResult(new DataverseQueryResult
        {
            Success = true,
            Data = "{}",
            RecordCount = 0
        });
    }

    private static string GenerateMockSolutions()
    {
        return JsonSerializer.Serialize(new[]
        {
            new
            {
                solutionid = Guid.NewGuid().ToString(),
                uniquename = "CoreSolution",
                friendlyname = "Core Solution",
                publisherid = new { name = "CorePublisher" },
                ismanaged = true,
                version = "1.0.0.0",
                installedon = DateTimeOffset.UtcNow.AddDays(-30)
            },
            new
            {
                solutionid = Guid.NewGuid().ToString(),
                uniquename = "ProjectA",
                friendlyname = "Project A Solution",
                publisherid = new { name = "ProjectPublisher" },
                ismanaged = true,
                version = "1.0.0.0",
                installedon = DateTimeOffset.UtcNow.AddDays(-15)
            },
            new
            {
                solutionid = Guid.NewGuid().ToString(),
                uniquename = "ProjectB",
                friendlyname = "Project B Solution",
                publisherid = new { name = "ProjectPublisher" },
                ismanaged = true,
                version = "1.0.0.0",
                installedon = DateTimeOffset.UtcNow.AddDays(-10)
            }
        });
    }

    private static string GenerateMockSolutionComponents()
    {
        var componentId = Guid.NewGuid();
        return JsonSerializer.Serialize(new[]
        {
            new
            {
                solutioncomponentid = Guid.NewGuid().ToString(),
                objectid = componentId.ToString(),
                componenttype = 26, // SavedQuery (View)
                solutionid = Guid.NewGuid().ToString()
            },
            new
            {
                solutioncomponentid = Guid.NewGuid().ToString(),
                objectid = Guid.NewGuid().ToString(),
                componenttype = 60, // SystemForm (Form)
                solutionid = Guid.NewGuid().ToString()
            }
        });
    }

    private static string GenerateMockComponentLayers()
    {
        return JsonSerializer.Serialize(new[]
        {
            new
            {
                msdyn_componentlayerid = Guid.NewGuid().ToString(),
                msdyn_componentid = Guid.NewGuid().ToString(),
                msdyn_solutioncomponentname = "SavedQuery",
                msdyn_solutionname = "CoreSolution",
                msdyn_name = "Active Accounts",
                msdyn_order = 0,
                msdyn_publishername = "CorePublisher",
                msdyn_changes = "{}",
                msdyn_componentjson = "{}"
            },
            new
            {
                msdyn_componentlayerid = Guid.NewGuid().ToString(),
                msdyn_componentid = Guid.NewGuid().ToString(),
                msdyn_solutioncomponentname = "SavedQuery",
                msdyn_solutionname = "ProjectA",
                msdyn_name = "Active Accounts",
                msdyn_order = 1,
                msdyn_publishername = "ProjectPublisher",
                msdyn_changes = "{}",
                msdyn_componentjson = "{}"
            }
        });
    }

    private static string GenerateMockForms()
    {
        return JsonSerializer.Serialize(new[]
        {
            new
            {
                formid = Guid.NewGuid().ToString(),
                name = "Account Main Form",
                objecttypecode = "account",
                formxml = "<form><tabs><tab name='general'><columns><column width='100%'><sections><section name='account_information' /></sections></column></columns></tab></tabs></form>"
            }
        });
    }

    private static string GenerateMockViews()
    {
        return JsonSerializer.Serialize(new[]
        {
            new
            {
                savedqueryid = Guid.NewGuid().ToString(),
                name = "Active Accounts",
                returnedtypecode = "account",
                fetchxml = "<fetch><entity name='account'><attribute name='name'/></entity></fetch>",
                layoutxml = "<grid><row><cell name='name'/></row></grid>"
            }
        });
    }

    private static int CountJsonArray(string json)
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() : 0;
        }
        catch
        {
            return 0;
        }
    }
}
