import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { 
  IndexStats, 
  FilterNode, 
  ComponentResult, 
  AttributeDiff, 
  QueryPlanStats,
  Report,
  ReportGroup,
  ReportSummary,
  ReportItem,
  ReportOutputFormat,
  ReportProgressEvent,
} from '../types';

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

/**
 * Report builder configuration state (persisted)
 */
export interface ReportBuilderState {
  /** Report groups with their contained reports */
  reportGroups: ReportGroup[];
  /** Reports not assigned to any group */
  ungroupedReports: Report[];
}

/**
 * Report execution run state (not persisted)
 */
export interface ReportRunState {
  /** Current operation ID for tracking */
  operationId: string | null;
  /** Whether reports are currently being executed */
  isRunning: boolean;
  /** Progress information during execution */
  progress: ReportProgressEvent | null;
  /** Execution results after completion */
  results: {
    summary: ReportSummary;
    reports: ReportItem[];
  } | null;
  /** File content if generateFile was requested */
  outputContent: string | null;
  /** Format of the output content */
  outputFormat: ReportOutputFormat | null;
  /** Whether the summary panel is collapsed */
  summaryCollapsed: boolean;
  /** Error from last run */
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
  
  // Report builder state
  reportBuilderState: ReportBuilderState;
  
  // Report run state
  reportRunState: ReportRunState;
  
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
  
  // Report builder actions
  setReportBuilderState: (state: Partial<ReportBuilderState>) => void;
  addReportGroup: (group: ReportGroup) => void;
  updateReportGroup: (groupId: string, updates: Partial<ReportGroup>) => void;
  deleteReportGroup: (groupId: string) => void;
  addReport: (report: Report, groupId?: string) => void;
  updateReport: (reportId: string, updates: Partial<Report>, groupId?: string) => void;
  deleteReport: (reportId: string, groupId?: string) => void;
  moveReport: (reportId: string, fromGroupId: string | null, toGroupId: string | null) => void;
  reorderReportGroups: (groupIds: string[]) => void;
  reorderReports: (groupId: string | null, reportIds: string[]) => void;
  
  // Report run state actions
  setReportRunState: (state: Partial<ReportRunState>) => void;
  clearReportResults: () => void;
  
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
  reportBuilderState: {
    reportGroups: [],
    ungroupedReports: [],
  } as ReportBuilderState,
  reportRunState: {
    operationId: null,
    isRunning: false,
    progress: null,
    results: null,
    outputContent: null,
    outputFormat: null,
    summaryCollapsed: false,
    lastError: null,
  } as ReportRunState,
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
      
      // Report builder actions
      setReportBuilderState: (reportBuilderState) =>
        set((state) => ({
          reportBuilderState: { ...state.reportBuilderState, ...reportBuilderState },
        })),
      
      addReportGroup: (group) =>
        set((state) => ({
          reportBuilderState: {
            ...state.reportBuilderState,
            reportGroups: [...state.reportBuilderState.reportGroups, group],
          },
        })),
      
      updateReportGroup: (groupId, updates) =>
        set((state) => ({
          reportBuilderState: {
            ...state.reportBuilderState,
            reportGroups: state.reportBuilderState.reportGroups.map((g) =>
              g.id === groupId ? { ...g, ...updates } : g
            ),
          },
        })),
      
      deleteReportGroup: (groupId) =>
        set((state) => {
          const group = state.reportBuilderState.reportGroups.find((g) => g.id === groupId);
          const reportsToMove = group?.reports || [];
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              reportGroups: state.reportBuilderState.reportGroups.filter((g) => g.id !== groupId),
              // Move reports from deleted group to ungrouped
              ungroupedReports: [...state.reportBuilderState.ungroupedReports, ...reportsToMove],
            },
          };
        }),
      
      addReport: (report, groupId) =>
        set((state) => {
          if (groupId) {
            return {
              reportBuilderState: {
                ...state.reportBuilderState,
                reportGroups: state.reportBuilderState.reportGroups.map((g) =>
                  g.id === groupId
                    ? { ...g, reports: [...g.reports, report] }
                    : g
                ),
              },
            };
          }
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              ungroupedReports: [...state.reportBuilderState.ungroupedReports, report],
            },
          };
        }),
      
      updateReport: (reportId, updates, groupId) =>
        set((state) => {
          if (groupId) {
            return {
              reportBuilderState: {
                ...state.reportBuilderState,
                reportGroups: state.reportBuilderState.reportGroups.map((g) =>
                  g.id === groupId
                    ? {
                        ...g,
                        reports: g.reports.map((r) =>
                          r.id === reportId ? { ...r, ...updates } : r
                        ),
                      }
                    : g
                ),
              },
            };
          }
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              ungroupedReports: state.reportBuilderState.ungroupedReports.map((r) =>
                r.id === reportId ? { ...r, ...updates } : r
              ),
            },
          };
        }),
      
      deleteReport: (reportId, groupId) =>
        set((state) => {
          if (groupId) {
            return {
              reportBuilderState: {
                ...state.reportBuilderState,
                reportGroups: state.reportBuilderState.reportGroups.map((g) =>
                  g.id === groupId
                    ? { ...g, reports: g.reports.filter((r) => r.id !== reportId) }
                    : g
                ),
              },
            };
          }
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              ungroupedReports: state.reportBuilderState.ungroupedReports.filter(
                (r) => r.id !== reportId
              ),
            },
          };
        }),
      
      moveReport: (reportId, fromGroupId, toGroupId) =>
        set((state) => {
          let report: Report | undefined;
          let newReportGroups = [...state.reportBuilderState.reportGroups];
          let newUngroupedReports = [...state.reportBuilderState.ungroupedReports];

          // Remove from source
          if (fromGroupId) {
            const groupIdx = newReportGroups.findIndex((g) => g.id === fromGroupId);
            if (groupIdx >= 0) {
              report = newReportGroups[groupIdx].reports.find((r) => r.id === reportId);
              newReportGroups[groupIdx] = {
                ...newReportGroups[groupIdx],
                reports: newReportGroups[groupIdx].reports.filter((r) => r.id !== reportId),
              };
            }
          } else {
            report = newUngroupedReports.find((r) => r.id === reportId);
            newUngroupedReports = newUngroupedReports.filter((r) => r.id !== reportId);
          }

          if (!report) return state;

          // Add to destination
          if (toGroupId) {
            const groupIdx = newReportGroups.findIndex((g) => g.id === toGroupId);
            if (groupIdx >= 0) {
              newReportGroups[groupIdx] = {
                ...newReportGroups[groupIdx],
                reports: [...newReportGroups[groupIdx].reports, report],
              };
            }
          } else {
            newUngroupedReports = [...newUngroupedReports, report];
          }

          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              reportGroups: newReportGroups,
              ungroupedReports: newUngroupedReports,
            },
          };
        }),
      
      reorderReportGroups: (groupIds) =>
        set((state) => {
          const groupMap = new Map(state.reportBuilderState.reportGroups.map((g) => [g.id, g]));
          const reorderedGroups = groupIds
            .map((id, index) => {
              const group = groupMap.get(id);
              return group ? { ...group, displayOrder: index } : null;
            })
            .filter((g): g is ReportGroup => g !== null);
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              reportGroups: reorderedGroups,
            },
          };
        }),
      
      reorderReports: (groupId, reportIds) =>
        set((state) => {
          if (groupId) {
            return {
              reportBuilderState: {
                ...state.reportBuilderState,
                reportGroups: state.reportBuilderState.reportGroups.map((g) => {
                  if (g.id !== groupId) return g;
                  const reportMap = new Map(g.reports.map((r) => [r.id, r]));
                  const reorderedReports = reportIds
                    .map((id, index) => {
                      const report = reportMap.get(id);
                      return report ? { ...report, displayOrder: index } : null;
                    })
                    .filter((r): r is Report => r !== null);
                  return { ...g, reports: reorderedReports };
                }),
              },
            };
          }
          const reportMap = new Map(state.reportBuilderState.ungroupedReports.map((r) => [r.id, r]));
          const reorderedReports = reportIds
            .map((id, index) => {
              const report = reportMap.get(id);
              return report ? { ...report, displayOrder: index } : null;
            })
            .filter((r): r is Report => r !== null);
          return {
            reportBuilderState: {
              ...state.reportBuilderState,
              ungroupedReports: reorderedReports,
            },
          };
        }),
      
      // Report run state actions
      setReportRunState: (reportRunState) =>
        set((state) => ({
          reportRunState: { ...state.reportRunState, ...reportRunState },
        })),
      
      clearReportResults: () =>
        set((state) => ({
          reportRunState: {
            ...state.reportRunState,
            results: null,
            outputContent: null,
            outputFormat: null,
            progress: null,
            lastError: null,
          },
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
        // Persist report builder config but not run state
        reportBuilderState: state.reportBuilderState,
        // Persist summary collapsed state only
        reportRunState: {
          summaryCollapsed: state.reportRunState.summaryCollapsed,
        },
        // Don't persist operations or runtime state
      }),
    }
  )
);
