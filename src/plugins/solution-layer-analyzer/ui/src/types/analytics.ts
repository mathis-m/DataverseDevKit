// Analytics types mirroring backend AnalyticsDtos.cs

export interface AnalyticsData {
  solutionOverlaps: SolutionOverlapMatrix;
  componentRisks: ComponentRiskSummary[];
  violations: ViolationItem[];
  solutionMetrics: SolutionMetrics[];
  networkData: NetworkGraphData;
  hierarchyData: HierarchyData;
  chordData: ChordDiagramData;
  upSetData: UpSetPlotData;
}

export interface SolutionOverlapMatrix {
  matrix: Record<string, Record<string, number>>;
  detailedOverlaps: DetailedOverlap[];
}

export interface DetailedOverlap {
  solution1: string;
  solution2: string;
  totalOverlap: number;
  managedOverlap: number;
  unmanagedOverlap: number;
  componentTypeBreakdown: Record<string, number>;
  componentIds: string[];
  severity: SeverityLevel;
}

export interface ComponentRiskSummary {
  componentId: string;
  logicalName: string;
  displayName: string;
  componentType: string;
  riskScore: number;
  layerDepth: number;
  topmostSolution: string;
  baseSolution: string;
  hasUnmanagedOverride: boolean;
  violationFlags: string[];
  allSolutions: string[];
}

export interface ViolationItem {
  type: string;
  severity: 'low' | 'medium' | 'high' | 'critical';
  description: string;
  componentId?: string;
  componentName?: string;
  affectedSolutions: string[];
  details?: string;
}

export interface SolutionMetrics {
  solutionName: string;
  totalLayers: number;
  unmanagedLayers: number;
  componentsModified: number;
  componentTypeBreakdown: Record<string, number>;
  overlapsWithOtherSolutions: string[];
  publisher: string;
  isManaged: boolean;
}

export interface NetworkGraphData {
  nodes: NetworkNode[];
  links: NetworkLink[];
}

export interface NetworkNode {
  id: string;
  label: string;
  type: 'solution' | 'component';
  group: string;
  size: number;
  metadata: Record<string, any>;
}

export interface NetworkLink {
  source: string;
  target: string;
  value: number;
  type: string;
}

export interface HierarchyData {
  name: string;
  type: 'solution' | 'component' | 'layer';
  value?: number;
  children?: HierarchyData[];
  metadata: Record<string, any>;
}

export interface ChordDiagramData {
  solutions: string[];
  matrix: number[][];
  chordDetails: ChordDetail[];
}

export interface ChordDetail {
  source: number;
  target: number;
  value: number;
  componentIds: string[];
  componentNames: string[];
}

export interface UpSetPlotData {
  solutions: string[];
  intersections: IntersectionData[];
}

export interface IntersectionData {
  solutions: string[];
  degree: number;
  size: number;
  componentIds: string[];
  componentNames: string[];
}

// Visualization state types
export interface SelectionState {
  selectedComponents: string[];
  selectedSolutions: string[];
  highlightedViolations: string[];
}

export interface FilterState {
  severityFilter: ('low' | 'medium' | 'high' | 'critical')[];
  componentTypeFilter: string[];
  managedFilter: 'all' | 'managed' | 'unmanaged';
  riskScoreRange: [number, number];
}

// Utility types
export type SeverityLevel = 'low' | 'normal' | 'medium' | 'high' | 'critical';
export type ViolationType = 'unmanaged_override' | 'excessive_depth' | 'forbidden_layer';

// Color scales
export const SEVERITY_COLORS: Record<SeverityLevel, string> = {
  low: '#2E7D32',      // Green
  normal: '#4F6BED',   // Indigo
  medium: '#F57C00',   // Orange
  high: '#D32F2F',     // Red
  critical: '#B71C1C', // Dark Red
};

export const RISK_SCORE_COLORS = {
  low: '#4CAF50',      // Green (0-30)
  medium: '#FFC107',   // Yellow (31-60)
  high: '#FF9800',     // Orange (61-80)
  critical: '#F44336', // Red (81-100)
};

// Helper functions
export function getRiskColor(score: number): string {
  if (score <= 30) return RISK_SCORE_COLORS.low;
  if (score <= 60) return RISK_SCORE_COLORS.medium;
  if (score <= 80) return RISK_SCORE_COLORS.high;
  return RISK_SCORE_COLORS.critical;
}

export function getRiskLevel(score: number): 'low' | 'medium' | 'high' | 'critical' {
  if (score <= 30) return 'low';
  if (score <= 60) return 'medium';
  if (score <= 80) return 'high';
  return 'critical';
}

export function getSeverityColor(severity: SeverityLevel): string {
  return SEVERITY_COLORS[severity];
}
