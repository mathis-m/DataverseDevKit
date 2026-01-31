namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Shared constants for filter evaluation.
/// </summary>
public static class FilterConstants
{
    /// <summary>
    /// Attributes to always exclude from relevance evaluation and diff comparison.
    /// These are system/metadata fields that have no meaningful impact on the component's behavior.
    /// </summary>
    public static readonly HashSet<string> ExcludedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "sitemapnameunique",
        "solutionid",
        "sitemapxml",
        "ismanaged",
        "sitemapidunique",
        "isappaware",
        "createdon",
        "componentstate",
        "modifiedon",
        "sitemapid",
        "versionnumber",
        "sitemapname",
        "enablecollapsiblegroups",
        "showhome",
        "showrecents",
        "showpinned",
        "savedqueryidunique",
    };
}
