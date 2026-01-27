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

export interface Layer {
  solutionName: string;
  publisher?: string;
  managed: boolean;
  version?: string;
  ordinal: number;
  createdOn?: string;
}

export interface IndexStats {
  stats?: {
    solutions: number;
    components: number;
    layers: number;
  };
  warnings?: string[];
}

export type GroupByOption = 'solution' | 'componentType' | 'table' | 'publisher' | 'managed';

export interface FilterOptions {
  componentTypes: string[];
  solutions: string[];
  managed?: boolean;
  searchText: string;
}
