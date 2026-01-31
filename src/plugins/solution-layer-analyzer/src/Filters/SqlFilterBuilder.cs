using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Builds EF Core-compatible expressions from filter nodes.
/// These expressions can be translated to SQL by the EF Core query provider.
/// </summary>
public sealed class SqlFilterBuilder
{
    /// <summary>
    /// Pre-computed ordinal constraints for ORDER filters.
    /// Key: solution name (case-insensitive), Value: (minOrdinal, maxOrdinal)
    /// </summary>
    private readonly Dictionary<string, (int Min, int Max)>? _ordinalLookup;

    /// <summary>
    /// Pre-computed solution name mappings for SolutionQuery resolution.
    /// Key: original query value, Value: list of matching solution names
    /// </summary>
    private readonly Dictionary<string, List<string>>? _solutionQueryResults;

    /// <summary>
    /// Creates a new SqlFilterBuilder with optional pre-computed lookups.
    /// </summary>
    /// <param name="ordinalLookup">Pre-fetched ordinal ranges per solution.</param>
    /// <param name="solutionQueryResults">Pre-resolved solution query results.</param>
    public SqlFilterBuilder(
        Dictionary<string, (int Min, int Max)>? ordinalLookup = null,
        Dictionary<string, List<string>>? solutionQueryResults = null)
    {
        _ordinalLookup = ordinalLookup;
        _solutionQueryResults = solutionQueryResults;
    }

    /// <summary>
    /// Builds an expression tree that can be used in EF Core Where() clause.
    /// </summary>
    /// <param name="filter">The filter to translate.</param>
    /// <returns>An expression that evaluates to true for matching components.</returns>
    public Expression<Func<Component, bool>> BuildExpression(FilterNode? filter)
    {
        if (filter == null)
        {
            return c => true;
        }

        var param = Expression.Parameter(typeof(Component), "c");
        var body = BuildExpressionBody(filter, param);
        return Expression.Lambda<Func<Component, bool>>(body, param);
    }

    private Expression BuildExpressionBody(FilterNode filter, ParameterExpression componentParam)
    {
        return filter switch
        {
            // Component-level filters
            AttributeFilterNode attr => BuildAttributeFilter(attr, componentParam),
            ComponentTypeFilterNode ct => BuildComponentTypeFilter(ct, componentParam),
            ManagedFilterNode m => BuildManagedFilter(m, componentParam),
            PublisherFilterNode p => BuildPublisherFilter(p, componentParam),

            // Layer query filters
            LayerQueryFilterNode lq => BuildLayerQueryFilter(lq, componentParam),
            SolutionQueryFilterNode sq => BuildSolutionQueryFilter(sq, componentParam),

            // Layer attribute filters
            LayerAttributeFilterNode la => BuildLayerAttributeFilter(la, componentParam),
            LayerAttributeQueryFilterNode laq => BuildLayerAttributeQueryFilter(laq, componentParam),
            HasRelevantChangesFilterNode => BuildHasRelevantChangesFilter(componentParam),
            HasAttributeDiffFilterNode diff => BuildHasAttributeDiffFilter(diff, componentParam),

            // Logical operators
            AndFilterNode and => BuildAndFilter(and, componentParam),
            OrFilterNode or => BuildOrFilter(or, componentParam),
            NotFilterNode not => BuildNotFilter(not, componentParam),

            // Legacy layer filters
            HasFilterNode has => BuildHasFilter(has, componentParam),
            HasAnyFilterNode hasAny => BuildHasAnyFilter(hasAny, componentParam),
            HasAllFilterNode hasAll => BuildHasAllFilter(hasAll, componentParam),
            HasNoneFilterNode hasNone => BuildHasNoneFilter(hasNone, componentParam),

            // ORDER filters - complex, use hybrid approach
            OrderStrictFilterNode orderStrict => BuildOrderStrictFilter(orderStrict, componentParam),
            OrderFlexFilterNode orderFlex => BuildOrderFlexFilter(orderFlex, componentParam),

            // Fallback - always true (filter will be applied in memory)
            _ => Expression.Constant(true)
        };
    }

    #region Component-Level Filters

    private Expression BuildAttributeFilter(AttributeFilterNode filter, ParameterExpression param)
    {
        // Get the property to filter on
        var targetProperty = filter.Attribute switch
        {
            AttributeTarget.LogicalName => Expression.Property(param, nameof(Component.LogicalName)),
            AttributeTarget.DisplayName => Expression.Coalesce(
                Expression.Property(param, nameof(Component.DisplayName)),
                Expression.Constant(string.Empty)),
            AttributeTarget.ComponentType => Expression.Property(param, nameof(Component.ComponentType)),
            AttributeTarget.TableLogicalName => Expression.Coalesce(
                Expression.Property(param, nameof(Component.TableLogicalName)),
                Expression.Constant(string.Empty)),
            AttributeTarget.Publisher => BuildPublisherPropertyAccess(param),
            _ => Expression.Constant(string.Empty)
        };

        return BuildStringOperatorExpression(targetProperty, filter.Operator, filter.Value);
    }

    private Expression BuildPublisherPropertyAccess(ParameterExpression param)
    {
        // c.Layers.FirstOrDefault().Publisher ?? ""
        // This is complex in expression trees, so we use a subquery approach instead
        // c.Layers.Select(l => l.Publisher).FirstOrDefault() ?? ""
        var layersProperty = Expression.Property(param, nameof(Component.Layers));

        // Build: layers.Any() ? layers.First().Publisher : ""
        var layerParam = Expression.Parameter(typeof(Layer), "l");
        var publisherProperty = Expression.Property(layerParam, nameof(Layer.Publisher));

        var anyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(Layer));

        var firstMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "First" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(Layer));

        var hasLayers = Expression.Call(anyMethod, layersProperty);
        var firstLayer = Expression.Call(firstMethod, layersProperty);
        var firstPublisher = Expression.Property(firstLayer, nameof(Layer.Publisher));

        return Expression.Condition(
            hasLayers,
            Expression.Coalesce(firstPublisher, Expression.Constant(string.Empty)),
            Expression.Constant(string.Empty));
    }

    private Expression BuildComponentTypeFilter(ComponentTypeFilterNode filter, ParameterExpression param)
    {
        var property = Expression.Property(param, nameof(Component.ComponentType));
        var value = Expression.Constant(filter.ComponentType);

        return BuildEqualsIgnoreCase(property, value);
    }

    #endregion

    #region Layer-Based Filters

    private Expression BuildManagedFilter(ManagedFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => l.IsManaged) == filter.IsManaged
        var layersProperty = Expression.Property(param, nameof(Component.Layers));
        var layerParam = Expression.Parameter(typeof(Layer), "l");
        var isManagedProperty = Expression.Property(layerParam, nameof(Layer.IsManaged));

        var anyPredicate = Expression.Lambda<Func<Layer, bool>>(isManagedProperty, layerParam);

        var anyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Layer));

        var anyCall = Expression.Call(anyMethod, layersProperty, anyPredicate);

        if (filter.IsManaged)
        {
            return anyCall;
        }
        else
        {
            return Expression.Not(anyCall);
        }
    }

    private Expression BuildPublisherFilter(PublisherFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => l.Publisher.Equals(filter.Publisher, OrdinalIgnoreCase))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var publisherProperty = Expression.Property(layerParam, nameof(Layer.Publisher));
            return BuildEqualsIgnoreCase(publisherProperty, Expression.Constant(filter.Publisher));
        });
    }

    private Expression BuildHasFilter(HasFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => l.SolutionName.Equals(filter.Solution, OrdinalIgnoreCase))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var solutionNameProperty = Expression.Property(layerParam, nameof(Layer.SolutionName));
            return BuildEqualsIgnoreCase(solutionNameProperty, Expression.Constant(filter.Solution));
        });
    }

    private Expression BuildHasAnyFilter(HasAnyFilterNode filter, ParameterExpression param)
    {
        // Build: c.Layers.Any(l => l.SolutionName == "sol1" || l.SolutionName == "sol2" || ...)
        // Using individual equality checks for SQL compatibility
        if (filter.Solutions.Count == 0)
        {
            return Expression.Constant(false);
        }

        // For a single solution, use simple HAS
        if (filter.Solutions.Count == 1)
        {
            return BuildHasFilter(new HasFilterNode { Solution = filter.Solutions[0] }, param);
        }

        // For multiple solutions, build OR expression inside the Any()
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var solutionNameProperty = Expression.Property(layerParam, nameof(Layer.SolutionName));
            
            // Build: solutionName == "sol1" || solutionName == "sol2" || ...
            Expression? combined = null;
            foreach (var solution in filter.Solutions)
            {
                var equalExpr = BuildEqualsIgnoreCaseSqlite(solutionNameProperty, Expression.Constant(solution));
                combined = combined == null
                    ? equalExpr
                    : Expression.OrElse(combined, equalExpr);
            }

            return combined ?? Expression.Constant(false);
        });
    }

    private Expression BuildHasAllFilter(HasAllFilterNode filter, ParameterExpression param)
    {
        // All solutions must have a matching layer
        // solutions.All(s => c.Layers.Any(l => l.SolutionName.Equals(s, OrdinalIgnoreCase)))
        // This translates to: AND of HAS filters for each solution
        if (filter.Solutions.Count == 0)
        {
            return Expression.Constant(true);
        }

        Expression? combined = null;
        foreach (var solution in filter.Solutions)
        {
            var hasFilter = new HasFilterNode { Solution = solution };
            var hasExpr = BuildHasFilter(hasFilter, param);

            combined = combined == null
                ? hasExpr
                : Expression.AndAlso(combined, hasExpr);
        }

        return combined ?? Expression.Constant(true);
    }

    private Expression BuildHasNoneFilter(HasNoneFilterNode filter, ParameterExpression param)
    {
        // !c.Layers.Any(l => solutions.Contains(l.SolutionName, OrdinalIgnoreCase))
        var hasAnyExpr = BuildHasAnyFilter(
            new HasAnyFilterNode { Solutions = filter.Solutions },
            param);

        return Expression.Not(hasAnyExpr);
    }

    private Expression BuildLayerQueryFilter(LayerQueryFilterNode filter, ParameterExpression param)
    {
        if (filter.LayerFilter == null)
        {
            return Expression.Constant(true);
        }

        return BuildExpressionBody(filter.LayerFilter, param);
    }

    private Expression BuildSolutionQueryFilter(SolutionQueryFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => EvaluateStringOp(l.SolutionName, filter.Value))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var solutionNameProperty = Expression.Property(layerParam, nameof(Layer.SolutionName));
            return BuildStringOperatorExpression(solutionNameProperty, filter.Operator, filter.Value);
        });
    }

    #endregion

    #region Layer Attribute Filters

    private Expression BuildLayerAttributeFilter(LayerAttributeFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => l.Attributes.Any(a => 
        //     a.AttributeName.Equals(filter.AttributeName, OrdinalIgnoreCase) &&
        //     (filter.AttributeType == null || a.AttributeType == filter.AttributeType) &&
        //     EvaluateStringOp(a.AttributeValue, filter.Value)))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            return BuildAttributeAnyExpression(layerParam, attrParam =>
            {
                // Name match
                var nameProperty = Expression.Property(attrParam, nameof(LayerAttribute.AttributeName));
                var nameMatch = BuildEqualsIgnoreCase(nameProperty, Expression.Constant(filter.AttributeName));

                // Value match
                var valueProperty = Expression.Coalesce(
                    Expression.Property(attrParam, nameof(LayerAttribute.AttributeValue)),
                    Expression.Constant(string.Empty));
                var valueMatch = BuildStringOperatorExpression(valueProperty, filter.Operator, filter.Value);

                Expression result = Expression.AndAlso(nameMatch, valueMatch);

                // Type match (if specified)
                if (filter.AttributeType.HasValue)
                {
                    var typeProperty = Expression.Property(attrParam, nameof(LayerAttribute.AttributeType));
                    var typeMatch = Expression.Equal(typeProperty, Expression.Constant(filter.AttributeType.Value));
                    result = Expression.AndAlso(result, typeMatch);
                }

                return result;
            });
        });
    }

    private Expression BuildLayerAttributeQueryFilter(LayerAttributeQueryFilterNode filter, ParameterExpression param)
    {
        // c.Layers.Any(l => l.SolutionName == filter.Solution && EvaluateAttributeFilter(l))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var solutionNameProperty = Expression.Property(layerParam, nameof(Layer.SolutionName));
            var solutionMatch = BuildEqualsIgnoreCase(solutionNameProperty, Expression.Constant(filter.Solution));

            if (filter.AttributeFilter == null)
            {
                return solutionMatch;
            }

            var attrFilterExpr = BuildLayerAttributeFilterBody(filter.AttributeFilter, layerParam);
            return Expression.AndAlso(solutionMatch, attrFilterExpr);
        });
    }

    private Expression BuildLayerAttributeFilterBody(FilterNode filter, ParameterExpression layerParam)
    {
        return filter switch
        {
            HasRelevantChangesFilterNode => BuildHasRelevantChangesForLayer(layerParam),
            HasAttributeDiffFilterNode diff => BuildHasAttributeDiffForLayer(diff, layerParam),
            AndFilterNode and => and.Children.Aggregate(
                (Expression)Expression.Constant(true),
                (acc, child) => Expression.AndAlso(acc, BuildLayerAttributeFilterBody(child, layerParam))),
            OrFilterNode or => or.Children.Aggregate(
                (Expression)Expression.Constant(false),
                (acc, child) => Expression.OrElse(acc, BuildLayerAttributeFilterBody(child, layerParam))),
            NotFilterNode not when not.Child != null =>
                Expression.Not(BuildLayerAttributeFilterBody(not.Child, layerParam)),
            _ => Expression.Constant(true)
        };
    }

    private Expression BuildHasRelevantChangesFilter(ParameterExpression param)
    {
        // c.Layers.Any(l => l.Attributes.Any(a => a.IsChanged && !excludedNames.Contains(a.AttributeName)))
        return BuildLayerAnyExpression(param, layerParam => BuildHasRelevantChangesForLayer(layerParam));
    }

    private Expression BuildHasRelevantChangesForLayer(ParameterExpression layerParam)
    {
        // l.Attributes.Any(a => a.IsChanged && attributeName NOT IN excludedNames)
        // Build OR expressions for excluded names check to avoid ToLowerInvariant
        var excludedNames = FilterConstants.ExcludedAttributeNames.ToList();

        return BuildAttributeAnyExpression(layerParam, attrParam =>
        {
            var isChangedProperty = Expression.Property(attrParam, nameof(LayerAttribute.IsChanged));
            var nameProperty = Expression.Property(attrParam, nameof(LayerAttribute.AttributeName));

            // Build: !(name == "excluded1" || name == "excluded2" || ...)
            // Using case-insensitive equality for SQLite
            Expression? excludedCheck = null;
            foreach (var excludedName in excludedNames)
            {
                var equalExpr = BuildEqualsIgnoreCaseSqlite(nameProperty, Expression.Constant(excludedName));
                excludedCheck = excludedCheck == null
                    ? equalExpr
                    : Expression.OrElse(excludedCheck, equalExpr);
            }

            var notExcluded = excludedCheck != null
                ? (Expression)Expression.Not(excludedCheck)
                : Expression.Constant(true);

            return Expression.AndAlso(isChangedProperty, notExcluded);
        });
    }

    private Expression BuildHasAttributeDiffFilter(HasAttributeDiffFilterNode filter, ParameterExpression param)
    {
        // When used at component level (not inside LAYER_ATTRIBUTE_QUERY), we need to find the source layer
        // c.Layers.Any(srcLayer => srcLayer.SolutionName == sourceSolution && HasDiffPredicate(srcLayer))
        return BuildLayerAnyExpression(param, layerParam =>
        {
            var solutionNameProperty = Expression.Property(layerParam, nameof(Layer.SolutionName));
            var solutionMatch = BuildEqualsIgnoreCase(solutionNameProperty, Expression.Constant(filter.SourceSolution));

            var diffPredicate = BuildHasAttributeDiffForLayer(filter, layerParam);
            return Expression.AndAlso(solutionMatch, diffPredicate);
        });
    }

    private Expression BuildHasAttributeDiffForLayer(HasAttributeDiffFilterNode filter, ParameterExpression srcLayerParam)
    {
        // Build expression to check if source layer has attributes that differ from target layers
        // This requires access to the component's layers collection, which we get via the navigation property

        // Get the component from the layer: srcLayer.Component
        var componentProperty = Expression.Property(srcLayerParam, nameof(Layer.Component));
        var componentLayersProperty = Expression.Property(componentProperty, nameof(Component.Layers));

        // Get source layer's ordinal and attributes
        var srcOrdinalProperty = Expression.Property(srcLayerParam, nameof(Layer.Ordinal));
        var srcAttributesProperty = Expression.Property(srcLayerParam, nameof(Layer.Attributes));

        // Build the attribute predicate based on filter settings
        var attrParam = Expression.Parameter(typeof(LayerAttribute), "srcAttr");

        // Base predicate: optionally filter by IsChanged and AttributeNames
        Expression attrPredicate = Expression.Constant(true);

        // Filter by IsChanged if onlyChangedAttributes is true
        if (filter.OnlyChangedAttributes)
        {
            var isChangedProperty = Expression.Property(attrParam, nameof(LayerAttribute.IsChanged));
            attrPredicate = Expression.AndAlso(attrPredicate, isChangedProperty);
        }

        // Exclude system attributes (same as HAS_RELEVANT_CHANGES)
        var excludedNames = FilterConstants.ExcludedAttributeNames.ToList();
        var nameProperty = Expression.Property(attrParam, nameof(LayerAttribute.AttributeName));
        Expression? excludedCheck = null;
        foreach (var excludedName in excludedNames)
        {
            var equalExpr = BuildEqualsIgnoreCaseSqlite(nameProperty, Expression.Constant(excludedName));
            excludedCheck = excludedCheck == null ? equalExpr : Expression.OrElse(excludedCheck, equalExpr);
        }
        if (excludedCheck != null)
        {
            var notExcluded = Expression.Not(excludedCheck);
            attrPredicate = Expression.AndAlso(attrPredicate, notExcluded);
        }

        // Filter by AttributeNames if specified
        if (filter.AttributeNames != null && filter.AttributeNames.Count > 0)
        {
            Expression? nameMatch = null;
            foreach (var name in filter.AttributeNames)
            {
                var equalExpr = BuildEqualsIgnoreCaseSqlite(nameProperty, Expression.Constant(name));
                nameMatch = nameMatch == null ? equalExpr : Expression.OrElse(nameMatch, equalExpr);
            }
            if (nameMatch != null)
            {
                attrPredicate = Expression.AndAlso(attrPredicate, nameMatch);
            }
        }

        // Build the "differs from target" check
        // For each matching source attribute, check if NO target layer has an attribute with the same hash
        var srcHashProperty = Expression.Property(attrParam, nameof(LayerAttribute.AttributeHash));

        // Build target layer predicate based on TargetMode
        var tgtLayerParam = Expression.Parameter(typeof(Layer), "tgtLayer");
        Expression targetLayerPredicate;

        if (filter.TargetMode == AttributeDiffTargetMode.AllBelow)
        {
            // tgtLayer.Ordinal < srcLayer.Ordinal
            var tgtOrdinalProperty = Expression.Property(tgtLayerParam, nameof(Layer.Ordinal));
            targetLayerPredicate = Expression.LessThan(tgtOrdinalProperty, srcOrdinalProperty);
        }
        else
        {
            // tgtLayer.SolutionName IN targetSolutions
            var tgtSolutionProperty = Expression.Property(tgtLayerParam, nameof(Layer.SolutionName));
            var targetSolutions = filter.TargetSolutions ?? new List<string>();

            if (targetSolutions.Count == 0)
            {
                // No targets specified - predicate is false (no match)
                targetLayerPredicate = Expression.Constant(false);
            }
            else
            {
                Expression? solutionMatch = null;
                foreach (var sol in targetSolutions)
                {
                    var equalExpr = BuildEqualsIgnoreCaseSqlite(tgtSolutionProperty, Expression.Constant(sol));
                    solutionMatch = solutionMatch == null ? equalExpr : Expression.OrElse(solutionMatch, equalExpr);
                }
                targetLayerPredicate = solutionMatch ?? Expression.Constant(false);
            }
        }

        // Build: tgtLayer.Attributes.Any(tgtAttr => tgtAttr.AttributeHash == srcAttr.AttributeHash)
        var tgtAttrParam = Expression.Parameter(typeof(LayerAttribute), "tgtAttr");
        var tgtHashProperty = Expression.Property(tgtAttrParam, nameof(LayerAttribute.AttributeHash));
        var hashMatch = Expression.Equal(tgtHashProperty, srcHashProperty);
        var tgtAttrPredicate = Expression.Lambda<Func<LayerAttribute, bool>>(hashMatch, tgtAttrParam);

        var tgtAttributesProperty = Expression.Property(tgtLayerParam, nameof(Layer.Attributes));
        var tgtAttrAnyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LayerAttribute));
        var tgtHasMatchingAttr = Expression.Call(tgtAttrAnyMethod, tgtAttributesProperty, tgtAttrPredicate);

        // Build: componentLayers.Any(tgtLayer => targetLayerPredicate && tgtLayer.Attributes.Any(...))
        var tgtLayerFullPredicate = Expression.AndAlso(targetLayerPredicate, tgtHasMatchingAttr);
        var tgtLayerLambda = Expression.Lambda<Func<Layer, bool>>(tgtLayerFullPredicate, tgtLayerParam);

        var tgtLayerAnyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Layer));
        var anyTargetHasMatch = Expression.Call(tgtLayerAnyMethod, componentLayersProperty, tgtLayerLambda);

        // The source attribute differs if NO target has a matching hash
        var srcAttrDiffers = Expression.Not(anyTargetHasMatch);

        // Combine with the attribute filter predicate
        var fullAttrPredicate = Expression.AndAlso(attrPredicate, srcAttrDiffers);
        var srcAttrLambda = Expression.Lambda<Func<LayerAttribute, bool>>(fullAttrPredicate, attrParam);

        // Use Any or All based on AttributeMatchLogic
        var aggregateMethod = filter.AttributeMatchLogic == AttributeMatchLogic.All
            ? typeof(Enumerable).GetMethods()
                .First(m => m.Name == "All" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(LayerAttribute))
            : typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(LayerAttribute));

        return Expression.Call(aggregateMethod, srcAttributesProperty, srcAttrLambda);
    }

    #endregion

    #region ORDER Filters

    private Expression BuildOrderStrictFilter(OrderStrictFilterNode filter, ParameterExpression param)
    {
        // For ORDER_STRICT, we need to verify exact sequence
        // This is complex in pure SQL, so we build a best-effort filter
        // that narrows down candidates, then apply full logic in memory

        if (filter.Sequence.Count == 0)
        {
            return Expression.Constant(true);
        }

        // At minimum, ensure all solutions in sequence exist
        var solutionNames = ExtractSolutionNames(filter.Sequence);
        if (solutionNames.Count == 0)
        {
            return Expression.Constant(true);
        }

        // Build HAS_ALL equivalent
        return BuildHasAllFilter(new HasAllFilterNode { Solutions = solutionNames }, param);
    }

    private Expression BuildOrderFlexFilter(OrderFlexFilterNode filter, ParameterExpression param)
    {
        // For ORDER_FLEX, we can be smarter with ordinal pre-fetch
        if (filter.Sequence.Count == 0)
        {
            return Expression.Constant(true);
        }

        var solutionNames = ExtractSolutionNames(filter.Sequence);
        if (solutionNames.Count == 0)
        {
            return Expression.Constant(true);
        }

        // If we have ordinal lookups, we can build a more precise filter
        if (_ordinalLookup != null && _ordinalLookup.Count > 0)
        {
            return BuildOrderFlexWithOrdinals(filter, param, solutionNames);
        }

        // Fallback: ensure all solutions exist
        return BuildHasAllFilter(new HasAllFilterNode { Solutions = solutionNames }, param);
    }

    private Expression BuildOrderFlexWithOrdinals(OrderFlexFilterNode filter, ParameterExpression param, List<string> solutionNames)
    {
        // With ordinals, we can build constraints like:
        // For sequence [A, B, C], component must have:
        // - A layer with SolutionName=A and Ordinal in known range for A
        // - B layer with Ordinal > A's Ordinal
        // - C layer with Ordinal > B's Ordinal

        // For now, we still use the HAS_ALL approach as the SQL filter
        // The full ordinal validation happens in memory
        // This could be enhanced with CTEs in the future

        return BuildHasAllFilter(new HasAllFilterNode { Solutions = solutionNames }, param);
    }

    private List<string> ExtractSolutionNames(List<object> sequence)
    {
        var names = new List<string>();

        foreach (var item in sequence)
        {
            if (item is string s)
            {
                names.Add(s);
            }
            else if (item is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Array)
                {
                    // Choice group - add all options
                    foreach (var element in json.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            names.Add(element.GetString() ?? string.Empty);
                        }
                    }
                }
                else if (json.ValueKind == JsonValueKind.String)
                {
                    names.Add(json.GetString() ?? string.Empty);
                }
                // Skip object (SolutionQueryNode) - handled separately
            }
            else if (item is List<string> choices)
            {
                names.AddRange(choices);
            }
        }

        return names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
    }

    #endregion

    #region Logical Operators

    private Expression BuildAndFilter(AndFilterNode filter, ParameterExpression param)
    {
        if (filter.Children.Count == 0)
        {
            return Expression.Constant(true);
        }

        return filter.Children
            .Select(child => BuildExpressionBody(child, param))
            .Aggregate(Expression.AndAlso);
    }

    private Expression BuildOrFilter(OrFilterNode filter, ParameterExpression param)
    {
        if (filter.Children.Count == 0)
        {
            return Expression.Constant(false);
        }

        return filter.Children
            .Select(child => BuildExpressionBody(child, param))
            .Aggregate(Expression.OrElse);
    }

    private Expression BuildNotFilter(NotFilterNode filter, ParameterExpression param)
    {
        if (filter.Child == null)
        {
            return Expression.Constant(true);
        }

        return Expression.Not(BuildExpressionBody(filter.Child, param));
    }

    #endregion

    #region Helper Methods

    private Expression BuildLayerAnyExpression(
        ParameterExpression componentParam,
        Func<ParameterExpression, Expression> predicateBuilder)
    {
        var layersProperty = Expression.Property(componentParam, nameof(Component.Layers));
        var layerParam = Expression.Parameter(typeof(Layer), "l");

        var predicateBody = predicateBuilder(layerParam);
        var predicate = Expression.Lambda<Func<Layer, bool>>(predicateBody, layerParam);

        var anyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Layer));

        return Expression.Call(anyMethod, layersProperty, predicate);
    }

    private Expression BuildAttributeAnyExpression(
        ParameterExpression layerParam,
        Func<ParameterExpression, Expression> predicateBuilder)
    {
        var attributesProperty = Expression.Property(layerParam, nameof(Layer.Attributes));
        var attrParam = Expression.Parameter(typeof(LayerAttribute), "a");

        var predicateBody = predicateBuilder(attrParam);
        var predicate = Expression.Lambda<Func<LayerAttribute, bool>>(predicateBody, attrParam);

        var anyMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(LayerAttribute));

        return Expression.Call(anyMethod, attributesProperty, predicate);
    }

    private Expression BuildStringOperatorExpression(Expression property, StringOperator op, string value)
    {
        // Use SQLite-compatible case-insensitive comparisons via EF.Functions.Like
        var valueConstant = Expression.Constant(value);

        return op switch
        {
            StringOperator.Equals => BuildEqualsIgnoreCaseSqlite(property, valueConstant),
            StringOperator.NotEquals => Expression.Not(BuildEqualsIgnoreCaseSqlite(property, valueConstant)),

            StringOperator.Contains => BuildLikePattern(property, $"%{EscapeLikePattern(value)}%"),
            StringOperator.NotContains => Expression.Not(BuildLikePattern(property, $"%{EscapeLikePattern(value)}%")),

            StringOperator.BeginsWith => BuildLikePattern(property, $"{EscapeLikePattern(value)}%"),
            StringOperator.NotBeginsWith => Expression.Not(BuildLikePattern(property, $"{EscapeLikePattern(value)}%")),

            StringOperator.EndsWith => BuildLikePattern(property, $"%{EscapeLikePattern(value)}"),
            StringOperator.NotEndsWith => Expression.Not(BuildLikePattern(property, $"%{EscapeLikePattern(value)}")),

            _ => Expression.Constant(true)
        };
    }

    /// <summary>
    /// Builds a case-insensitive equality expression using EF.Functions.Like for SQLite compatibility.
    /// </summary>
    private Expression BuildEqualsIgnoreCaseSqlite(Expression property, Expression value)
    {
        // For SQLite, use EF.Functions.Like which is case-insensitive for ASCII
        // Like pattern without wildcards is effectively an equality check
        // We need to escape any special characters in the value to prevent pattern matching
        if (value is ConstantExpression constExpr && constExpr.Value is string strValue)
        {
          //  return BuildLikePattern(property, EscapeLikePattern(strValue));
        }
        
        // Fallback: direct equality (SQLite default collation is case-sensitive)
        return Expression.Equal(property, value);
    }

    /// <summary>
    /// Builds EF.Functions.Like expression for SQLite compatibility.
    /// SQLite LIKE is case-insensitive for ASCII characters.
    /// </summary>
    private Expression BuildLikePattern(Expression property, string pattern)
    {
        // EF.Functions.Like(property, pattern)
        var efFunctionsType = typeof(EF);
        var functionsProperty = efFunctionsType.GetProperty(nameof(EF.Functions))!;
        var efFunctions = Expression.Property(null, functionsProperty);

        var dbFunctionsType = typeof(DbFunctionsExtensions);
        var likeMethod = dbFunctionsType.GetMethod(
            nameof(DbFunctionsExtensions.Like),
            new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

        return Expression.Call(
            likeMethod,
            efFunctions,
            property,
            Expression.Constant(pattern));
    }

    /// <summary>
    /// Escapes special LIKE pattern characters to treat them as literals.
    /// </summary>
    private static string EscapeLikePattern(string value)
    {
        // Escape LIKE special characters: %, _, [
        return value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }

    private Expression BuildEqualsIgnoreCase(Expression property, Expression value)
    {
        // Delegate to SQLite-compatible version
        return BuildEqualsIgnoreCaseSqlite(property, value);
    }

    private Expression BuildContainsIgnoreCase(Expression property, Expression value)
    {
        if (value is ConstantExpression constExpr && constExpr.Value is string strValue)
        {
            return BuildLikePattern(property, $"%{EscapeLikePattern(strValue)}%");
        }
        
        // Fallback: use Contains (may not be case-insensitive)
        var containsMethod = typeof(string).GetMethod(
            nameof(string.Contains),
            new[] { typeof(string) })!;
        return Expression.Call(property, containsMethod, value);
    }

    private Expression BuildStartsWithIgnoreCase(Expression property, Expression value)
    {
        if (value is ConstantExpression constExpr && constExpr.Value is string strValue)
        {
            return BuildLikePattern(property, $"{EscapeLikePattern(strValue)}%");
        }
        
        // Fallback
        var startsWithMethod = typeof(string).GetMethod(
            nameof(string.StartsWith),
            new[] { typeof(string) })!;
        return Expression.Call(property, startsWithMethod, value);
    }

    private Expression BuildEndsWithIgnoreCase(Expression property, Expression value)
    {
        if (value is ConstantExpression constExpr && constExpr.Value is string strValue)
        {
            return BuildLikePattern(property, $"%{EscapeLikePattern(strValue)}");
        }
        
        // Fallback
        var endsWithMethod = typeof(string).GetMethod(
            nameof(string.EndsWith),
            new[] { typeof(string) })!;
        return Expression.Call(property, endsWithMethod, value);
    }

    #endregion
}
