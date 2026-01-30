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

export type GroupByOption = 'solution' | 'componentType' | 'table' | 'publisher' | 'managed';

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
