using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Extensions.Logging;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for retrieving and normalizing component payloads.
/// </summary>
public class PayloadService
{
    private readonly ServiceClient _serviceClient;
    private readonly ILogger _logger;

    public PayloadService(ServiceClient serviceClient, ILogger logger)
    {
        _serviceClient = serviceClient;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the payload for a component.
    /// </summary>
    public async Task<(string? Payload, string MimeType)> RetrievePayloadAsync(
        Guid objectId,
        string componentType,
        string? solutionName,
        CancellationToken cancellationToken)
    {
        try
        {
            return componentType.ToUpperInvariant() switch
            {
                "SYSTEMFORM" or "FORM" => await RetrieveFormPayloadAsync(objectId, cancellationToken),
                "SAVEDQUERY" or "VIEW" => await RetrieveViewPayloadAsync(objectId, cancellationToken),
                "RIBBONCUSTOMIZATION" or "RIBBON" => await RetrieveRibbonPayloadAsync(objectId, cancellationToken),
                "WEBRESOURCE" => await RetrieveWebResourcePayloadAsync(objectId, cancellationToken),
                _ => (null, "text/plain")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payload for {ComponentType} {ObjectId}", componentType, objectId);
            return (null, "text/plain");
        }
    }

    private async Task<(string? Payload, string MimeType)> RetrieveFormPayloadAsync(Guid formId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("formxml"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("formid", ConditionOperator.Equal, formId) }
            }
        };

        var result = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

        if (result.Entities.Count > 0)
        {
            var formXml = result.Entities[0].GetAttributeValue<string>("formxml");
            if (!string.IsNullOrEmpty(formXml))
            {
                return (NormalizeXml(formXml), "application/xml");
            }
        }

        return (null, "application/xml");
    }

    private async Task<(string? Payload, string MimeType)> RetrieveViewPayloadAsync(Guid viewId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("savedquery")
        {
            ColumnSet = new ColumnSet("fetchxml", "layoutxml", "columnsetxml"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("savedqueryid", ConditionOperator.Equal, viewId) }
            }
        };

        var result = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

        if (result.Entities.Count > 0)
        {
            var entity = result.Entities[0];
            var fetchXml = entity.GetAttributeValue<string>("fetchxml");
            var layoutXml = entity.GetAttributeValue<string>("layoutxml");
            var columnSetXml = entity.GetAttributeValue<string>("columnsetxml");

            // Combine all XML parts
            var combined = new StringBuilder();
            combined.AppendLine("<!-- FetchXML -->");
            combined.AppendLine(NormalizeXml(fetchXml ?? ""));
            combined.AppendLine();
            combined.AppendLine("<!-- LayoutXML -->");
            combined.AppendLine(NormalizeXml(layoutXml ?? ""));

            if (!string.IsNullOrEmpty(columnSetXml))
            {
                combined.AppendLine();
                combined.AppendLine("<!-- ColumnSetXML -->");
                combined.AppendLine(NormalizeXml(columnSetXml));
            }

            return (combined.ToString(), "application/xml");
        }

        return (null, "application/xml");
    }

    private async Task<(string? Payload, string MimeType)> RetrieveRibbonPayloadAsync(Guid ribbonId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("ribboncustomization")
        {
            ColumnSet = new ColumnSet("entity", "ribbondiffxml"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("ribboncustomizationid", ConditionOperator.Equal, ribbonId) }
            }
        };

        var result = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

        if (result.Entities.Count > 0)
        {
            var ribbonDiffXml = result.Entities[0].GetAttributeValue<string>("ribbondiffxml");
            if (!string.IsNullOrEmpty(ribbonDiffXml))
            {
                return (NormalizeXml(ribbonDiffXml), "application/xml");
            }
        }

        return (null, "application/xml");
    }

    private async Task<(string? Payload, string MimeType)> RetrieveWebResourcePayloadAsync(Guid webResourceId, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("webresource")
        {
            ColumnSet = new ColumnSet("content", "webresourcetype"),
            Criteria = new FilterExpression
            {
                Conditions = { new ConditionExpression("webresourceid", ConditionOperator.Equal, webResourceId) }
            }
        };

        var result = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

        if (result.Entities.Count > 0)
        {
            var entity = result.Entities[0];
            var content = entity.GetAttributeValue<string>("content");
            var webResourceType = entity.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value ?? 0;

            if (!string.IsNullOrEmpty(content))
            {
                // Decode base64 content
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(content));

                // Determine MIME type
                var mimeType = webResourceType switch
                {
                    1 => "text/html",
                    2 => "text/css",
                    3 => "application/javascript",
                    4 => "application/xml",
                    5 => "image/png",
                    6 => "image/jpeg",
                    7 => "image/gif",
                    8 => "application/x-silverlight-app",
                    9 => "text/css",
                    10 => "image/x-icon",
                    11 => "image/svg+xml",
                    _ => "text/plain"
                };

                // For text files, normalize if JSON or XML
                if (mimeType.Contains("json") || mimeType.Contains("javascript"))
                {
                    return (NormalizeJson(decoded), mimeType);
                }
                else if (mimeType.Contains("xml") || mimeType.Contains("html"))
                {
                    return (NormalizeXml(decoded), mimeType);
                }

                return (decoded, mimeType);
            }
        }

        return (null, "text/plain");
    }

    /// <summary>
    /// Normalizes XML for diffing (pretty-print, consistent formatting).
    /// </summary>
    private string NormalizeXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            var doc = XDocument.Parse(xml);
            return doc.ToString(SaveOptions.None);
        }
        catch (XmlException)
        {
            // If XML is invalid, return as-is
            return xml;
        }
    }

    /// <summary>
    /// Normalizes JSON for diffing (pretty-print, consistent formatting).
    /// </summary>
    private string NormalizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            // If JSON is invalid, return as-is
            return json;
        }
    }
}
