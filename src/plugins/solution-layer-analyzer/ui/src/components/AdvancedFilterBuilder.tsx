import React, { useState } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Card,
  CardHeader,
  Dropdown,
  Option,
  Label,
  Text,
  Tooltip,
  Tag,
  TagGroup,
  Input,
} from '@fluentui/react-components';
import { DeleteRegular, InfoRegular, AddRegular, DismissRegular, ArrowUpRegular } from '@fluentui/react-icons';
import { FilterNode, AttributeTarget, StringOperator, SolutionQueryNode } from '../types';

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
    flexWrap: 'wrap',
  },
  badge: {
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    borderRadius: tokens.borderRadiusSmall,
    fontSize: tokens.fontSizeBase200,
  },
  addFilterRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  sequenceContainer: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  sequenceStep: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusSmall,
    border: `1px dashed ${tokens.colorNeutralStroke2}`,
  },
  sequenceStepHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  sequenceStepContent: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  solutionTag: {
    display: 'flex',
    alignItems: 'center',
  },
  addLayerButton: {
    marginTop: tokens.spacingVerticalXS,
  },
  stepNumber: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorBrandForeground1,
    minWidth: '20px',
  },
});

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

  // Track selected filter type for each node that can have children
  const [selectedFilterTypes, setSelectedFilterTypes] = useState<Record<string, string>>({});

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
      ...(childType === 'ORDER_STRICT' && { sequence: [[]] }),
      ...(childType === 'ORDER_FLEX' && { sequence: [[]] }),
      ...(childType === 'ATTRIBUTE' && { 
        attribute: AttributeTarget.LogicalName,
        operator: StringOperator.Contains,
        value: ''
      }),
      ...(childType === 'LAYER_QUERY' && { 
        layerFilter: {
          type: 'HAS',
          id: `layer-${Date.now()}-${Math.random()}`,
          solution: solutions[0] || ''
        }
      }),
      ...(childType === 'SOLUTION_QUERY' && { 
        attribute: 'SchemaName',
        operator: StringOperator.BeginsWith,
        value: ''
      }),
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
    // Clear the selected filter type after adding
    setSelectedFilterTypes(prev => ({ ...prev, [parentId]: '' }));
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

  // Helper functions for ORDER sequence manipulation
  const addSolutionToSequenceStep = (nodeId: string, stepIndex: number, solution: string) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = [...node.sequence];
        if (Array.isArray(newSequence[stepIndex])) {
          if (!newSequence[stepIndex].includes(solution)) {
            newSequence[stepIndex] = [...newSequence[stepIndex], solution];
          }
        } else {
          // Convert single solution to array if needed
          const existing = newSequence[stepIndex];
          newSequence[stepIndex] = existing ? [existing, solution] : [solution];
        }
        return { ...node, sequence: newSequence };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const removeSolutionFromSequenceStep = (nodeId: string, stepIndex: number, solution: string) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = [...node.sequence];
        if (Array.isArray(newSequence[stepIndex])) {
          newSequence[stepIndex] = newSequence[stepIndex].filter((s: string) => s !== solution);
        }
        return { ...node, sequence: newSequence };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const addSequenceStep = (nodeId: string) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        return { ...node, sequence: [...node.sequence, []] };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const removeSequenceStep = (nodeId: string, stepIndex: number) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = node.sequence.filter((_: any, i: number) => i !== stepIndex);
        return { ...node, sequence: newSequence.length > 0 ? newSequence : [[]] };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const addSolutionQueryToSequenceStep = (nodeId: string, stepIndex: number, query: SolutionQueryNode) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = [...node.sequence];
        newSequence[stepIndex] = query;
        return { ...node, sequence: newSequence };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const removeSolutionQueryFromSequenceStep = (nodeId: string, stepIndex: number) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = [...node.sequence];
        newSequence[stepIndex] = [];
        return { ...node, sequence: newSequence };
      }
      if (node.children) {
        return { ...node, children: node.children.map(child => updateNode(child)) };
      }
      return node;
    };
    updateFilter(updateNode(rootFilter));
  };

  const isSolutionQuery = (item: any): item is SolutionQueryNode => {
    return item && typeof item === 'object' && 'attribute' in item && 'operator' in item && 'value' in item;
  };

  const renderOrderSequence = (node: FilterNode): React.ReactNode => {
    const sequence = node.sequence || [[]];
    const isStrict = node.type === 'ORDER_STRICT';

    return (
      <div className={styles.sequenceContainer}>
        <Text size={200}>
          {isStrict 
            ? 'Layers must appear in exact order (strict sequence)' 
            : 'Layers must appear in order but may have other layers between (flexible sequence)'}
        </Text>
        
        {sequence.map((step: string | string[] | SolutionQueryNode, stepIndex: number) => {
          // Check if this step is a solution query
          const isQuery = isSolutionQuery(step);
          const stepSolutions = isQuery ? [] : (Array.isArray(step) ? step : (step ? [step] : []));
          const availableSolutions = solutions.filter(s => !stepSolutions.includes(s));
          
          return (
            <div key={stepIndex} className={styles.sequenceStep}>
              <div className={styles.sequenceStepHeader}>
                <span className={styles.stepNumber}>{stepIndex + 1}.</span>
                <Text size={200}>
                  {stepIndex === 0 ? 'Must include layer(s)' : 'Have layer(s) on-top'}
                </Text>
                {sequence.length > 1 && (
                  <Button
                    appearance="subtle"
                    icon={<DeleteRegular />}
                    size="small"
                    onClick={() => removeSequenceStep(node.id, stepIndex)}
                    title="Remove this layer requirement"
                  />
                )}
              </div>
              
              <div className={styles.sequenceStepContent}>
                {isQuery ? (
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Tag dismissible dismissIcon={<DismissRegular />} onClick={() => removeSolutionQueryFromSequenceStep(node.id, stepIndex)}>
                      Query: {step.attribute} {step.operator} "{step.value}"
                    </Tag>
                  </div>
                ) : (
                  <>
                    {stepSolutions.length > 0 && (
                      <TagGroup>
                        {stepSolutions.map((sol: string) => (
                          <Tag
                            key={sol}
                            dismissible
                            dismissIcon={<DismissRegular />}
                            value={sol}
                            onClick={() => removeSolutionFromSequenceStep(node.id, stepIndex, sol)}
                          >
                            {sol}
                          </Tag>
                        ))}
                      </TagGroup>
                    )}
                    
                    {availableSolutions.length > 0 && (
                      <Dropdown
                        placeholder={stepSolutions.length > 0 ? 'Add additional...' : 'Select solution...'}
                        onOptionSelect={(_, data) => {
                          if (data.optionValue) {
                            addSolutionToSequenceStep(node.id, stepIndex, data.optionValue);
                          }
                        }}
                        size="small"
                        selectedOptions={[]}
                        value=""
                      >
                        {availableSolutions.map(sol => (
                          <Option key={sol} value={sol}>{sol}</Option>
                        ))}
                      </Dropdown>
                    )}
                  </>
                )}
                
                {!isQuery && stepSolutions.length === 0 && (
                  <Button
                    appearance="subtle"
                    size="small"
                    onClick={() => {
                      const query: SolutionQueryNode = {
                        attribute: 'SchemaName',
                        operator: StringOperator.BeginsWith,
                        value: ''
                      };
                      addSolutionQueryToSequenceStep(node.id, stepIndex, query);
                    }}
                  >
                    Or use Solution Query
                  </Button>
                )}
              </div>
            </div>
          );
        })}
        
        <Button
          appearance="subtle"
          icon={<ArrowUpRegular />}
          size="small"
          onClick={() => addSequenceStep(node.id)}
          className={styles.addLayerButton}
        >
          Add layer filter on top
        </Button>
      </div>
    );
  };

  const renderNode = (node: FilterNode, depth: number = 0): React.ReactNode => {
    const canDelete = node.id !== 'root';
    const selectedType = selectedFilterTypes[node.id] || '';

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
              <div className={styles.addFilterRow}>
                <Dropdown
                  placeholder="Select filter type..."
                  selectedOptions={selectedType ? [selectedType] : []}
                  value={selectedType}
                  onOptionSelect={(_, data) => {
                    setSelectedFilterTypes(prev => ({ 
                      ...prev, 
                      [node.id]: data.optionValue || '' 
                    }));
                  }}
                  size="small"
                >
                  {/* Component-level filters */}
                  <Option value="ATTRIBUTE">ATTRIBUTE (filter by component attribute)</Option>
                  {/* Nested query filters */}
                  <Option value="LAYER_QUERY">LAYER_QUERY (query layers/solutions)</Option>
                  <Option value="SOLUTION_QUERY">SOLUTION_QUERY (match solution by name)</Option>
                  {/* Legacy layer filters (for backward compatibility) */}
                  <Option value="HAS">HAS (has solution) [Legacy]</Option>
                  <Option value="HAS_ANY">HAS_ANY (has any of solutions) [Legacy]</Option>
                  <Option value="HAS_ALL">HAS_ALL (has all solutions) [Legacy]</Option>
                  <Option value="HAS_NONE">HAS_NONE (has none of solutions) [Legacy]</Option>
                  <Option value="ORDER_STRICT">ORDER_STRICT (strict sequence) [Legacy]</Option>
                  <Option value="ORDER_FLEX">ORDER_FLEX (flexible sequence) [Legacy]</Option>
                  {/* Logical operators */}
                  <Option value="AND">AND</Option>
                  <Option value="OR">OR</Option>
                  <Option value="NOT">NOT</Option>
                </Dropdown>
                <Button
                  appearance="primary"
                  icon={<AddRegular />}
                  size="small"
                  disabled={!selectedType}
                  onClick={() => {
                    if (selectedType) {
                      addChild(node.id, selectedType);
                    }
                  }}
                >
                  Add
                </Button>
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
          {['ORDER_STRICT', 'ORDER_FLEX'].includes(node.type) && renderOrderSequence(node)}

          {/* ATTRIBUTE filter */}
          {node.type === 'ATTRIBUTE' && (
            <div className={styles.inline}>
              <Label size="small">Attribute:</Label>
              <Dropdown
                value={node.attribute || AttributeTarget.LogicalName}
                selectedOptions={node.attribute ? [node.attribute] : [AttributeTarget.LogicalName]}
                onOptionSelect={(_, data) => updateNodeProperty(node.id, 'attribute', data.optionValue)}
                size="small"
              >
                <Option value={AttributeTarget.LogicalName}>Logical Name</Option>
                <Option value={AttributeTarget.DisplayName}>Display Name</Option>
                <Option value={AttributeTarget.ComponentType}>Component Type</Option>
                <Option value={AttributeTarget.Publisher}>Publisher</Option>
                <Option value={AttributeTarget.TableLogicalName}>Table Logical Name</Option>
              </Dropdown>

              <Label size="small">Operator:</Label>
              <Dropdown
                value={node.operator || StringOperator.Contains}
                selectedOptions={node.operator ? [node.operator] : [StringOperator.Contains]}
                onOptionSelect={(_, data) => updateNodeProperty(node.id, 'operator', data.optionValue)}
                size="small"
              >
                <Option value={StringOperator.Equals}>Equals</Option>
                <Option value={StringOperator.NotEquals}>Not Equals</Option>
                <Option value={StringOperator.Contains}>Contains</Option>
                <Option value={StringOperator.NotContains}>Not Contains</Option>
                <Option value={StringOperator.BeginsWith}>Begins With</Option>
                <Option value={StringOperator.NotBeginsWith}>Not Begins With</Option>
                <Option value={StringOperator.EndsWith}>Ends With</Option>
                <Option value={StringOperator.NotEndsWith}>Not Ends With</Option>
              </Dropdown>

              <Label size="small">Value:</Label>
              <Input
                value={node.value || ''}
                onChange={(_, data) => updateNodeProperty(node.id, 'value', data.value)}
                size="small"
                placeholder="Enter value..."
              />
            </div>
          )}

          {/* LAYER_QUERY filter */}
          {node.type === 'LAYER_QUERY' && (
            <div style={{ marginLeft: tokens.spacingHorizontalL }}>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalS, display: 'block' }}>
                Layer Filter (nested):
              </Text>
              {node.layerFilter && renderNode(node.layerFilter, depth + 1)}
              {!node.layerFilter && (
                <Text size={200} italic>No layer filter set</Text>
              )}
            </div>
          )}

          {/* SOLUTION_QUERY filter */}
          {node.type === 'SOLUTION_QUERY' && (
            <div className={styles.inline}>
              <Label size="small">Attribute:</Label>
              <Input
                value={node.attribute || 'SchemaName'}
                onChange={(_, data) => updateNodeProperty(node.id, 'attribute', data.value)}
                size="small"
                placeholder="SchemaName"
              />

              <Label size="small">Operator:</Label>
              <Dropdown
                value={node.operator || StringOperator.BeginsWith}
                selectedOptions={node.operator ? [node.operator] : [StringOperator.BeginsWith]}
                onOptionSelect={(_, data) => updateNodeProperty(node.id, 'operator', data.optionValue)}
                size="small"
              >
                <Option value={StringOperator.Equals}>Equals</Option>
                <Option value={StringOperator.NotEquals}>Not Equals</Option>
                <Option value={StringOperator.Contains}>Contains</Option>
                <Option value={StringOperator.NotContains}>Not Contains</Option>
                <Option value={StringOperator.BeginsWith}>Begins With</Option>
                <Option value={StringOperator.NotBeginsWith}>Not Begins With</Option>
                <Option value={StringOperator.EndsWith}>Ends With</Option>
                <Option value={StringOperator.NotEndsWith}>Not Ends With</Option>
              </Dropdown>

              <Label size="small">Value:</Label>
              <Input
                value={node.value || ''}
                onChange={(_, data) => updateNodeProperty(node.id, 'value', data.value)}
                size="small"
                placeholder="Enter value..."
              />
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
