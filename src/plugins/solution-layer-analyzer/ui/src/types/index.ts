export interface ComponentResult {
  componentId: string;
  componentType: string;
  logicalName: string;
  displayName?: string;
  layerSequence: string[];
  isManaged: boolean;
  publisher?: string;
  tableLogicalName?: string;
}

export interface ComponentJsonAttributes {
  Key: string;
  Value: unknown;
}

export interface ComponentJson {
  Attributes?: ComponentJsonAttributes[];
  Id?: string;
  SchemaName?: string;
}

export interface Layer {
  solutionName: string;
  publisher?: string;
  managed: boolean;
  version?: string;
  ordinal: number;
  createdOn?: string;
  componentJson?: ComponentJson; // Full entity attributes from msdyn_componentjson
  changes?: string; // Changed attributes from msdyn_changes
}

export interface IndexStats {
  stats?: {
    solutions: number;
    components: number;
    layers: number;
  };
  warnings?: string[];
}

export enum StringOperator {
  Equals = 'Equals',
  NotEquals = 'NotEquals',
  Contains = 'Contains',
  NotContains = 'NotContains',
  BeginsWith = 'BeginsWith',
  NotBeginsWith = 'NotBeginsWith',
  EndsWith = 'EndsWith',
  NotEndsWith = 'NotEndsWith'
}

export enum AttributeTarget {
  LogicalName = 'LogicalName',
  DisplayName = 'DisplayName',
  ComponentType = 'ComponentType',
  Publisher = 'Publisher',
  TableLogicalName = 'TableLogicalName'
}

export enum AttributeDiffTargetMode {
  Specific = 'Specific',
  AllBelow = 'AllBelow'
}

export enum AttributeMatchLogic {
  Any = 'Any',
  All = 'All'
}

export interface SolutionQueryNode {
  attribute: string;
  operator: StringOperator;
  value: string;
}

export interface FilterNode {
  type: string;
  id: string;
  solution?: string;
  solutions?: string[];
  sequence?: (string | string[] | SolutionQueryNode)[];
  children?: FilterNode[];
  // Component-level attribute filter properties (AttributeTarget for ATTRIBUTE type, string for SOLUTION_QUERY)
  attribute?: AttributeTarget | string;
  operator?: StringOperator;
  value?: string;
  // Nested query properties
  layerFilter?: FilterNode; // For LAYER_QUERY
  attributeFilter?: FilterNode; // For LAYER_ATTRIBUTE_QUERY - nested filter for layer attributes
  // HAS_ATTRIBUTE_DIFF filter properties
  sourceSolution?: string;
  targetMode?: AttributeDiffTargetMode;
  targetSolutions?: string[];
  onlyChangedAttributes?: boolean;
  attributeNames?: string[];
  attributeMatchLogic?: AttributeMatchLogic;
  // Source identifier for correlating simple filter UI controls with their AST representation
  // Used to find/update specific nodes when simple filter values change
  sourceId?: 'simple-search' | 'simple-types' | 'simple-solutions' | 'simple-managed' | string;
}

/**
 * Represents the simple filter values extracted from a FilterNode AST.
 * These values drive the simple filter UI controls.
 */
export interface SimpleFilterValues {
  searchText: string;
  selectedTypes: string[];
  selectedSolutions: string[];
  managedFilter: 'all' | 'managed' | 'unmanaged';
}

/**
 * Information about filter complexity for UI display
 */
export interface FilterComplexityInfo {
  /** True if the current filter can be fully represented in simple mode */
  isSimpleRepresentable: boolean;
  /** Descriptions of conditions that cannot be shown in simple mode */
  hiddenConditions: string[];
}

export interface IndexResponse {
  operationId: string;
  started: boolean;
  errorMessage?: string;
}

export interface IndexCompletionEvent {
  operationId: string;
  success: boolean;
  stats?: {
    solutions: number;
    components: number;
    layers: number;
  };
  warnings?: string[];
  errorMessage?: string;
}

export interface IndexMetadata {
  hasIndex: boolean;
  sourceSolutions: string[];
  targetSolutions: string[];
  stats?: {
    solutions: number;
    components: number;
    layers: number;
  };
}

/**
 * Query execution statistics for diagnostics.
 */
export interface QueryPlanStats {
  preFetchDurationMs?: number;
  sqlQueryDurationMs?: number;
  inMemoryFilterDurationMs?: number;
  totalDurationMs?: number;
  rowsFromSql?: number;
  rowsAfterFilter?: number;
  filterEfficiency?: number;
  usedInMemoryFilter?: boolean;
  planDescription?: string;
}

/**
 * Acknowledgment returned when using event-based queries.
 */
export interface QueryAcknowledgment {
  queryId: string;
  started: boolean;
  errorMessage?: string;
}

/**
 * Event payload for query results when using event-based responses.
 */
export interface QueryResultEvent {
  queryId: string;
  success: boolean;
  rows?: ComponentResult[];
  total?: number;
  stats?: QueryPlanStats;
  errorMessage?: string;
}

export type GroupByOption = 'solution' | 'componentType' | 'table' | 'publisher' | 'managed';

export type ReportOutputFormat = 'Json' | 'Yaml' | 'Csv';
export type ReportVerbosity = 'Basic' | 'Medium' | 'Verbose';
export type ReportSeverity = 'Information' | 'Warning' | 'Critical';

export interface GenerateReportOutputRequest {
  connectionId?: string;
  format: ReportOutputFormat;
  verbosity: ReportVerbosity;
}

export interface ReportComponentLayer {
  solutionName: string;
  changedAttributes?: Array<{ attributeName: string }>;
}

export interface ReportComponent {
  componentId: string;
  componentTypeName: string;
  displayName?: string;
  logicalName?: string;
  solutions?: string[];
  layers?: ReportComponentLayer[];
  makePortalUrl?: string;
}

export interface ReportItem {
  name: string;
  severity: ReportSeverity;
  group?: string;
  totalMatches: number;
  recommendedAction?: string;
  components?: ReportComponent[];
}

export interface ReportSummary {
  totalReports: number;
  criticalFindings: number;
  warningFindings: number;
  informationalFindings: number;
  totalComponents: number;
}

export interface GenerateReportOutputResponse {
  outputContent?: string;
  summary?: ReportSummary;
  reports?: ReportItem[];
}

export interface FilterOptions {
  componentTypes: string[];
  solutions: string[];
  managed?: boolean;
  searchText: string;
}

export interface AttributeDiff {
  attributeName: string;
  leftValue?: string;
  rightValue?: string;
  attributeType: number;
  isComplex: boolean;
  onlyInLeft: boolean;
  onlyInRight: boolean;
  isDifferent: boolean;
}

// ============================================================================
// Report Builder Types
// ============================================================================

/**
 * A single report definition with a filter query
 */
export interface Report {
  id: string;
  name: string;
  description?: string;
  severity: ReportSeverity;
  recommendedAction?: string;
  queryJson: string;
  displayOrder: number;
}

/**
 * A group of reports
 */
export interface ReportGroup {
  id: string;
  name: string;
  displayOrder: number;
  reports: Report[];
}

/**
 * Full report configuration (groups + ungrouped reports)
 */
export interface ReportConfig {
  sourceSolutions: string[];
  targetSolutions: string[];
  componentTypes?: number[];
  reportGroups: ReportGroup[];
  ungroupedReports: Report[];
}

/**
 * Format for report config serialization
 */
export type ReportConfigFormat = 'json' | 'yaml' | 'xml';

/**
 * Request to parse a report config from file content
 */
export interface ParseReportConfigRequest {
  content: string;
  format?: ReportConfigFormat; // Auto-detect if not provided
}

/**
 * Response from parsing a report config
 */
export interface ParseReportConfigResponse {
  config: ReportConfig;
  errors?: string[];
  warnings?: string[];
}

/**
 * Request to serialize a report config to a specific format
 */
export interface SerializeReportConfigRequest {
  config: ReportConfig;
  format: ReportConfigFormat;
}

/**
 * Response from serializing a report config
 */
export interface SerializeReportConfigResponse {
  content: string;
}

/**
 * Request to execute reports (event-based)
 */
export interface ExecuteReportsRequest {
  operationId?: string;
  connectionId?: string;
  config: ReportConfig;
  verbosity: ReportVerbosity;
  format?: ReportOutputFormat;
  generateFile?: boolean;
}

/**
 * Acknowledgment when starting report execution
 */
export interface ExecuteReportsAcknowledgment {
  operationId: string;
  started: boolean;
  errorMessage?: string;
}

/**
 * Progress event during report execution
 */
export interface ReportProgressEvent {
  operationId: string;
  currentReport: number;
  totalReports: number;
  currentReportName?: string;
  phase: 'starting' | 'executing' | 'generating-output' | 'complete';
  percent: number;
}

/**
 * Completion event with report execution results
 */
export interface ReportCompletionEvent {
  operationId: string;
  success: boolean;
  summary?: ReportSummary;
  reports?: ReportItem[];
  outputContent?: string; // Serialized file content if generateFile was true
  outputFormat?: ReportOutputFormat;
  errorMessage?: string;
}
