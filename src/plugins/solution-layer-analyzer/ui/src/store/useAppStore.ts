import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { IndexStats, FilterNode, ComponentResult } from '../types';

export interface IndexConfig {
  connectionId?: string;
  sourceSolutions: string[];
  targetSolutions: string[];
  includeComponentTypes: string[];
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

interface AppState {
  // Index state
  indexConfig: IndexConfig;
  indexStats: IndexStats | null;
  
  // Analysis state
  components: ComponentResult[];
  filterConfig: FilterConfig;
  selectedComponentId: string | null;
  
  // Diff state
  diffComponentId: string | undefined;
  diffLeftSolution: string | undefined;
  diffRightSolution: string | undefined;
  
  // Progress tracking
  operations: ProgressOperation[];
  
  // Actions
  setIndexConfig: (config: Partial<IndexConfig>) => void;
  setIndexStats: (stats: IndexStats | null) => void;
  setComponents: (components: ComponentResult[]) => void;
  setFilterConfig: (config: FilterConfig) => void;
  setSelectedComponentId: (id: string | null) => void;
  setDiffState: (componentId?: string, leftSolution?: string, rightSolution?: string) => void;
  
  // Progress actions
  addOperation: (operation: ProgressOperation) => void;
  updateOperation: (id: string, updates: Partial<ProgressOperation>) => void;
  removeOperation: (id: string) => void;
  
  // Reset
  reset: () => void;
}

const initialState = {
  indexConfig: {
    sourceSolutions: [],
    targetSolutions: [],
    includeComponentTypes: ['SystemForm', 'SavedQuery', 'Entity', 'Attribute', 'RibbonCustomization'],
    payloadMode: 'lazy' as const,
  },
  indexStats: null,
  components: [],
  filterConfig: { filters: null },
  selectedComponentId: null,
  diffComponentId: undefined,
  diffLeftSolution: undefined,
  diffRightSolution: undefined,
  operations: [],
};

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      ...initialState,
      
      setIndexConfig: (config) =>
        set((state) => ({
          indexConfig: { ...state.indexConfig, ...config },
        })),
      
      setIndexStats: (stats) => set({ indexStats: stats }),
      
      setComponents: (components) => set({ components }),
      
      setFilterConfig: (config) => set({ filterConfig: config }),
      
      setSelectedComponentId: (id) => set({ selectedComponentId: id }),
      
      setDiffState: (componentId, leftSolution, rightSolution) =>
        set({ diffComponentId: componentId, diffLeftSolution: leftSolution, diffRightSolution: rightSolution }),
      
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
        // Don't persist operations or runtime state
      }),
    }
  )
);
