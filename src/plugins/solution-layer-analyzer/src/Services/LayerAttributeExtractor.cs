using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ddk.SolutionLayerAnalyzer.Models;
using Microsoft.Extensions.Logging;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for extracting and formatting layer attributes from component JSON.
/// Attributes are extracted during indexing and stored in a queryable format.
/// </summary>
public class LayerAttributeExtractor
{
    private readonly ILogger _logger;
    private readonly PayloadService _payloadService;

    public LayerAttributeExtractor(ILogger logger, PayloadService payloadService)
    {
        _logger = logger;
        _payloadService = payloadService;
    }

    /// <summary>
    /// Extracts attributes from a layer's component JSON.
    /// Returns a list of LayerAttribute objects ready to be stored.
    /// </summary>
    /// <param name="layerId">The layer ID</param>
    /// <param name="componentJson">The component JSON from msdyn_componentjson</param>
    /// <param name="changes">The changes JSON from msdyn_changes (optional)</param>
    public List<LayerAttribute> ExtractAttributes(Guid layerId, string? componentJson, string? changes = null)
    {
        var attributes = new List<LayerAttribute>();

        if (string.IsNullOrWhiteSpace(componentJson))
        {
            return attributes;
        }

        // Parse changes to get the set of changed attribute names
        var changedAttributeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(changes))
        {
            try
            {
                using var changesDoc = JsonDocument.Parse(changes);
                var changesRoot = changesDoc.RootElement;

                // Check if it's an object with Attributes array (common Dataverse format)
                if (changesRoot.ValueKind == JsonValueKind.Object && changesRoot.TryGetProperty("Attributes", out var changesAttributesArray))
                {
                    if (changesAttributesArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in changesAttributesArray.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("Key", out var keyElement))
                            {
                                var attributeName = keyElement.GetString();
                                if (!string.IsNullOrEmpty(attributeName))
                                {
                                    changedAttributeNames.Add(attributeName);
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse changes JSON for layer {LayerId}", layerId);
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(componentJson);
            var root = doc.RootElement;

            // Check if it's an object with Attributes array (common Dataverse format)
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Attributes", out var attributesArray))
            {
                ExtractFromAttributesArray(layerId, attributesArray, attributes, changedAttributeNames);
            }
            else
            {
                // Otherwise, extract all properties at root level
                ExtractFromJsonObject(layerId, root, attributes, changedAttributeNames);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse component JSON for layer {LayerId}", layerId);
        }

        return attributes;
    }

    /// <summary>
    /// Extracts attributes from a Dataverse Attributes array format.
    /// Format: { "Attributes": [{ "Key": "name", "Value": "value" }] }
    /// </summary>
    private void ExtractFromAttributesArray(Guid layerId, JsonElement attributesArray, List<LayerAttribute> result, HashSet<string> changedAttributeNames)
    {
        if (attributesArray.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in attributesArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty("Key", out var keyElement) || 
                !item.TryGetProperty("Value", out var valueElement))
            {
                continue;
            }

            var attributeName = keyElement.GetString();
            if (string.IsNullOrEmpty(attributeName))
            {
                continue;
            }

            var attribute = CreateLayerAttribute(layerId, attributeName, valueElement);
            if (attribute != null)
            {
                attribute.IsChanged = changedAttributeNames.Contains(attributeName);
                result.Add(attribute);
            }
        }
    }

    /// <summary>
    /// Extracts attributes from a flat JSON object.
    /// </summary>
    private void ExtractFromJsonObject(Guid layerId, JsonElement obj, List<LayerAttribute> result, HashSet<string> changedAttributeNames)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in obj.EnumerateObject())
        {
            var attribute = CreateLayerAttribute(layerId, property.Name, property.Value);
            if (attribute != null)
            {
                attribute.IsChanged = changedAttributeNames.Contains(property.Name);
                result.Add(attribute);
            }
        }
    }

    /// <summary>
    /// Creates a LayerAttribute from a JSON property.
    /// Handles type detection and value formatting.
    /// </summary>
    private LayerAttribute? CreateLayerAttribute(Guid layerId, string name, JsonElement value)
    {
        try
        {
            var attribute = new LayerAttribute
            {
                AttributeId = Guid.NewGuid(),
                LayerId = layerId,
                AttributeName = name
            };

            // Determine type and extract value
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    ProcessStringValue(attribute, value.GetString());
                    break;

                case JsonValueKind.Number:
                    attribute.AttributeType = LayerAttributeType.Number;
                    attribute.AttributeValue = value.GetRawText();
                    attribute.RawValue = value.GetRawText();
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    attribute.AttributeType = LayerAttributeType.Boolean;
                    attribute.AttributeValue = value.GetBoolean().ToString();
                    attribute.RawValue = value.GetBoolean().ToString();
                    break;

                case JsonValueKind.Object:
                    ProcessObjectValue(attribute, value);
                    break;

                case JsonValueKind.Array:
                    ProcessArrayValue(attribute, value);
                    break;

                case JsonValueKind.Null:
                    attribute.AttributeType = LayerAttributeType.String;
                    attribute.AttributeValue = null;
                    attribute.RawValue = null;
                    break;

                default:
                    return null;
            }

            // Compute attribute hash for efficient cross-layer comparison
            attribute.AttributeHash = ComputeAttributeHash(attribute.AttributeName, attribute.RawValue);

            return attribute;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create attribute {Name} for layer {LayerId}", name, layerId);
            return null;
        }
    }

    /// <summary>
    /// Processes a string value, detecting and formatting complex types.
    /// </summary>
    private void ProcessStringValue(LayerAttribute attribute, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            attribute.AttributeType = LayerAttributeType.String;
            attribute.AttributeValue = value;
            attribute.RawValue = value;
            return;
        }

        var trimmed = value.Trim();

        // Check if it's XML
        if (trimmed.StartsWith('<') && trimmed.EndsWith('>'))
        {
            attribute.AttributeType = LayerAttributeType.Xml;
            attribute.IsComplexValue = true;
            attribute.RawValue = value;
            attribute.AttributeValue = FormatXml(value);
            return;
        }

        // Check if it's JSON
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                attribute.AttributeType = LayerAttributeType.Json;
                attribute.IsComplexValue = true;
                attribute.RawValue = value;
                attribute.AttributeValue = FormatJson(doc.RootElement);
                return;
            }
            catch (JsonException)
            {
                // Not valid JSON, treat as string
            }
        }

        // Check if it's a DateTime
        if (DateTime.TryParse(value, out var dateTime))
        {
            attribute.AttributeType = LayerAttributeType.DateTime;
            attribute.AttributeValue = dateTime.ToString("o"); // ISO 8601 format
            attribute.RawValue = value;
            return;
        }

        // Default to string
        attribute.AttributeType = LayerAttributeType.String;
        attribute.AttributeValue = value;
        attribute.RawValue = value;
    }

    /// <summary>
    /// Processes an object value (EntityReference, Money, OptionSet, etc.).
    /// </summary>
    private void ProcessObjectValue(LayerAttribute attribute, JsonElement value)
    {
        // Check for EntityReference pattern
        if (value.TryGetProperty("Id", out var idProp) && 
            value.TryGetProperty("LogicalName", out var logicalNameProp))
        {
            attribute.AttributeType = LayerAttributeType.EntityReference;
            attribute.IsComplexValue = true;
            attribute.RawValue = value.GetRawText();
            
            var id = idProp.GetString();
            var logicalName = logicalNameProp.GetString();
            var name = value.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
            
            attribute.AttributeValue = name ?? $"{logicalName} ({id})";
            return;
        }

        // Check for Money pattern
        if (value.TryGetProperty("Value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Number)
        {
            attribute.AttributeType = LayerAttributeType.Money;
            attribute.IsComplexValue = true;
            attribute.RawValue = value.GetRawText();
            attribute.AttributeValue = valueProp.GetDecimal().ToString("N2");
            return;
        }

        // Check for OptionSet pattern
        if (value.TryGetProperty("Value", out var optionValue) && optionValue.ValueKind == JsonValueKind.Number)
        {
            attribute.AttributeType = LayerAttributeType.OptionSet;
            attribute.IsComplexValue = true;
            attribute.RawValue = value.GetRawText();
            
            var label = value.TryGetProperty("Label", out var labelProp) ? labelProp.GetString() : null;
            var val = optionValue.GetInt32();
            
            attribute.AttributeValue = label ?? val.ToString();
            return;
        }

        // Default: serialize as JSON
        attribute.AttributeType = LayerAttributeType.Json;
        attribute.IsComplexValue = true;
        attribute.RawValue = value.GetRawText();
        attribute.AttributeValue = FormatJson(value);
    }

    /// <summary>
    /// Processes an array value.
    /// </summary>
    private void ProcessArrayValue(LayerAttribute attribute, JsonElement value)
    {
        attribute.AttributeType = LayerAttributeType.Json;
        attribute.IsComplexValue = true;
        attribute.RawValue = value.GetRawText();
        attribute.AttributeValue = FormatJson(value);
    }

    /// <summary>
    /// Formats XML with proper indentation.
    /// </summary>
    private string FormatXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.ToString(SaveOptions.None);
        }
        catch
        {
            return xml; // Return as-is if parsing fails
        }
    }

    /// <summary>
    /// Formats JSON with proper indentation.
    /// </summary>
    private string FormatJson(JsonElement element)
    {
        try
        {
            return JsonSerializer.Serialize(element, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return element.GetRawText();
        }
    }

    /// <summary>
    /// Computes a hash of the attribute name and raw value for efficient cross-layer comparison.
    /// Returns a 40-character hex string (truncated SHA256).
    /// </summary>
    private static string ComputeAttributeHash(string attributeName, string? rawValue)
    {
        var input = $"{attributeName}|{rawValue ?? ""}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        // Take first 20 bytes (40 hex chars) for reasonable uniqueness with smaller storage
        return Convert.ToHexString(hashBytes, 0, 20);
    }
}
