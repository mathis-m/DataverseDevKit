import { FilterNode } from '../types';

/**
 * Represents for the filter node format expected by the C# backend.
 * Key differences from UI FilterNode:
 * - No `id` property (UI-only concern)
 * - `NOT` nodes use `child` (singular) instead of `children` (array)
 */
export interface BackendFilterNode {
  type: string;
  solution?: string;
  solutions?: string[];
  sequence?: (string | string[])[];
  children?: BackendFilterNode[];
  child?: BackendFilterNode;
}

/**
 * Transforms a UI FilterNode to the format expected by the C# backend.
 * - Removes the `id` property
 * - Converts NOT nodes from `children` array to `child` singular
 */
export function transformFilterForBackend(filter: FilterNode | null): BackendFilterNode | null {
  if (!filter) {
    return null;
  }

  const { id, children, ...rest } = filter;

  const result: BackendFilterNode = { ...rest };

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
