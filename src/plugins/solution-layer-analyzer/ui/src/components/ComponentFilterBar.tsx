import React, { useMemo, useEffect, useState, useRef, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Input,
  Field,
  Dropdown,
  Option,
  Badge,
  Button,
  Spinner,
} from '@fluentui/react-components';
import {
  SearchRegular,
  DismissRegular,
  FilterRegular,
} from '@fluentui/react-icons';
import { ComponentResult, FilterNode, AttributeTarget, StringOperator } from '../types';
import { AdvancedFilterBuilder } from './AdvancedFilterBuilder';
import { useAppStore } from '../store/useAppStore';
import { useDebouncedValue } from '../hooks/useDebounce';

const FILTER_DEBOUNCE_MS = 300;

const useStyles = makeStyles({
  filterBar: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'flex-end',
    flexWrap: 'wrap',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  filterField: {
    flex: '1 1 200px',
    minWidth: '150px',
  },
  activeBadges: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
});

interface ComponentFilterBarProps {
  components: ComponentResult[];
  availableSolutions?: string[];
  onFilterChange: (filteredComponents: ComponentResult[]) => void;
  onAdvancedFilterChange?: (filter: FilterNode | null) => void;
  loading?: boolean;
}

export const ComponentFilterBar: React.FC<ComponentFilterBarProps> = ({
  components,
  availableSolutions,
  onFilterChange,
  onAdvancedFilterChange,
  loading,
}) => {
  const styles = useStyles();
  
  // Use store for persisted filter state
  const { filterBarState, setFilterBarState } = useAppStore();
  const {
    advancedMode,
    advancedFilter,
  } = filterBarState;

  // Local state for immediate UI responsiveness
  // These update immediately on user input without blocking
  const [localSearchText, setLocalSearchText] = useState(filterBarState.searchText);
  const [localSelectedTypes, setLocalSelectedTypes] = useState(filterBarState.selectedTypes);
  const [localSelectedSolutions, setLocalSelectedSolutions] = useState(filterBarState.selectedSolutions);
  const [localManagedFilter, setLocalManagedFilter] = useState(filterBarState.managedFilter);

  // Track if we've mounted to avoid syncing on initial render
  const isInitialMount = useRef(true);

  // Debounced values for expensive operations (backend query)
  const debouncedSearchText = useDebouncedValue(localSearchText, FILTER_DEBOUNCE_MS);
  const debouncedSelectedTypes = useDebouncedValue(localSelectedTypes, FILTER_DEBOUNCE_MS);
  const debouncedSelectedSolutions = useDebouncedValue(localSelectedSolutions, FILTER_DEBOUNCE_MS);
  const debouncedManagedFilter = useDebouncedValue(localManagedFilter, FILTER_DEBOUNCE_MS);

  // Setters that only update local state (immediate, non-blocking)
  const setSearchText = (value: string) => setLocalSearchText(value);
  const setSelectedTypes = (value: string[]) => setLocalSelectedTypes(value);
  const setSelectedSolutions = (value: string[]) => setLocalSelectedSolutions(value);
  const setManagedFilter = (value: string) => setLocalManagedFilter(value);
  const setAdvancedMode = useCallback((value: boolean) => setFilterBarState({ advancedMode: value }), [setFilterBarState]);
  const setAdvancedFilter = useCallback((value: FilterNode | null) => {
    console.log('[ComponentFilterBar] setAdvancedFilter called:', JSON.stringify(value, null, 2));
    setFilterBarState({ advancedFilter: value });
  }, [setFilterBarState]);

  // Sync debounced values back to store (for persistence)
  useEffect(() => {
    if (isInitialMount.current) {
      isInitialMount.current = false;
      return;
    }
    setFilterBarState({
      searchText: debouncedSearchText,
      selectedTypes: debouncedSelectedTypes,
      selectedSolutions: debouncedSelectedSolutions,
      managedFilter: debouncedManagedFilter,
    });
  }, [debouncedSearchText, debouncedSelectedTypes, debouncedSelectedSolutions, debouncedManagedFilter, setFilterBarState]);

  // Use local values for display/badges
  const searchText = localSearchText;
  const selectedTypes = localSelectedTypes;
  const selectedSolutions = localSelectedSolutions;
  const managedFilter = localManagedFilter;

  // All available component types (static list - not filtered by current results)
  // This ensures users can always filter by any component type, regardless of current results
  const allComponentTypes = useMemo(() => [
    'Entity',
    'Attribute',
    'SystemForm',
    'SavedQuery',
    'SavedQueryVisualization',
    'RibbonCustomization',
    'WebResource',
    'SDKMessageProcessingStep',
    'Workflow',
    'AppModule',
    'SiteMap',
    'OptionSet',
    'PluginAssembly',
    'PluginType',
    'ServiceEndpoint',
    'CustomAPI',
    'Report',
    'EmailTemplate',
    'Dashboard',
    'Chart',
  ].sort(), []);

  const uniqueSolutions = useMemo(() => {
    const solutions = new Set<string>();
    components.forEach(c => c.layerSequence.forEach(s => solutions.add(s)));
    return Array.from(solutions).sort();
  }, [components]);

  // Apply filters
  const filteredComponents = useMemo(() => {
    let filtered = components;

    // Search filter
    if (searchText) {
      const search = searchText.toLowerCase();
      filtered = filtered.filter(c =>
        c.logicalName.toLowerCase().includes(search) ||
        c.displayName?.toLowerCase().includes(search) ||
        c.componentType.toLowerCase().includes(search) ||
        c.tableLogicalName?.toLowerCase().includes(search)
      );
    }

    // Type filter
    if (selectedTypes.length > 0) {
      filtered = filtered.filter(c => selectedTypes.includes(c.componentType));
    }

    // Solution filter
    if (selectedSolutions.length > 0) {
      filtered = filtered.filter(c =>
        c.layerSequence.some(s => selectedSolutions.includes(s))
      );
    }

    // Managed filter
    if (managedFilter !== 'all') {
      const isManaged = managedFilter === 'managed';
      filtered = filtered.filter(c => c.isManaged === isManaged);
    }

    return filtered;
  }, [components, searchText, selectedTypes, selectedSolutions, managedFilter]);

  // Notify parent of filter changes
  useEffect(() => {
    onFilterChange(filteredComponents);
  }, [filteredComponents]); // eslint-disable-line react-hooks/exhaustive-deps

  // Convert simple filters to advanced filter AST using DEBOUNCED values
  // This prevents expensive backend queries on every keystroke
  const debouncedSimpleFiltersKey = useMemo(() => 
    JSON.stringify({ 
      searchText: debouncedSearchText, 
      selectedTypes: debouncedSelectedTypes, 
      selectedSolutions: debouncedSelectedSolutions, 
      managedFilter: debouncedManagedFilter 
    }),
    [debouncedSearchText, debouncedSelectedTypes, debouncedSelectedSolutions, debouncedManagedFilter]
  );
  
  const convertSimpleFiltersToAdvanced = useMemo((): FilterNode | null => {
    // Parse the key back to get values (ensures memoization works correctly)
    const { searchText: search, selectedTypes: types, selectedSolutions: solutions, managedFilter: managed } = 
      JSON.parse(debouncedSimpleFiltersKey);
    
    const conditions: FilterNode[] = [];

    // Search filter -> OR of multiple ATTRIBUTE filters
    if (search) {
      const searchConditions: FilterNode[] = [
        {
          type: 'ATTRIBUTE',
          id: 'search-logical',
          attribute: AttributeTarget.LogicalName,
          operator: StringOperator.Contains,
          value: search
        },
        {
          type: 'ATTRIBUTE',
          id: 'search-display',
          attribute: AttributeTarget.DisplayName,
          operator: StringOperator.Contains,
          value: search
        },
        {
          type: 'ATTRIBUTE',
          id: 'search-type',
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Contains,
          value: search
        },
        {
          type: 'ATTRIBUTE',
          id: 'search-table',
          attribute: AttributeTarget.TableLogicalName,
          operator: StringOperator.Contains,
          value: search
        }
      ];
      conditions.push({
        type: 'OR',
        id: 'search-or',
        children: searchConditions
      });
    }

    // Type filter -> OR of ATTRIBUTE filters for each type
    if (types.length > 0) {
      if (types.length === 1) {
        conditions.push({
          type: 'ATTRIBUTE',
          id: 'type-single',
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Equals,
          value: types[0]
        });
      } else {
        const typeConditions = types.map((type: string, idx: number) => ({
          type: 'ATTRIBUTE',
          id: `type-${idx}`,
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Equals,
          value: type
        }));
        conditions.push({
          type: 'OR',
          id: 'type-or',
          children: typeConditions
        });
      }
    }

    // Solution filter -> HAS_ANY
    if (solutions.length > 0) {
      conditions.push({
        type: 'HAS_ANY',
        id: 'solutions',
        solutions: solutions
      });
    }

    // Managed filter -> MANAGED node (using existing node type if available, otherwise ATTRIBUTE)
    if (managed !== 'all') {
      // For now, we'll use MANAGED node which exists in backend
      conditions.push({
        type: 'MANAGED',
        id: 'managed',
        // Note: The MANAGED node in backend expects isManaged boolean
        // This will need to be handled in the transform
        value: managed === 'managed' ? 'true' : 'false'
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
        children: conditions
      };
    }
  }, [debouncedSimpleFiltersKey]); // Use debounced key for stable dep

  // Combined advanced filter: user's explicit advanced filter OR simple filters converted to advanced
  const combinedAdvancedFilter = useMemo((): FilterNode | null => {
    if (advancedMode) {
      // In advanced mode, use only the explicit advanced filter (can be null initially)
      return advancedFilter;
    } else {
      // In simple mode, convert simple filters to advanced filter
      return convertSimpleFiltersToAdvanced;
    }
  }, [advancedMode, advancedFilter, convertSimpleFiltersToAdvanced]);

  // Notify parent when combined advanced filter changes (for backend query)
  // Use JSON stringification to avoid triggering on reference changes with same content
  const filterJson = useMemo(() => 
    combinedAdvancedFilter ? JSON.stringify(combinedAdvancedFilter) : null,
    [combinedAdvancedFilter]
  );
  
  useEffect(() => {
    // Update the store with the combined filter (for AnalysisTab to query with)
    // Only update if in simple mode - in advanced mode, user edits directly
    if (!advancedMode) {
      setAdvancedFilter(combinedAdvancedFilter);
    }
    // Also notify parent for backward compatibility
    onAdvancedFilterChange?.(combinedAdvancedFilter);
  }, [filterJson, advancedMode]); // eslint-disable-line react-hooks/exhaustive-deps

  const activeFilterCount = 
    (searchText ? 1 : 0) +
    (selectedTypes.length > 0 ? 1 : 0) +
    (selectedSolutions.length > 0 ? 1 : 0) +
    (managedFilter !== 'all' ? 1 : 0) +
    (advancedFilter ? 1 : 0);

  const clearFilters = () => {
    setSearchText('');
    setSelectedTypes([]);
    setSelectedSolutions([]);
    setManagedFilter('all');
    setAdvancedFilter(null);
  };

  return (
    <div>
      <div className={styles.filterBar}>
        <Field label="Search" className={styles.filterField}>
          <Input
            contentBefore={<SearchRegular />}
            value={searchText}
            onChange={(_, d) => setSearchText(d.value)}
            placeholder="Search components..."
          />
        </Field>

        {!advancedMode && (
          <>
            <Field label="Component Type" className={styles.filterField}>
              <Dropdown
                multiselect
                placeholder="All types"
                value={selectedTypes.length > 0 ? `${selectedTypes.length} selected` : 'All types'}
                selectedOptions={selectedTypes}
                onOptionSelect={(_, data) => {
                  setSelectedTypes(data.selectedOptions);
                }}
              >
                {allComponentTypes.map(type => (
                  <Option key={type} value={type}>{type}</Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Solution" className={styles.filterField}>
              <Dropdown
                multiselect
                placeholder="All solutions"
                value={selectedSolutions.length > 0 ? `${selectedSolutions.length} selected` : 'All solutions'}
                selectedOptions={selectedSolutions}
                onOptionSelect={(_, data) => {
                  setSelectedSolutions(data.selectedOptions);
                }}
              >
                {uniqueSolutions.map(solution => (
                  <Option key={solution} value={solution}>{solution}</Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Managed" className={styles.filterField}>
              <Dropdown
                value={managedFilter === 'all' ? 'All' : managedFilter === 'managed' ? 'Managed' : 'Unmanaged'}
                onOptionSelect={(_, data) => setManagedFilter(data.optionValue || 'all')}
              >
                <Option value="all">All</Option>
                <Option value="managed">Managed</Option>
                <Option value="unmanaged">Unmanaged</Option>
              </Dropdown>
            </Field>
          </>
        )}

        <Button
          icon={<FilterRegular />}
          appearance={advancedMode ? 'primary' : 'secondary'}
          onClick={() => setAdvancedMode(!advancedMode)}
        >
          {advancedMode ? 'Simple Filters' : 'Advanced Filters'}
        </Button>

        {activeFilterCount > 0 && (
          <Button
            icon={<DismissRegular />}
            onClick={clearFilters}
          >
            Clear Filters ({activeFilterCount})
          </Button>
        )}
      </div>

      {advancedMode && (
        <div style={{ marginTop: tokens.spacingVerticalM }}>
          <AdvancedFilterBuilder
            solutions={availableSolutions || uniqueSolutions}
            initialFilter={advancedFilter}
            onFilterChange={setAdvancedFilter}
          />
        </div>
      )}

      <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM, padding: tokens.spacingVerticalS }}>
        <Badge appearance="outline" color="informative">
          {loading ? <Spinner size="tiny" /> : `${filteredComponents.length} / ${components.length} components`}
        </Badge>
        {activeFilterCount > 0 && (
          <div className={styles.activeBadges}>
            {searchText && <Badge appearance="filled" color="brand">Search: {searchText}</Badge>}
            {selectedTypes.length > 0 && <Badge appearance="filled" color="success">{selectedTypes.length} types</Badge>}
            {selectedSolutions.length > 0 && <Badge appearance="filled" color="warning">{selectedSolutions.length} solutions</Badge>}
            {managedFilter !== 'all' && <Badge appearance="filled" color="important">{managedFilter}</Badge>}
            {advancedFilter && <Badge appearance="filled" color="danger">Advanced filter active</Badge>}
          </div>
        )}
      </div>
    </div>
  );
};
