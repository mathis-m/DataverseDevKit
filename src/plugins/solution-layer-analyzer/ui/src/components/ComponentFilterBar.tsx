import React, { useMemo, useEffect } from 'react';
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
  
  // Use store for filter state
  const { filterBarState, setFilterBarState } = useAppStore();
  const {
    searchText,
    selectedTypes,
    selectedSolutions,
    managedFilter,
    advancedMode,
    advancedFilter,
  } = filterBarState;

  // Setters that update the store
  const setSearchText = (value: string) => setFilterBarState({ searchText: value });
  const setSelectedTypes = (value: string[]) => setFilterBarState({ selectedTypes: value });
  const setSelectedSolutions = (value: string[]) => setFilterBarState({ selectedSolutions: value });
  const setManagedFilter = (value: string) => setFilterBarState({ managedFilter: value });
  const setAdvancedMode = (value: boolean) => setFilterBarState({ advancedMode: value });
  const setAdvancedFilter = (value: FilterNode | null) => setFilterBarState({ advancedFilter: value });

  // Extract unique values for filters
  const uniqueTypes = useMemo(() => {
    const types = new Set<string>();
    components.forEach(c => types.add(c.componentType));
    return Array.from(types).sort();
  }, [components]);

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
  }, [filteredComponents, onFilterChange]);

  // Convert simple filters to advanced filter AST
  const convertSimpleFiltersToAdvanced = useMemo((): FilterNode | null => {
    const conditions: FilterNode[] = [];

    // Search filter -> OR of multiple ATTRIBUTE filters
    if (searchText) {
      const searchConditions: FilterNode[] = [
        {
          type: 'ATTRIBUTE',
          id: `search-logical-${Date.now()}`,
          attribute: AttributeTarget.LogicalName,
          operator: StringOperator.Contains,
          value: searchText
        },
        {
          type: 'ATTRIBUTE',
          id: `search-display-${Date.now()}`,
          attribute: AttributeTarget.DisplayName,
          operator: StringOperator.Contains,
          value: searchText
        },
        {
          type: 'ATTRIBUTE',
          id: `search-type-${Date.now()}`,
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Contains,
          value: searchText
        },
        {
          type: 'ATTRIBUTE',
          id: `search-table-${Date.now()}`,
          attribute: AttributeTarget.TableLogicalName,
          operator: StringOperator.Contains,
          value: searchText
        }
      ];
      conditions.push({
        type: 'OR',
        id: `search-or-${Date.now()}`,
        children: searchConditions
      });
    }

    // Type filter -> OR of ATTRIBUTE filters for each type
    if (selectedTypes.length > 0) {
      if (selectedTypes.length === 1) {
        conditions.push({
          type: 'ATTRIBUTE',
          id: `type-${Date.now()}`,
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Equals,
          value: selectedTypes[0]
        });
      } else {
        const typeConditions = selectedTypes.map(type => ({
          type: 'ATTRIBUTE',
          id: `type-${type}-${Date.now()}`,
          attribute: AttributeTarget.ComponentType,
          operator: StringOperator.Equals,
          value: type
        }));
        conditions.push({
          type: 'OR',
          id: `type-or-${Date.now()}`,
          children: typeConditions
        });
      }
    }

    // Solution filter -> HAS_ANY
    if (selectedSolutions.length > 0) {
      conditions.push({
        type: 'HAS_ANY',
        id: `solutions-${Date.now()}`,
        solutions: selectedSolutions
      });
    }

    // Managed filter -> MANAGED node (using existing node type if available, otherwise ATTRIBUTE)
    if (managedFilter !== 'all') {
      // For now, we'll use MANAGED node which exists in backend
      conditions.push({
        type: 'MANAGED',
        id: `managed-${Date.now()}`,
        // Note: The MANAGED node in backend expects isManaged boolean
        // This will need to be handled in the transform
        value: managedFilter === 'managed' ? 'true' : 'false'
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
        id: `root-${Date.now()}`,
        children: conditions
      };
    }
  }, [searchText, selectedTypes, selectedSolutions, managedFilter]);

  // Combined advanced filter: user's explicit advanced filter OR simple filters converted to advanced
  const combinedAdvancedFilter = useMemo((): FilterNode | null => {
    if (advancedMode && advancedFilter) {
      // In advanced mode, use only the explicit advanced filter
      return advancedFilter;
    } else {
      // In simple mode, convert simple filters to advanced filter
      return convertSimpleFiltersToAdvanced;
    }
  }, [advancedMode, advancedFilter, convertSimpleFiltersToAdvanced]);

  // Notify parent when combined advanced filter changes (for backend query)
  useEffect(() => {
    onAdvancedFilterChange?.(combinedAdvancedFilter);
  }, [combinedAdvancedFilter, onAdvancedFilterChange]);

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
                {uniqueTypes.map(type => (
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
