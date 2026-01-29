import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { IndexStats, FilterNode, ComponentResult, GroupByOption } from '../types';

export interface Solution {
  uniqueName: string;
  displayName: string;
  version: string;
  isManaged: boolean;
  publisher?: string;
}

export interface ComponentType {
  name: string;
  displayName: string;
  typeCode: number;
}

export interface IndexConfig {
  connectionId?: string;
  sourceSolutions: string[];
  targetSolutions: string[];
  componentTypes: string[];
  payloadMode: 'lazy' | 'eager';
}

export interface FilterConfig {
  filters: FilterNode | null;
}

export interface ProgressOperation {
  id: string;
  type: 'index' | 'query' | 'diff' | 'save' | 'load';
  message: string;
  percent: number;
  phase?: string;
}

export interface DiffState {
  componentId?: string;
  leftSolution?: string;
  rightSolution?: string;
  leftPayload?: any;
  rightPayload?: any;
  warnings?: string[];
  searchTerm?: string;
}

export interface FilterBarState {
  searchText: string;
  selectedTypes: string[];
  selectedSolutions: string[];
  managedFilter: string;
  advancedMode: boolean;
  advancedFilter: FilterNode | null;
}

export interface AnalysisState {
  allComponents: ComponentResult[];
  filteredComponents: ComponentResult[];
  selectedComponent: ComponentResult | null;
  viewMode: 'list' | 'visualizations';
  visualizationType: 'sankey' | 'heatmap' | 'stacked' | 'network' | 'circle' | 'treemap';
  groupBy: 'componentType' | 'table' | 'publisher' | 'solution' | 'managed';
}

interface AppState {
  // Global metadata (loaded once)
  availableSolutions: Solution[];
  availableComponentTypes: ComponentType[];
  metadataLoaded: boolean;
  
  // Index state
  indexConfig: IndexConfig;
  indexStats: IndexStats | null;
  
  // UI state
  selectedTab: string;
  
  // Analysis state
  analysisState: AnalysisState;
  
  // Filter bar state
  filterBarState: FilterBarState;
  
  // Legacy (keeping for backward compatibility)
  components: ComponentResult[];
  filterConfig: FilterConfig;
  selectedComponentId: string | null;
  
  // Diff state
  diffState: DiffState | null;
  
  // Progress tracking
  operations: ProgressOperation[];
  
  // Actions
  setAvailableSolutions: (solutions: Solution[]) => void;
  setAvailableComponentTypes: (types: ComponentType[]) => void;
  setMetadataLoaded: (loaded: boolean) => void;
  setIndexConfig: (config: Partial<IndexConfig>) => void;
  setIndexStats: (stats: IndexStats | null) => void;
  
  // UI actions
  setSelectedTab: (tab: string) => void;
  
  // Analysis actions
  setAnalysisState: (state: Partial<AnalysisState>) => void;
  
  // Filter bar actions
  setFilterBarState: (state: Partial<FilterBarState>) => void;
  
  // Legacy actions (keeping for backward compatibility)
  setComponents: (components: ComponentResult[]) => void;
  setFilterConfig: (config: FilterConfig) => void;
  setSelectedComponentId: (id: string | null) => void;
  
  // Diff actions
  setDiffState: (state: DiffState | null) => void;
  
  // Progress actions
  addOperation: (operation: ProgressOperation) => void;
  updateOperation: (id: string, updates: Partial<ProgressOperation>) => void;
  removeOperation: (id: string) => void;
  
  // Reset
  reset: () => void;
}

const initialState = {
  availableSolutions: [] as Solution[],
  availableComponentTypes: [] as ComponentType[],
  metadataLoaded: false,
  indexConfig: {
    sourceSolutions: [],
    targetSolutions: [],
    componentTypes: ['SystemForm', 'SavedQuery', 'Entity', 'Attribute', 'RibbonCustomization'],
    payloadMode: 'lazy' as const,
  },
  indexStats: null,
  selectedTab: 'index',
  analysisState: {
    allComponents: [],
    filteredComponents: [],
    selectedComponent: null,
    viewMode: 'list' as const,
    visualizationType: 'network' as const,
    groupBy: 'componentType' as const,
  },
  filterBarState: {
    searchText: '',
    selectedTypes: [],
    selectedSolutions: [],
    managedFilter: 'all',
    advancedMode: false,
    advancedFilter: null,
  },
  components: [],
  filterConfig: { filters: null },
  selectedComponentId: null,
  diffState: null,
  operations: [],
};

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      ...initialState,
      
      setAvailableSolutions: (solutions) => set({ availableSolutions: solutions }),
      
      setAvailableComponentTypes: (types) => set({ availableComponentTypes: types }),
      
      setMetadataLoaded: (loaded) => set({ metadataLoaded: loaded }),
      
      setIndexConfig: (config) =>
        set((state) => ({
          indexConfig: { ...state.indexConfig, ...config },
        })),
      
      setIndexStats: (stats) => set({ indexStats: stats }),
      
      setSelectedTab: (tab) => set({ selectedTab: tab }),
      
      setAnalysisState: (analysisState) =>
        set((state) => ({
          analysisState: { ...state.analysisState, ...analysisState },
        })),
      
      setFilterBarState: (filterBarState) =>
        set((state) => ({
          filterBarState: { ...state.filterBarState, ...filterBarState },
        })),
      
      setComponents: (components) => set({ components }),
      
      setFilterConfig: (config) => set({ filterConfig: config }),
      
      setSelectedComponentId: (id) => set({ selectedComponentId: id }),
      
      setDiffState: (state) => set({ diffState: state }),
      
      addOperation: (operation) =>
        set((state) => ({
          operations: [...state.operations, operation],
        })),
      
      updateOperation: (id, updates) =>
        set((state) => ({
          operations: state.operations.map((op) =>
            op.id === id ? { ...op, ...updates } : op
          ),
        })),
      
      removeOperation: (id) =>
        set((state) => ({
          operations: state.operations.filter((op) => op.id !== id),
        })),
      
      reset: () => set(initialState),
    }),
    {
      name: 'sla-app-storage',
      partialize: (state) => ({
        indexConfig: state.indexConfig,
        filterConfig: state.filterConfig,
        selectedTab: state.selectedTab,
        analysisState: {
          ...state.analysisState,
          // Don't persist selected component to avoid stale references
          selectedComponent: null,
        },
        filterBarState: state.filterBarState,
        diffState: state.diffState,
        // Don't persist operations or runtime state
      }),
    }
  )
);
