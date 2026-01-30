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
    /// Retrieves the payload from a layer's componentjson field.
    /// This works for ALL component types since it uses the msdyn_componentjson field.
    /// </summary>
    public (string? Payload, string MimeType) RetrievePayloadFromComponentJson(string? componentJson)
    {
        if (string.IsNullOrWhiteSpace(componentJson))
        {
            return (null, "application/json");
        }

        try
        {
            // The componentjson is already JSON, just normalize it
            var normalized = NormalizeJson(componentJson);
            return (normalized, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing component JSON");
            return (componentJson, "application/json");
        }
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
    /// Recursively normalizes nested JSON strings within attribute values.
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
            var normalized = NormalizeJsonElement(doc.RootElement);
            return JsonSerializer.Serialize(normalized, new JsonSerializerOptions
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

    /// <summary>
    /// Recursively normalizes a JsonElement, detecting and parsing nested JSON strings.
    /// </summary>
    private object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeJsonObject(element),
            JsonValueKind.Array => NormalizeJsonArray(element),
            JsonValueKind.String => NormalizeJsonString(element.GetString()),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Normalizes a JSON object by recursively normalizing its properties.
    /// </summary>
    private Dictionary<string, object?> NormalizeJsonObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = NormalizeJsonElement(property.Value);
        }
        return result;
    }

    /// <summary>
    /// Normalizes a JSON array by recursively normalizing its elements.
    /// </summary>
    private List<object?> NormalizeJsonArray(JsonElement element)
    {
        var result = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(NormalizeJsonElement(item));
        }
        return result;
    }

    /// <summary>
    /// Normalizes a string value, attempting to parse it as nested JSON if possible.
    /// This handles double-encoded JSON strings from Dataverse componentjson.
    /// </summary>
    private object? NormalizeJsonString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();

        // Check if the string looks like it could be JSON (object or array)
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            try
            {
                using var nestedDoc = JsonDocument.Parse(trimmed);
                // Successfully parsed as JSON - recursively normalize it
                return NormalizeJsonElement(nestedDoc.RootElement);
            }
            catch (JsonException)
            {
                // Not valid JSON, return as string
                return value;
            }
        }

        // Check if the string looks like XML and normalize it
        if (trimmed.StartsWith('<') && trimmed.EndsWith('>'))
        {
            return NormalizeXml(value);
        }

        return value;
    }
}
