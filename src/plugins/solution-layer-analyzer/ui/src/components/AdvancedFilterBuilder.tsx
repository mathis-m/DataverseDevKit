import React, { useState } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Card,
  CardHeader,
  Dropdown,
  Option,
  Input,
  Label,
  Text,
  Tooltip,
} from '@fluentui/react-components';
import {
  AddRegular,
  DeleteRegular,
  InfoRegular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  filterNode: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  filterHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalS,
  },
  filterContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginLeft: tokens.spacingHorizontalXXL,
  },
  inline: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  badge: {
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    borderRadius: tokens.borderRadiusSmall,
    fontSize: tokens.fontSizeBase200,
  },
});

export interface FilterNode {
  type: string;
  id: string;
  solution?: string;
  solutions?: string[];
  sequence?: (string | string[])[];
  children?: FilterNode[];
}

interface AdvancedFilterBuilderProps {
  solutions: string[];
  onFilterChange: (filter: FilterNode | null) => void;
}

export const AdvancedFilterBuilder: React.FC<AdvancedFilterBuilderProps> = ({
  solutions,
  onFilterChange,
}) => {
  const styles = useStyles();
  
  const [rootFilter, setRootFilter] = useState<FilterNode>({
    type: 'AND',
    id: 'root',
    children: [],
  });

  const updateFilter = (updatedFilter: FilterNode) => {
    setRootFilter(updatedFilter);
    onFilterChange(updatedFilter.children && updatedFilter.children.length > 0 ? updatedFilter : null);
  };

  const addChild = (parentId: string, childType: string) => {
    const newChild: FilterNode = {
      type: childType,
      id: `${Date.now()}-${Math.random()}`,
      ...(childType === 'HAS' && { solution: solutions[0] || '' }),
      ...(childType === 'HAS_ANY' && { solutions: [] }),
      ...(childType === 'HAS_ALL' && { solutions: [] }),
      ...(childType === 'HAS_NONE' && { solutions: [] }),
      ...(childType === 'ORDER_STRICT' && { sequence: [] }),
      ...(childType === 'ORDER_FLEX' && { sequence: [] }),
      ...(['AND', 'OR', 'NOT'].includes(childType) && { children: [] }),
    };

    const addToNode = (node: FilterNode): FilterNode => {
      if (node.id === parentId) {
        return {
          ...node,
          children: [...(node.children || []), newChild],
        };
      }
      if (node.children) {
        return {
          ...node,
          children: node.children.map(child => addToNode(child)),
        };
      }
      return node;
    };

    updateFilter(addToNode(rootFilter));
  };

  const removeChild = (nodeId: string) => {
    const removeFromNode = (node: FilterNode): FilterNode => {
      if (node.children) {
        return {
          ...node,
          children: node.children
            .filter(child => child.id !== nodeId)
            .map(child => removeFromNode(child)),
        };
      }
      return node;
    };

    updateFilter(removeFromNode(rootFilter));
  };

  const updateNodeProperty = (nodeId: string, property: string, value: any) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId) {
        return {
          ...node,
          [property]: value,
        };
      }
      if (node.children) {
        return {
          ...node,
          children: node.children.map(child => updateNode(child)),
        };
      }
      return node;
    };

    updateFilter(updateNode(rootFilter));
  };

  const renderNode = (node: FilterNode, depth: number = 0): React.ReactNode => {
    const canDelete = node.id !== 'root';

    return (
      <div key={node.id} className={styles.filterNode} style={{ marginLeft: depth * 20 }}>
        <div className={styles.filterHeader}>
          <span className={styles.badge}>{node.type}</span>
          
          <Tooltip content="Filter type information" relationship="label">
            <Button
              appearance="subtle"
              icon={<InfoRegular />}
              size="small"
            />
          </Tooltip>

          {canDelete && (
            <Button
              appearance="subtle"
              icon={<DeleteRegular />}
              size="small"
              onClick={() => removeChild(node.id)}
            />
          )}
        </div>

        <div className={styles.filterContent}>
          {/* Logical operators with children */}
          {['AND', 'OR', 'NOT'].includes(node.type) && (
            <>
              <div className={styles.inline}>
                <Dropdown
                  placeholder="Add filter..."
                  onOptionSelect={(_, data) => {
                    if (data.optionValue) {
                      addChild(node.id, data.optionValue);
                    }
                  }}
                  size="small"
                >
                  <Option value="HAS">HAS (has solution)</Option>
                  <Option value="HAS_ANY">HAS_ANY (has any of solutions)</Option>
                  <Option value="HAS_ALL">HAS_ALL (has all solutions)</Option>
                  <Option value="HAS_NONE">HAS_NONE (has none of solutions)</Option>
                  <Option value="ORDER_STRICT">ORDER_STRICT (strict sequence)</Option>
                  <Option value="ORDER_FLEX">ORDER_FLEX (flexible sequence)</Option>
                  <Option value="AND">AND</Option>
                  <Option value="OR">OR</Option>
                  <Option value="NOT">NOT</Option>
                </Dropdown>
              </div>

              {node.children && node.children.length > 0 && (
                <div>
                  {node.children.map(child => renderNode(child, depth + 1))}
                </div>
              )}

              {(!node.children || node.children.length === 0) && (
                <Text size={200} italic>No filters added yet</Text>
              )}
            </>
          )}

          {/* HAS filter */}
          {node.type === 'HAS' && (
            <div className={styles.inline}>
              <Label size="small">Solution:</Label>
              <Dropdown
                value={node.solution || ''}
                selectedOptions={node.solution ? [node.solution] : []}
                onOptionSelect={(_, data) => updateNodeProperty(node.id, 'solution', data.optionValue)}
                size="small"
              >
                {solutions.map(sol => (
                  <Option key={sol} value={sol}>{sol}</Option>
                ))}
              </Dropdown>
            </div>
          )}

          {/* HAS_ANY, HAS_ALL, HAS_NONE filters */}
          {['HAS_ANY', 'HAS_ALL', 'HAS_NONE'].includes(node.type) && (
            <div className={styles.inline}>
              <Label size="small">Solutions:</Label>
              <Dropdown
                multiselect
                selectedOptions={node.solutions || []}
                onOptionSelect={(_, data) => {
                  const current = node.solutions || [];
                  const updated = data.selectedOptions;
                  updateNodeProperty(node.id, 'solutions', updated);
                }}
                size="small"
                placeholder="Select solutions..."
              >
                {solutions.map(sol => (
                  <Option key={sol} value={sol}>{sol}</Option>
                ))}
              </Dropdown>
            </div>
          )}

          {/* ORDER_STRICT, ORDER_FLEX filters */}
          {['ORDER_STRICT', 'ORDER_FLEX'].includes(node.type) && (
            <div>
              <Label size="small">
                Sequence (comma-separated, use [] for choice groups):
              </Label>
              <Input
                size="small"
                placeholder="e.g., CoreSolution,[ProjectA,ProjectB],ProjectC"
                value={node.sequence ? JSON.stringify(node.sequence) : ''}
                onChange={(_, data) => {
                  try {
                    const parsed = JSON.parse(data.value);
                    updateNodeProperty(node.id, 'sequence', parsed);
                  } catch {
                    // Invalid JSON, ignore
                  }
                }}
              />
              <Text size={100}>
                Example: ["Core", ["ProjA", "ProjB"], "Final"] means Core, then any of ProjA/ProjB, then Final
              </Text>
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <div className={styles.container}>
      <Card>
        <CardHeader
          header={<Text weight="semibold">Advanced Layer Filters</Text>}
          description="Build complex filter conditions using logical operators and sequence constraints"
        />
        {renderNode(rootFilter)}
      </Card>
    </div>
  );
};
