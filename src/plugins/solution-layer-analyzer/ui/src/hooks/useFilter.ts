import { useMemo, useCallback } from 'react';
import { useAppStore } from '../store/useAppStore';
import { 
  FilterNode, 
  SimpleFilterValues, 
  FilterComplexityInfo, 
  AttributeTarget, 
  StringOperator 
} from '../types';
import { useDebouncedValue } from './useDebounce';

const FILTER_DEBOUNCE_MS = 300;

// Source IDs for correlating simple filter controls with AST nodes
const SOURCE_ID = {
  SEARCH: 'simple-search',
  TYPES: 'simple-types',
  SOLUTIONS: 'simple-solutions',
  MANAGED: 'simple-managed',
} as const;

/**
 * Extracts simple filter values from a FilterNode AST.
 * Walks the tree looking for nodes with known sourceIds.
 */
function extractSimpleFilters(filter: FilterNode | null): SimpleFilterValues {
  const result: SimpleFilterValues = {
    searchText: '',
    selectedTypes: [],
    selectedSolutions: [],
    managedFilter: 'all',
  };

  if (!filter) return result;

  const walk = (node: FilterNode) => {
    // Check for simple filter sourceIds
    if (node.sourceId === SOURCE_ID.SEARCH) {
      // Search is stored as OR of ATTRIBUTE nodes, but the parent OR has the sourceId
      // Extract from the first child's value (they all have same search text)
      if (node.type === 'OR' && node.children && node.children.length > 0) {
        result.searchText = node.children[0].value || '';
      } else if (node.type === 'ATTRIBUTE') {
        result.searchText = node.value || '';
      }
    }

    if (node.sourceId === SOURCE_ID.TYPES) {
      // Types can be single ATTRIBUTE or OR of ATTRIBUTEs
      if (node.type === 'ATTRIBUTE' && node.value) {
        result.selectedTypes = [node.value];
      } else if (node.type === 'OR' && node.children) {
        result.selectedTypes = node.children
          .filter(c => c.type === 'ATTRIBUTE' && c.attribute === AttributeTarget.ComponentType)
          .map(c => c.value || '')
          .filter(Boolean);
      }
    }

    if (node.sourceId === SOURCE_ID.SOLUTIONS) {
      // Solutions are stored as HAS_ANY
      if (node.type === 'HAS_ANY' && node.solutions) {
        result.selectedSolutions = [...node.solutions];
      }
    }

    if (node.sourceId === SOURCE_ID.MANAGED) {
      // Managed is stored as MANAGED node
      if (node.type === 'MANAGED') {
        result.managedFilter = node.value === 'true' ? 'managed' : 'unmanaged';
      }
    }

    // Recurse into children
    if (node.children) {
      node.children.forEach(walk);
    }
    if (node.layerFilter) {
      walk(node.layerFilter);
    }
  };

  walk(filter);
  return result;
}

/**
 * Analyzes filter complexity to determine what can be shown in simple mode.
 */
function analyzeFilterComplexity(filter: FilterNode | null): FilterComplexityInfo {
  const hiddenConditions: string[] = [];
  
  if (!filter) {
    return { isSimpleRepresentable: true, hiddenConditions: [] };
  }

  const walk = (node: FilterNode, depth: number = 0) => {
    // Skip nodes that are from simple filter (have sourceId)
    if (node.sourceId && Object.values(SOURCE_ID).includes(node.sourceId as any)) {
      return;
    }

    // Complex conditions that can't be represented in simple mode
    switch (node.type) {
      case 'LAYER_QUERY':
        hiddenConditions.push('Layer query filter');
        break;
      case 'ORDER_STRICT':
        hiddenConditions.push('Strict layer order constraint');
        break;
      case 'ORDER_FLEX':
        hiddenConditions.push('Flexible layer order constraint');
        break;
      case 'HAS':
        if (!node.sourceId) {
          hiddenConditions.push(`Layer has solution: ${node.solution}`);
        }
        break;
      case 'HAS_ALL':
        hiddenConditions.push(`Layer has all solutions: ${node.solutions?.join(', ')}`);
        break;
      case 'HAS_NONE':
        hiddenConditions.push(`Layer has none of: ${node.solutions?.join(', ')}`);
        break;
      case 'SOLUTION_QUERY':
        hiddenConditions.push(`Solution query: ${node.attribute} ${node.operator} "${node.value}"`);
        break;
      case 'NOT':
        if (!node.sourceId) {
          hiddenConditions.push('NOT condition');
        }
        break;
      case 'ATTRIBUTE':
        // ATTRIBUTE nodes without sourceId are custom advanced filters
        if (!node.sourceId) {
          hiddenConditions.push(`Attribute filter: ${node.attribute} ${node.operator} "${node.value}"`);
        }
        break;
      case 'AND':
      case 'OR':
        // These are OK if they contain simple filters, recurse to check children
        break;
    }

    // Recurse
    if (node.children) {
      node.children.forEach(c => walk(c, depth + 1));
    }
    if (node.layerFilter) {
      walk(node.layerFilter, depth + 1);
    }
  };

  walk(filter);

  return {
    isSimpleRepresentable: hiddenConditions.length === 0,
    hiddenConditions,
  };
}

/**
 * Creates a FilterNode AST from simple filter values.
 * Each simple filter gets a sourceId for correlation.
 */
function buildFilterFromSimpleValues(values: SimpleFilterValues): FilterNode | null {
  const conditions: FilterNode[] = [];

  // Search filter -> OR of multiple ATTRIBUTE filters with sourceId on the OR
  if (values.searchText) {
    const searchConditions: FilterNode[] = [
      {
        type: 'ATTRIBUTE',
        id: `${SOURCE_ID.SEARCH}-logical`,
        attribute: AttributeTarget.LogicalName,
        operator: StringOperator.Contains,
        value: values.searchText,
      },
      {
        type: 'ATTRIBUTE',
        id: `${SOURCE_ID.SEARCH}-display`,
        attribute: AttributeTarget.DisplayName,
        operator: StringOperator.Contains,
        value: values.searchText,
      },
      {
        type: 'ATTRIBUTE',
        id: `${SOURCE_ID.SEARCH}-type`,
        attribute: AttributeTarget.ComponentType,
        operator: StringOperator.Contains,
        value: values.searchText,
      },
      {
        type: 'ATTRIBUTE',
        id: `${SOURCE_ID.SEARCH}-table`,
        attribute: AttributeTarget.TableLogicalName,
        operator: StringOperator.Contains,
        value: values.searchText,
      },
    ];
    conditions.push({
      type: 'OR',
      id: SOURCE_ID.SEARCH,
      sourceId: SOURCE_ID.SEARCH,
      children: searchConditions,
    });
  }

  // Type filter -> single ATTRIBUTE or OR of ATTRIBUTEs with sourceId
  if (values.selectedTypes.length > 0) {
    if (values.selectedTypes.length === 1) {
      conditions.push({
        type: 'ATTRIBUTE',
        id: SOURCE_ID.TYPES,
        sourceId: SOURCE_ID.TYPES,
        attribute: AttributeTarget.ComponentType,
        operator: StringOperator.Equals,
        value: values.selectedTypes[0],
      });
    } else {
      const typeConditions = values.selectedTypes.map((type, idx) => ({
        type: 'ATTRIBUTE',
        id: `${SOURCE_ID.TYPES}-${idx}`,
        attribute: AttributeTarget.ComponentType,
        operator: StringOperator.Equals,
        value: type,
      }));
      conditions.push({
        type: 'OR',
        id: SOURCE_ID.TYPES,
        sourceId: SOURCE_ID.TYPES,
        children: typeConditions,
      });
    }
  }

  // Solution filter -> HAS_ANY with sourceId
  if (values.selectedSolutions.length > 0) {
    conditions.push({
      type: 'HAS_ANY',
      id: SOURCE_ID.SOLUTIONS,
      sourceId: SOURCE_ID.SOLUTIONS,
      solutions: values.selectedSolutions,
    });
  }

  // Managed filter -> MANAGED node with sourceId
  if (values.managedFilter !== 'all') {
    conditions.push({
      type: 'MANAGED',
      id: SOURCE_ID.MANAGED,
      sourceId: SOURCE_ID.MANAGED,
      value: values.managedFilter === 'managed' ? 'true' : 'false',
    });
  }

  // Combine all conditions with AND
  if (conditions.length === 0) {
    return null;
  } else if (conditions.length === 1) {
    return conditions[0];
  } else {
    return {
      type: 'AND',
      id: 'root',
      children: conditions,
    };
  }
}

/**
 * Updates an existing filter AST with new simple filter values.
 * Preserves any advanced (non-simple) conditions while updating simple ones.
 */
function updateFilterWithSimpleValues(
  existingFilter: FilterNode | null,
  values: SimpleFilterValues
): FilterNode | null {
  // Build the simple filter portion
  const simpleFilter = buildFilterFromSimpleValues(values);
  
  if (!existingFilter) {
    return simpleFilter;
  }

  // Extract non-simple conditions from existing filter
  const nonSimpleConditions: FilterNode[] = [];
  
  const collectNonSimple = (node: FilterNode) => {
    // If this node has no sourceId and is not a container (AND/OR at root level), keep it
    if (node.sourceId && Object.values(SOURCE_ID).includes(node.sourceId as any)) {
      // Skip - this is a simple filter node that will be replaced
      return;
    }
    
    if (node.type === 'AND' || node.type === 'OR') {
      // For container nodes at root, recurse to find non-simple children
      if (node.id === 'root' || !node.sourceId) {
        node.children?.forEach(child => {
          if (child.sourceId && Object.values(SOURCE_ID).includes(child.sourceId as any)) {
            // Skip simple filter nodes
            return;
          }
          // Keep non-simple conditions
          nonSimpleConditions.push(child);
        });
      }
    } else if (!node.sourceId) {
      // Non-container, non-simple node - this is an advanced filter
      nonSimpleConditions.push(node);
    }
  };

  collectNonSimple(existingFilter);

  // Combine simple and non-simple conditions
  const allConditions: FilterNode[] = [];
  
  // Add simple conditions
  if (simpleFilter) {
    if (simpleFilter.type === 'AND' && simpleFilter.children) {
      allConditions.push(...simpleFilter.children);
    } else {
      allConditions.push(simpleFilter);
    }
  }
  
  // Add non-simple conditions
  allConditions.push(...nonSimpleConditions);

  if (allConditions.length === 0) {
    return null;
  } else if (allConditions.length === 1) {
    return allConditions[0];
  } else {
    return {
      type: 'AND',
      id: 'root',
      children: allConditions,
    };
  }
}

export interface UseFilterResult {
  /** The current filter AST (single source of truth) */
  filter: FilterNode | null;
  
  /** Simple filter values extracted from the AST */
  simpleValues: SimpleFilterValues;
  
  /** Information about filter complexity */
  complexityInfo: FilterComplexityInfo;
  
  /** Whether advanced mode is enabled */
  advancedMode: boolean;
  
  // Simple filter setters (update specific AST nodes)
  setSearchText: (value: string) => void;
  setSelectedTypes: (value: string[]) => void;
  setSelectedSolutions: (value: string[]) => void;
  setManagedFilter: (value: 'all' | 'managed' | 'unmanaged') => void;
  
  // Advanced filter setter (wholesale AST update)
  setFilter: (filter: FilterNode | null) => void;
  
  // Mode toggle
  setAdvancedMode: (enabled: boolean) => void;
  
  // Clear all filters
  clearFilter: () => void;
  
  /** Debounced filter for expensive operations (backend queries) */
  debouncedFilter: FilterNode | null;
}

/**
 * Unified filter hook that manages filter state with FilterNode as the single source of truth.
 * Simple filter UI is a projection of the AST; changes update specific nodes.
 */
export function useFilter(): UseFilterResult {
  const { filterBarState, setFilterBarState } = useAppStore();
  const { advancedMode, advancedFilter } = filterBarState;
  
  // The filter AST is the single source of truth
  const filter = advancedFilter;
  
  // Extract simple values from the current filter
  const simpleValues = useMemo(() => extractSimpleFilters(filter), [filter]);
  
  // Analyze complexity for UI display
  const complexityInfo = useMemo(() => analyzeFilterComplexity(filter), [filter]);
  
  // Debounced filter for backend queries
  const debouncedFilter = useDebouncedValue(filter, FILTER_DEBOUNCE_MS);

  // Internal setter that updates the store
  const setFilter = useCallback((newFilter: FilterNode | null) => {
    setFilterBarState({ advancedFilter: newFilter });
  }, [setFilterBarState]);

  // Simple filter setters - these update specific parts of the AST
  const updateSimpleFilter = useCallback((updates: Partial<SimpleFilterValues>) => {
    const currentSimple = extractSimpleFilters(filter);
    const newValues: SimpleFilterValues = {
      ...currentSimple,
      ...updates,
    };
    const newFilter = updateFilterWithSimpleValues(filter, newValues);
    setFilter(newFilter);
  }, [filter, setFilter]);

  const setSearchText = useCallback((value: string) => {
    updateSimpleFilter({ searchText: value });
  }, [updateSimpleFilter]);

  const setSelectedTypes = useCallback((value: string[]) => {
    updateSimpleFilter({ selectedTypes: value });
  }, [updateSimpleFilter]);

  const setSelectedSolutions = useCallback((value: string[]) => {
    updateSimpleFilter({ selectedSolutions: value });
  }, [updateSimpleFilter]);

  const setManagedFilter = useCallback((value: 'all' | 'managed' | 'unmanaged') => {
    updateSimpleFilter({ managedFilter: value });
  }, [updateSimpleFilter]);

  // Mode toggle
  const setAdvancedMode = useCallback((enabled: boolean) => {
    setFilterBarState({ advancedMode: enabled });
  }, [setFilterBarState]);

  // Clear all filters
  const clearFilter = useCallback(() => {
    setFilter(null);
  }, [setFilter]);

  return {
    filter,
    simpleValues,
    complexityInfo,
    advancedMode,
    setSearchText,
    setSelectedTypes,
    setSelectedSolutions,
    setManagedFilter,
    setFilter,
    setAdvancedMode,
    clearFilter,
    debouncedFilter,
  };
}
