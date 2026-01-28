import React, { useState, useMemo } from 'react';
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
import { ComponentResult } from '../types';
import { AdvancedFilterBuilder } from './AdvancedFilterBuilder';
import { FilterNode } from '../types';

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
  
  const [searchText, setSearchText] = useState('');
  const [selectedTypes, setSelectedTypes] = useState<string[]>([]);
  const [selectedSolutions, setSelectedSolutions] = useState<string[]>([]);
  const [managedFilter, setManagedFilter] = useState<string>('all');
  const [advancedMode, setAdvancedMode] = useState(false);
  const [advancedFilter, setAdvancedFilter] = useState<FilterNode | null>(null);

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
  React.useEffect(() => {
    onFilterChange(filteredComponents);
  }, [filteredComponents, onFilterChange]);

  // Notify parent when advanced filter changes (for backend query)
  React.useEffect(() => {
    onAdvancedFilterChange?.(advancedFilter);
  }, [advancedFilter, onAdvancedFilterChange]);

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
