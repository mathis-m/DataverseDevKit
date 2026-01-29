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
  // New attribute filter properties
  attribute?: AttributeTarget;
  operator?: StringOperator;
  value?: string;
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
