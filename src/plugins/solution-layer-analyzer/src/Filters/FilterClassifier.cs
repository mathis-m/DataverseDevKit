namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Describes how a filter can be executed.
/// </summary>
public enum FilterCapability
{
    /// <summary>
    /// Filter can be fully translated to SQL WHERE clause.
    /// </summary>
    FullySqlTranslatable,

    /// <summary>
    /// Filter requires pre-fetch operations but can then be translated to SQL.
    /// Example: ORDER filters need ordinal lookups first.
    /// </summary>
    HybridSqlTranslatable,

    /// <summary>
    /// Filter must be evaluated in memory after data retrieval.
    /// </summary>
    InMemoryOnly
}

/// <summary>
/// Classification result for a filter node.
/// </summary>
public sealed class FilterClassification
{
    /// <summary>
    /// The overall capability of the filter.
    /// </summary>
    public FilterCapability Capability { get; init; }

    /// <summary>
    /// Human-readable reason for the classification.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Child classifications for composite filters.
    /// </summary>
    public List<FilterClassification> Children { get; init; } = new();

    /// <summary>
    /// The filter node this classification applies to.
    /// </summary>
    public FilterNode? Node { get; init; }
}

/// <summary>
/// Classifies filter nodes by their SQL translatability.
/// </summary>
public sealed class FilterClassifier
{
    /// <summary>
    /// Classifies a filter node and returns its capability.
    /// </summary>
    public FilterClassification Classify(FilterNode? filter)
    {
        if (filter == null)
        {
            return new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "Null filter (no filtering)"
            };
        }

        return filter switch
        {
            // Component-level filters - fully translatable
            AttributeFilterNode attr => ClassifyAttributeFilter(attr),
            ComponentTypeFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "COMPONENT_TYPE translates to simple WHERE clause",
                Node = filter
            },
            ManagedFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "MANAGED translates to EXISTS subquery on Layers",
                Node = filter
            },
            PublisherFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "PUBLISHER translates to EXISTS subquery on Layers",
                Node = filter
            },

            // Layer query filters
            LayerQueryFilterNode layerQuery => ClassifyLayerQuery(layerQuery),
            SolutionQueryFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "SOLUTION_QUERY translates to EXISTS subquery on Layers",
                Node = filter
            },

            // Layer attribute filters
            LayerAttributeFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "LAYER_ATTRIBUTE translates to EXISTS subquery on Layers/Attributes",
                Node = filter
            },
            LayerAttributeQueryFilterNode layerAttrQuery => ClassifyLayerAttributeQuery(layerAttrQuery),
            HasRelevantChangesFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS_RELEVANT_CHANGES translates to EXISTS subquery on Attributes with IsChanged",
                Node = filter
            },
            HasAttributeDiffFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS_ATTRIBUTE_DIFF translates to correlated subquery comparing AttributeHash across layers",
                Node = filter
            },

            // Logical operators - inherit worst case from children
            AndFilterNode and => ClassifyLogicalOperator(and.Children, "AND", filter),
            OrFilterNode or => ClassifyLogicalOperator(or.Children, "OR", filter),
            NotFilterNode not => ClassifyNot(not),

            // Legacy layer filters
            HasFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS translates to EXISTS subquery on Layers",
                Node = filter
            },
            HasAnyFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS_ANY translates to EXISTS subquery with IN clause",
                Node = filter
            },
            HasAllFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS_ALL translates to multiple EXISTS subqueries",
                Node = filter
            },
            HasNoneFilterNode => new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "HAS_NONE translates to NOT EXISTS subquery",
                Node = filter
            },

            // ORDER filters - hybrid (need ordinal pre-fetch)
            OrderStrictFilterNode orderStrict => ClassifyOrderFilter(orderStrict.Sequence, "ORDER_STRICT", filter),
            OrderFlexFilterNode orderFlex => ClassifyOrderFilter(orderFlex.Sequence, "ORDER_FLEX", filter),

            // Unknown filter types - in memory only
            _ => new FilterClassification
            {
                Capability = FilterCapability.InMemoryOnly,
                Reason = $"Unknown filter type: {filter.GetType().Name}",
                Node = filter
            }
        };
    }

    private FilterClassification ClassifyAttributeFilter(AttributeFilterNode attr)
    {
        // All attribute targets can be translated to SQL
        return new FilterClassification
        {
            Capability = FilterCapability.FullySqlTranslatable,
            Reason = $"ATTRIBUTE({attr.Attribute}) translates to WHERE clause on Component",
            Node = attr
        };
    }

    private FilterClassification ClassifyLayerQuery(LayerQueryFilterNode filter)
    {
        if (filter.LayerFilter == null)
        {
            return new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "LAYER_QUERY with null filter",
                Node = filter
            };
        }

        // Classify the nested filter
        var childClassification = Classify(filter.LayerFilter);
        return new FilterClassification
        {
            Capability = childClassification.Capability,
            Reason = $"LAYER_QUERY inherits capability from nested filter",
            Node = filter,
            Children = new List<FilterClassification> { childClassification }
        };
    }

    private FilterClassification ClassifyLayerAttributeQuery(LayerAttributeQueryFilterNode filter)
    {
        if (filter.AttributeFilter == null)
        {
            return new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "LAYER_ATTRIBUTE_QUERY checks layer existence only",
                Node = filter
            };
        }

        var childClassification = Classify(filter.AttributeFilter);
        return new FilterClassification
        {
            Capability = childClassification.Capability,
            Reason = "LAYER_ATTRIBUTE_QUERY inherits capability from nested filter",
            Node = filter,
            Children = new List<FilterClassification> { childClassification }
        };
    }

    private FilterClassification ClassifyLogicalOperator(List<FilterNode> children, string opName, FilterNode node)
    {
        var childClassifications = children.Select(Classify).ToList();

        // The overall capability is the worst case among children
        var worstCapability = FilterCapability.FullySqlTranslatable;
        foreach (var child in childClassifications)
        {
            if (child.Capability > worstCapability)
            {
                worstCapability = child.Capability;
            }
        }

        return new FilterClassification
        {
            Capability = worstCapability,
            Reason = $"{opName} inherits worst capability from {children.Count} children",
            Node = node,
            Children = childClassifications
        };
    }

    private FilterClassification ClassifyNot(NotFilterNode not)
    {
        if (not.Child == null)
        {
            return new FilterClassification
            {
                Capability = FilterCapability.FullySqlTranslatable,
                Reason = "NOT with null child",
                Node = not
            };
        }

        var childClassification = Classify(not.Child);
        return new FilterClassification
        {
            Capability = childClassification.Capability,
            Reason = "NOT inherits capability from child",
            Node = not,
            Children = new List<FilterClassification> { childClassification }
        };
    }

    private FilterClassification ClassifyOrderFilter(List<object> sequence, string filterType, FilterNode node)
    {
        // ORDER filters can be hybrid - we pre-fetch ordinals, then build SQL
        // But if sequence contains SolutionQueryNodes with complex operators, may need in-memory
        bool hasComplexQueries = sequence.Any(item =>
        {
            if (item is System.Text.Json.JsonElement jsonElement &&
                jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // This might be a SolutionQueryNode - could be complex
                return true;
            }
            return item is SolutionQueryNode;
        });

        if (hasComplexQueries)
        {
            return new FilterClassification
            {
                Capability = FilterCapability.HybridSqlTranslatable,
                Reason = $"{filterType} with dynamic solution queries requires ordinal pre-fetch",
                Node = node
            };
        }

        return new FilterClassification
        {
            Capability = FilterCapability.HybridSqlTranslatable,
            Reason = $"{filterType} requires ordinal pre-fetch but can then use SQL",
            Node = node
        };
    }

    /// <summary>
    /// Gets a summary of filter capabilities for debugging/telemetry.
    /// </summary>
    public Dictionary<FilterCapability, int> GetCapabilitySummary(FilterNode? filter)
    {
        var summary = new Dictionary<FilterCapability, int>
        {
            { FilterCapability.FullySqlTranslatable, 0 },
            { FilterCapability.HybridSqlTranslatable, 0 },
            { FilterCapability.InMemoryOnly, 0 }
        };

        CountCapabilities(filter, summary);
        return summary;
    }

    private void CountCapabilities(FilterNode? filter, Dictionary<FilterCapability, int> summary)
    {
        if (filter == null) return;

        var classification = Classify(filter);
        summary[classification.Capability]++;

        // Count children
        switch (filter)
        {
            case AndFilterNode and:
                foreach (var child in and.Children)
                    CountCapabilities(child, summary);
                break;
            case OrFilterNode or:
                foreach (var child in or.Children)
                    CountCapabilities(child, summary);
                break;
            case NotFilterNode not:
                CountCapabilities(not.Child, summary);
                break;
            case LayerQueryFilterNode lq:
                CountCapabilities(lq.LayerFilter, summary);
                break;
            case LayerAttributeQueryFilterNode laq:
                CountCapabilities(laq.AttributeFilter, summary);
                break;
        }
    }
}
