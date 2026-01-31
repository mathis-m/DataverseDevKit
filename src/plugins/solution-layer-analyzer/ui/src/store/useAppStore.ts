import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { IndexStats, FilterNode, ComponentResult, AttributeDiff, QueryPlanStats } from '../types';

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

// Index metadata from existing index (source/target solutions used to build it)
export interface StoredIndexMetadata {
  hasIndex: boolean;
  sourceSolutions: string[];
  targetSolutions: string[];
  stats?: {
    solutions: number;
    components: number;
    layers: number;
  };
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
  attributeDiffs?: AttributeDiff[];
  warnings?: string[];
  searchTerm?: string;
}

export interface FilterBarState {
  /** Whether advanced filter mode is enabled */
  advancedMode: boolean;
  /** The filter AST - single source of truth for all filter state */
  advancedFilter: FilterNode | null;
  // Legacy fields - kept for backward compatibility with persisted state
  // These are no longer used; simple values are derived from advancedFilter
  /** @deprecated Use advancedFilter instead */
  searchText?: string;
  /** @deprecated Use advancedFilter instead */
  selectedTypes?: string[];
  /** @deprecated Use advancedFilter instead */
  selectedSolutions?: string[];
  /** @deprecated Use advancedFilter instead */
  managedFilter?: string;
}

export interface AnalysisState {
  allComponents: ComponentResult[];
  filteredComponents: ComponentResult[];
  selectedComponent: ComponentResult | null;
  viewMode: 'list' | 'visualizations';
  visualizationType: 'sankey' | 'heatmap' | 'stacked' | 'network' | 'circle' | 'treemap';
  groupBy: 'componentType' | 'table' | 'publisher' | 'solution' | 'managed';
}

/**
 * Query state for tracking event-based queries.
 */
export interface QueryState {
  /** The latest query ID that was sent */
  latestQueryId: string | null;
  /** Whether a query is currently in progress */
  isQuerying: boolean;
  /** The last query stats for diagnostics */
  lastQueryStats: QueryPlanStats | null;
  /** Error message from the last query (if any) */
  lastError: string | null;
}

interface AppState {
  // Global metadata (loaded once)
  availableSolutions: Solution[];
  availableComponentTypes: ComponentType[];
  metadataLoaded: boolean;
  
  // Index state
  indexConfig: IndexConfig;
  indexStats: IndexStats | null;
  indexMetadata: StoredIndexMetadata | null;
  
  // UI state
  selectedTab: string;
  
  // Analysis state
  analysisState: AnalysisState;
  
  // Filter bar state
  filterBarState: FilterBarState;
  
  // Query state (for event-based queries)
  queryState: QueryState;
  
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
  setIndexMetadata: (metadata: StoredIndexMetadata | null) => void;
  
  // UI actions
  setSelectedTab: (tab: string) => void;
  
  // Analysis actions
  setAnalysisState: (state: Partial<AnalysisState>) => void;
  
  // Filter bar actions
  setFilterBarState: (state: Partial<FilterBarState>) => void;
  
  // Query state actions
  setQueryState: (state: Partial<QueryState>) => void;
  
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
  indexMetadata: null as StoredIndexMetadata | null,
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
    advancedMode: false,
    advancedFilter: null,
  },
  queryState: {
    latestQueryId: null,
    isQuerying: false,
    lastQueryStats: null,
    lastError: null,
  } as QueryState,
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
      
      setIndexMetadata: (metadata) => set({ indexMetadata: metadata }),
      
      setSelectedTab: (tab) => set({ selectedTab: tab }),
      
      setAnalysisState: (analysisState) =>
        set((state) => ({
          analysisState: { ...state.analysisState, ...analysisState },
        })),
      
      setFilterBarState: (filterBarState) =>
        set((state) => ({
          filterBarState: { ...state.filterBarState, ...filterBarState },
        })),
      
      setQueryState: (queryState) =>
        set((state) => ({
          queryState: { ...state.queryState, ...queryState },
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
