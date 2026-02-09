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
  Tooltip,
} from '@fluentui/react-components';
import {
  SearchRegular,
  DismissRegular,
  FilterRegular,
  InfoRegular,
  SaveRegular,
} from '@fluentui/react-icons';
import { ComponentResult, FilterNode } from '../types';
import { AdvancedFilterBuilder } from './AdvancedFilterBuilder';
import { useFilter } from '../hooks/useFilter';

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
  hiddenFiltersIndicator: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
});

interface ComponentFilterBarProps {
  components: ComponentResult[];
  availableSolutions?: string[];
  onFilterChange: (filteredComponents: ComponentResult[]) => void;
  onAdvancedFilterChange?: (filter: FilterNode | null) => void;
  onSaveToReport?: () => void;
  loading?: boolean;
}

export const ComponentFilterBar: React.FC<ComponentFilterBarProps> = ({
  components,
  availableSolutions,
  onFilterChange,
  onAdvancedFilterChange,
  onSaveToReport,
  loading,
}) => {
  const styles = useStyles();
  
  // Use the unified filter hook - single source of truth
  const {
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
  } = useFilter();

  // Destructure simple values for easy access
  const { searchText, selectedTypes, selectedSolutions, managedFilter } = simpleValues;

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

  // Notify parent when filter changes (for backend query)
  useEffect(() => {
    onAdvancedFilterChange?.(debouncedFilter);
  }, [debouncedFilter]); // eslint-disable-line react-hooks/exhaustive-deps

  // Count active filters (simple + hidden advanced)
  const activeFilterCount = 
    (searchText ? 1 : 0) +
    (selectedTypes.length > 0 ? 1 : 0) +
    (selectedSolutions.length > 0 ? 1 : 0) +
    (managedFilter !== 'all' ? 1 : 0) +
    complexityInfo.hiddenConditions.length;

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
                onOptionSelect={(_, data) => setManagedFilter((data.optionValue || 'all') as 'all' | 'managed' | 'unmanaged')}
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
          <>
            <Button
              icon={<DismissRegular />}
              onClick={clearFilter}
            >
              Clear Filters ({activeFilterCount})
            </Button>
            {onSaveToReport && (
              <Button
                icon={<SaveRegular />}
                appearance="primary"
                onClick={onSaveToReport}
              >
                Save To Report
              </Button>
            )}
          </>
        )}
      </div>

      {advancedMode && (
        <div style={{ marginTop: tokens.spacingVerticalM }}>
          <AdvancedFilterBuilder
            solutions={availableSolutions || uniqueSolutions}
            initialFilter={filter}
            onFilterChange={setFilter}
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
            {complexityInfo.hiddenConditions.length > 0 && (
              <Tooltip 
                content={complexityInfo.hiddenConditions.join(', ')} 
                relationship="description"
              >
                <div className={styles.hiddenFiltersIndicator}>
                  <Badge appearance="filled" color="danger">
                    {complexityInfo.hiddenConditions.length} advanced {complexityInfo.hiddenConditions.length === 1 ? 'filter' : 'filters'}
                  </Badge>
                  <InfoRegular />
                </div>
              </Tooltip>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
