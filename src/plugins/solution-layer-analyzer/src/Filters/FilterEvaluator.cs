using System.Text.Json;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Evaluates filters against components.
/// </summary>
public class FilterEvaluator
{
    /// <summary>
    /// Evaluates a filter against a component.
    /// </summary>
    public bool Evaluate(FilterNode? filter, Component component)
    {
        if (filter == null)
        {
            return true; // No filter means all components match
        }

        return filter switch
        {
            // Component-level filters
            AttributeFilterNode attribute => EvaluateAttribute(attribute, component),
            ComponentTypeFilterNode componentType => EvaluateComponentType(componentType, component),
            ManagedFilterNode managed => EvaluateManaged(managed, component),
            PublisherFilterNode publisher => EvaluatePublisher(publisher, component),
            
            // Nested query filters
            LayerQueryFilterNode layerQuery => EvaluateLayerQuery(layerQuery, component),
            SolutionQueryFilterNode solutionQuery => EvaluateSolutionQueryFilter(solutionQuery, component),
            
            // Logical operators
            AndFilterNode and => EvaluateAnd(and, component),
            OrFilterNode or => EvaluateOr(or, component),
            NotFilterNode not => EvaluateNot(not, component),
            
            // Legacy layer filters (for backward compatibility)
            HasFilterNode has => EvaluateHas(has, component),
            HasAnyFilterNode hasAny => EvaluateHasAny(hasAny, component),
            HasAllFilterNode hasAll => EvaluateHasAll(hasAll, component),
            HasNoneFilterNode hasNone => EvaluateHasNone(hasNone, component),
            OrderStrictFilterNode orderStrict => EvaluateOrderStrict(orderStrict, component),
            OrderFlexFilterNode orderFlex => EvaluateOrderFlex(orderFlex, component),
            
            _ => true
        };
    }

    private bool EvaluateLayerQuery(LayerQueryFilterNode filter, Component component)
    {
        // LayerQuery wraps a layer filter - evaluate the nested filter
        return filter.LayerFilter != null ? Evaluate(filter.LayerFilter, component) : true;
    }

    private bool EvaluateSolutionQueryFilter(SolutionQueryFilterNode filter, Component component)
    {
        // Solution query filter - check if any layer's solution matches the query
        return component.Layers.Any(layer =>
            EvaluateStringOperator(filter.Operator, layer.SolutionName, filter.Value));
    }

    private bool EvaluateHas(HasFilterNode filter, Component component)
    {
        return component.Layers.Any(l => l.SolutionName.Equals(filter.Solution, StringComparison.OrdinalIgnoreCase));
    }

    private bool EvaluateHasAny(HasAnyFilterNode filter, Component component)
    {
        return filter.Solutions.Any(solution =>
            component.Layers.Any(l => l.SolutionName.Equals(solution, StringComparison.OrdinalIgnoreCase)));
    }

    private bool EvaluateHasAll(HasAllFilterNode filter, Component component)
    {
        return filter.Solutions.All(solution =>
            component.Layers.Any(l => l.SolutionName.Equals(solution, StringComparison.OrdinalIgnoreCase)));
    }

    private bool EvaluateHasNone(HasNoneFilterNode filter, Component component)
    {
        return !filter.Solutions.Any(solution =>
            component.Layers.Any(l => l.SolutionName.Equals(solution, StringComparison.OrdinalIgnoreCase)));
    }

    private bool EvaluateOrderStrict(OrderStrictFilterNode filter, Component component)
    {
        var layerSequence = component.Layers
            .OrderBy(l => l.Ordinal)
            .Select(l => l.SolutionName)
            .ToList();

        return MatchesSequence(filter.Sequence, layerSequence, strict: true);
    }

    private bool EvaluateOrderFlex(OrderFlexFilterNode filter, Component component)
    {
        var layerSequence = component.Layers
            .OrderBy(l => l.Ordinal)
            .Select(l => l.SolutionName)
            .ToList();

        return MatchesSequence(filter.Sequence, layerSequence, strict: false);
    }

    private bool MatchesSequence(List<object> sequence, List<string> layerSequence, bool strict)
    {
        var sequenceIndex = 0;
        var layerIndex = 0;

        while (sequenceIndex < sequence.Count && layerIndex < layerSequence.Count)
        {
            var sequenceItem = sequence[sequenceIndex];
            var currentLayer = layerSequence[layerIndex];

            // Check if sequence item matches current layer
            bool matches = false;

            if (sequenceItem is string singleSolution)
            {
                matches = currentLayer.Equals(singleSolution, StringComparison.OrdinalIgnoreCase);
            }
            else if (sequenceItem is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    // Handle array of solution choices
                    var choices = jsonElement.EnumerateArray()
                        .Select(e => e.GetString() ?? string.Empty)
                        .ToList();
                    matches = choices.Any(choice => currentLayer.Equals(choice, StringComparison.OrdinalIgnoreCase));
                }
                else if (jsonElement.ValueKind == JsonValueKind.Object)
                {
                    // Try to deserialize as SolutionQueryNode
                    try
                    {
                        var query = JsonSerializer.Deserialize<SolutionQueryNode>(jsonElement.GetRawText());
                        if (query != null)
                        {
                            matches = EvaluateSolutionQuery(query, currentLayer);
                        }
                    }
                    catch
                    {
                        // Not a valid SolutionQueryNode, skip
                    }
                }
            }
            else if (sequenceItem is SolutionQueryNode query)
            {
                // Handle direct SolutionQueryNode (when not coming from JSON)
                matches = EvaluateSolutionQuery(query, currentLayer);
            }

            if (matches)
            {
                sequenceIndex++;
                layerIndex++;
            }
            else if (strict)
            {
                // In strict mode, any mismatch means failure
                return false;
            }
            else
            {
                // In flex mode, skip this layer and try the next one
                layerIndex++;
            }
        }

        // All sequence items must be matched
        return sequenceIndex == sequence.Count;
    }

    private bool EvaluateAnd(AndFilterNode filter, Component component)
    {
        return filter.Children.All(child => Evaluate(child, component));
    }

    private bool EvaluateOr(OrFilterNode filter, Component component)
    {
        return filter.Children.Any(child => Evaluate(child, component));
    }

    private bool EvaluateNot(NotFilterNode filter, Component component)
    {
        return !Evaluate(filter.Child, component);
    }

    private bool EvaluateComponentType(ComponentTypeFilterNode filter, Component component)
    {
        return component.ComponentType.Equals(filter.ComponentType, StringComparison.OrdinalIgnoreCase);
    }

    private bool EvaluateManaged(ManagedFilterNode filter, Component component)
    {
        var hasManaged = component.Layers.Any(l => l.IsManaged);
        return hasManaged == filter.IsManaged;
    }

    private bool EvaluatePublisher(PublisherFilterNode filter, Component component)
    {
        return component.Layers.Any(l => 
            l.Publisher != null && l.Publisher.Equals(filter.Publisher, StringComparison.OrdinalIgnoreCase));
    }

    private bool EvaluateAttribute(AttributeFilterNode filter, Component component)
    {
        var targetValue = filter.Attribute switch
        {
            AttributeTarget.LogicalName => component.LogicalName,
            AttributeTarget.DisplayName => component.DisplayName ?? string.Empty,
            AttributeTarget.ComponentType => component.ComponentType,
            AttributeTarget.TableLogicalName => component.TableLogicalName ?? string.Empty,
            AttributeTarget.Publisher => GetComponentPublisher(component),
            _ => string.Empty
        };

        return EvaluateStringOperator(filter.Operator, targetValue, filter.Value);
    }

    private string GetComponentPublisher(Component component)
    {
        // Get the first publisher from layers (could be refined)
        return component.Layers.FirstOrDefault()?.Publisher ?? string.Empty;
    }

    private bool EvaluateStringOperator(StringOperator op, string targetValue, string filterValue)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;

        return op switch
        {
            StringOperator.Equals => targetValue.Equals(filterValue, comparison),
            StringOperator.NotEquals => !targetValue.Equals(filterValue, comparison),
            StringOperator.Contains => targetValue.Contains(filterValue, comparison),
            StringOperator.NotContains => !targetValue.Contains(filterValue, comparison),
            StringOperator.BeginsWith => targetValue.StartsWith(filterValue, comparison),
            StringOperator.NotBeginsWith => !targetValue.StartsWith(filterValue, comparison),
            StringOperator.EndsWith => targetValue.EndsWith(filterValue, comparison),
            StringOperator.NotEndsWith => !targetValue.EndsWith(filterValue, comparison),
            _ => true
        };
    }

    /// <summary>
    /// Evaluates a solution query against a solution name.
    /// Used within ORDER sequence evaluation.
    /// </summary>
    private bool EvaluateSolutionQuery(SolutionQueryNode query, string solutionName)
    {
        // For now, we only support SchemaName attribute
        // The solutionName IS the schema name in our context
        return EvaluateStringOperator(query.Operator, solutionName, query.Value);
    }
}
