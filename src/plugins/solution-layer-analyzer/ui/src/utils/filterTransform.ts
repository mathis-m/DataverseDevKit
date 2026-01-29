import { FilterNode, SolutionQueryNode, AttributeTarget, StringOperator } from '../types';

/**
 * Represents for the filter node format expected by the C# backend.
 * Key differences from UI FilterNode:
 * - No `id` property (UI-only concern)
 * - `NOT` nodes use `child` (singular) instead of `children` (array)
 * - Includes new filter types: attribute, operator, value for ATTRIBUTE filters
 * - Includes layerFilter for LAYER_QUERY
 */
export interface BackendFilterNode {
  type: string;
  solution?: string;
  solutions?: string[];
  sequence?: (string | string[] | SolutionQueryNode)[];
  children?: BackendFilterNode[];
  child?: BackendFilterNode;
  // ATTRIBUTE filter properties
  attribute?: AttributeTarget | string;
  operator?: StringOperator;
  value?: string;
  // MANAGED filter property
  isManaged?: boolean;
  // LAYER_QUERY property
  layerFilter?: BackendFilterNode;
}

/**
 * Transforms a UI FilterNode to the format expected by the C# backend.
 * - Removes the `id` property
 * - Converts NOT nodes from `children` array to `child` singular
 * - Handles MANAGED filter conversion (value string to isManaged boolean)
 * - Recursively transforms nested filters (e.g., layerFilter in LAYER_QUERY)
 */
export function transformFilterForBackend(filter: FilterNode | null): BackendFilterNode | null {
  if (!filter) {
    return null;
  }

  const { id, children, layerFilter, ...rest } = filter;

  const result: BackendFilterNode = { ...rest };

  // Handle MANAGED filter - convert string value to boolean
  if (filter.type === 'MANAGED' && filter.value) {
    result.isManaged = filter.value === 'true';
    delete result.value;
  }

  // Handle LAYER_QUERY - recursively transform the nested layer filter
  if (filter.type === 'LAYER_QUERY' && layerFilter) {
    result.layerFilter = transformFilterForBackend(layerFilter);
  }

  if (filter.type === 'NOT') {
    // NOT expects a single child, not an array
    if (children && children.length > 0) {
      result.child = transformFilterForBackend(children[0]) ?? undefined;
    }
  } else if (children && children.length > 0) {
    // AND, OR have children array
    result.children = children.map(c => transformFilterForBackend(c)!).filter(Boolean);
  }

  return result;
}
