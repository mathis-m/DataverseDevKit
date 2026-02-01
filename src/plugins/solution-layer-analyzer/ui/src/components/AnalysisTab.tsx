import React, { useState, useCallback, useMemo, useRef, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Dropdown,
  Option,
  TabList,
  Tab,
  Divider,
} from '@fluentui/react-components';
import {
  FilterRegular,
  ChartMultipleRegular,
  TableRegular,
  ArrowMaximize20Regular,
} from '@fluentui/react-icons';
import { ComponentResult, GroupByOption, FilterNode } from '../types';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
import { ComponentFilterBar } from './ComponentFilterBar';
import { ComponentList } from './ComponentList';
import { ComponentDetailPanel } from './ComponentDetailPanel';
import { VisualizationModal } from './VisualizationModal';
import { SaveToReportDialog } from './SaveToReportDialog';
import { LayerFlowSankey } from '../visualizations/LayerFlowSankey';
import { LayerHeatmap } from '../visualizations/LayerHeatmap';
import { LayerStackedBarChart } from '../visualizations/LayerStackedBarChart';
import { LayerNetworkGraph } from '../visualizations/LayerNetworkGraph';
import { LayerCirclePacking } from '../visualizations/LayerCirclePacking';
import { LayerTreemap } from '../visualizations/LayerTreemap';
import { useDebouncedValue } from '../hooks/useDebounce';

const QUERY_DEBOUNCE_MS = 300;

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  visualizationContainer: {
    padding: tokens.spacingVerticalL,
  },
  splitView: {
    display: 'grid',
    gridTemplateColumns: '1fr auto',
    gap: tokens.spacingHorizontalM,
  },
});

interface AnalysisTabProps {
  onNavigateToDiff: (componentId: string, leftSolution: string, rightSolution: string) => void;
}

export const AnalysisTab: React.FC<AnalysisTabProps> = ({ onNavigateToDiff }) => {
  const styles = useStyles();
  const { queryComponents, loading } = usePluginApi();
  const { analysisState, setAnalysisState } = useAppStore();
  // Use stable selectors - Zustand only re-renders if this specific value changes
  const advancedFilter = useAppStore((state) => state.filterBarState.advancedFilter);
  const availableSolutions = useAppStore((state) => state.availableSolutions);

  // Memoize the solution names array to prevent unnecessary re-renders
  const availableSolutionNames = useMemo(
    () => availableSolutions.map(s => s.uniqueName),
    [availableSolutions]
  );

  // Extract state from store
  const {
    allComponents,
    filteredComponents,
    selectedComponent,
    viewMode,
    visualizationType,
    groupBy,
  } = analysisState;

  // Memoized setters that update the store (stable references)
  const setAllComponents = useCallback((components: ComponentResult[]) => 
    setAnalysisState({ allComponents: components }), [setAnalysisState]);
  const setFilteredComponents = useCallback((components: ComponentResult[]) => 
    setAnalysisState({ filteredComponents: components }), [setAnalysisState]);
  const setSelectedComponent = useCallback((component: ComponentResult | null) => 
    setAnalysisState({ selectedComponent: component }), [setAnalysisState]);
  const setViewMode = useCallback((mode: 'list' | 'visualizations') => 
    setAnalysisState({ viewMode: mode }), [setAnalysisState]);
  const setVisualizationType = useCallback((type: 'sankey' | 'heatmap' | 'stacked' | 'network' | 'circle' | 'treemap') => 
    setAnalysisState({ visualizationType: type }), [setAnalysisState]);
  const setGroupBy = useCallback((by: 'componentType' | 'table' | 'publisher' | 'solution' | 'managed') => 
    setAnalysisState({ groupBy: by }), [setAnalysisState]);

  const [fullscreenViz, setFullscreenViz] = useState<boolean>(false);

  const loadComponents = useCallback(async (filter?: FilterNode | null) => {
    console.log('[AnalysisTab] loadComponents called with filter:', JSON.stringify(filter, null, 2));
    const components = await queryComponents(filter);
    setAllComponents(components);
    setFilteredComponents(components);
  }, [queryComponents, setAllComponents, setFilteredComponents]);

  // Load components on mount and when advancedFilter changes
  // Debounce the filter to prevent rapid re-queries during typing/editing
  const advancedFilterJson = useMemo(() => 
    advancedFilter ? JSON.stringify(advancedFilter) : null,
    [advancedFilter]
  );
  
  // Debounce the JSON representation to batch rapid filter changes
  const debouncedFilterJson = useDebouncedValue(advancedFilterJson, QUERY_DEBOUNCE_MS);
  
  // Keep a ref to the current filter so we query with the latest when debounce fires
  const advancedFilterRef = useRef(advancedFilter);
  useEffect(() => {
    advancedFilterRef.current = advancedFilter;
  }, [advancedFilter]);
  
  useEffect(() => {
    // Parse the debounced JSON to get the filter, or use the ref for latest
    // The ref ensures we always query with the most recent filter value
    loadComponents(advancedFilterRef.current);
  }, [debouncedFilterJson]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleFilterChange = useCallback((filtered: ComponentResult[]) => {
    setFilteredComponents(filtered);
  }, [setFilteredComponents]);

  const handleAdvancedFilterChange = useCallback((_filter: FilterNode | null) => {
    // This is now handled via the store through ComponentFilterBar
  }, []);

  const handleSelectComponent = useCallback((component: ComponentResult) => {
    setSelectedComponent(component);
  }, [setSelectedComponent]);

  const handleCloseDetail = useCallback(() => {
    setSelectedComponent(null);
  }, [setSelectedComponent]);

  const handleDiff = useCallback((leftSolution: string, rightSolution: string) => {
    if (selectedComponent) {
      onNavigateToDiff(selectedComponent.componentId, leftSolution, rightSolution);
    }
  }, [selectedComponent, onNavigateToDiff]);

  const stats = useMemo(() => {
    const types = new Set(filteredComponents.map(c => c.componentType)).size;
    const solutions = new Set(filteredComponents.flatMap(c => c.layerSequence)).size;
    const avgLayers = filteredComponents.length > 0
      ? filteredComponents.reduce((sum, c) => sum + c.layerSequence.length, 0) / filteredComponents.length
      : 0;
    const maxLayers = Math.max(...filteredComponents.map(c => c.layerSequence.length), 0);

    return { types, solutions, avgLayers: avgLayers.toFixed(1), maxLayers };
  }, [filteredComponents]);

  return (
    <div className={styles.container}>
      <Card>
        <CardHeader 
          header={<Text weight="semibold">Component Analysis</Text>} 
          description="Explore and analyze solution component layers" 
          action={
            <Button 
              appearance="primary" 
              icon={<FilterRegular />}
              onClick={() => loadComponents(advancedFilter)}
              disabled={loading.querying}
            >
              {loading.querying ? 'Loading...' : 'Refresh'}
            </Button>
          }
        />

        <div style={{ padding: tokens.spacingVerticalM }}>
          <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, marginBottom: tokens.spacingVerticalM }}>
            <Card style={{ flex: 1, padding: tokens.spacingVerticalS }}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Components</Text>
              <Text size={600} weight="bold">{filteredComponents.length}</Text>
            </Card>
            <Card style={{ flex: 1, padding: tokens.spacingVerticalS }}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Types</Text>
              <Text size={600} weight="bold">{stats.types}</Text>
            </Card>
            <Card style={{ flex: 1, padding: tokens.spacingVerticalS }}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Solutions</Text>
              <Text size={600} weight="bold">{stats.solutions}</Text>
            </Card>
            <Card style={{ flex: 1, padding: tokens.spacingVerticalS }}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Avg Layers</Text>
              <Text size={600} weight="bold">{stats.avgLayers}</Text>
            </Card>
            <Card style={{ flex: 1, padding: tokens.spacingVerticalS }}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Max Layers</Text>
              <Text size={600} weight="bold">{stats.maxLayers}</Text>
            </Card>
          </div>

          <ComponentFilterBar
            components={allComponents}
            availableSolutions={availableSolutionNames}
            onFilterChange={handleFilterChange}
            onAdvancedFilterChange={handleAdvancedFilterChange}
            loading={loading.querying}
          />
        </div>
      </Card>

      <Card>
        <div style={{ padding: tokens.spacingVerticalM }}>
          <TabList
            selectedValue={viewMode}
            onTabSelect={(_, d) => setViewMode(d.value as any)}
          >
            <Tab icon={<TableRegular />} value="list">List View</Tab>
            <Tab icon={<ChartMultipleRegular />} value="visualizations">Visualizations</Tab>
          </TabList>

          <Divider style={{ margin: `${tokens.spacingVerticalM} 0` }} />

          {viewMode === 'list' ? (
            <div className={selectedComponent ? styles.splitView : undefined}>
              <ComponentList
                components={filteredComponents}
                onSelectComponent={handleSelectComponent}
                selectedComponentId={selectedComponent?.componentId}
              />
              {selectedComponent && (
                <ComponentDetailPanel
                  component={selectedComponent}
                  onClose={handleCloseDetail}
                  onDiff={handleDiff}
                />
              )}
            </div>
          ) : (
            <div>
              <div className={styles.controls}>
                <Text>Visualization:</Text>
                <Dropdown
                  value={
                    visualizationType === 'network' ? 'Network Graph' :
                    visualizationType === 'circle' ? 'Circle Packing' :
                    visualizationType === 'treemap' ? 'Treemap' :
                    visualizationType === 'sankey' ? 'Layer Flow' :
                    visualizationType === 'heatmap' ? 'Heatmap' :
                    'Layer Depth'
                  }
                  onOptionSelect={(_, d) => setVisualizationType(d.optionValue as any)}
                >
                  <Option value="network">Network Graph</Option>
                  <Option value="circle">Circle Packing</Option>
                  <Option value="treemap">Treemap</Option>
                  <Option value="sankey">Layer Flow</Option>
                  <Option value="heatmap">Heatmap</Option>
                  <Option value="stacked">Layer Depth</Option>
                </Dropdown>

                <Text>Group By:</Text>
                <Dropdown
                  value={groupBy}
                  onOptionSelect={(_, d) => setGroupBy(d.optionValue as GroupByOption)}
                  disabled={visualizationType === 'sankey' || visualizationType === 'network'}
                >
                  <Option value="componentType">Component Type</Option>
                  <Option value="table">Table</Option>
                  <Option value="publisher">Publisher</Option>
                  <Option value="solution">Solution</Option>
                  <Option value="managed">Managed Status</Option>
                </Dropdown>

                <Button
                  icon={<ArrowMaximize20Regular />}
                  onClick={() => setFullscreenViz(true)}
                  title="Fullscreen"
                >
                  Fullscreen
                </Button>
              </div>

              <div className={styles.visualizationContainer}>
                {visualizationType === 'network' && (
                  <LayerNetworkGraph components={filteredComponents} width={1000} height={600} />
                )}
                {visualizationType === 'circle' && (
                  <LayerCirclePacking components={filteredComponents} width={1000} height={600} />
                )}
                {visualizationType === 'treemap' && (
                  <LayerTreemap components={filteredComponents} width={1000} height={600} />
                )}
                {visualizationType === 'sankey' && (
                  <LayerFlowSankey components={filteredComponents} width={1000} height={600} />
                )}
                {visualizationType === 'heatmap' && (
                  <LayerHeatmap components={filteredComponents} groupBy={groupBy} width={1000} height={600} />
                )}
                {visualizationType === 'stacked' && (
                  <LayerStackedBarChart components={filteredComponents} groupBy={groupBy} width={1000} height={600} />
                )}
              </div>
            </div>
          )}
        </div>
      </Card>

      <VisualizationModal
        isOpen={fullscreenViz}
        onClose={() => setFullscreenViz(false)}
        title={
          visualizationType === 'network' ? 'Network Graph' :
          visualizationType === 'circle' ? 'Circle Packing' :
          visualizationType === 'treemap' ? 'Treemap' :
          visualizationType === 'sankey' ? 'Layer Flow' :
          visualizationType === 'heatmap' ? 'Heatmap' :
          'Layer Depth'
        }
      >
        {visualizationType === 'network' && (
          <LayerNetworkGraph components={filteredComponents} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
        {visualizationType === 'circle' && (
          <LayerCirclePacking components={filteredComponents} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
        {visualizationType === 'treemap' && (
          <LayerTreemap components={filteredComponents} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
        {visualizationType === 'sankey' && (
          <LayerFlowSankey components={filteredComponents} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
        {visualizationType === 'heatmap' && (
          <LayerHeatmap components={filteredComponents} groupBy={groupBy} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
        {visualizationType === 'stacked' && (
          <LayerStackedBarChart components={filteredComponents} groupBy={groupBy} width={window.innerWidth * 0.9} height={window.innerHeight * 0.8} />
        )}
      </VisualizationModal>

      <SaveToReportDialog
        open={showSaveToReport}
        onOpenChange={setShowSaveToReport}
        currentFilter={advancedFilter}
      />
    </div>
  );
};
