import React, { useState, useCallback, useMemo } from 'react';
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
  Checkbox,
  Radio,
  RadioGroup,
} from '@fluentui/react-components';
import { DeleteRegular, InfoRegular, AddRegular, DismissRegular, ArrowUpRegular } from '@fluentui/react-icons';
import { FilterNode, AttributeTarget, StringOperator, SolutionQueryNode, AttributeDiffTargetMode, AttributeMatchLogic } from '../types';

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
  initialFilter?: FilterNode | null;
  onFilterChange: (filter: FilterNode | null) => void;
}

export const AdvancedFilterBuilder: React.FC<AdvancedFilterBuilderProps> = ({
  solutions,
  initialFilter,
  onFilterChange,
}) => {
  const styles = useStyles();
  
  // The filter is now controlled by the parent (useFilter hook)
  // We use the initialFilter directly and call onFilterChange to update it
  const rootFilter: FilterNode = useMemo(() => {
    if (initialFilter && initialFilter.type === 'AND') {
      return {
        ...initialFilter,
        id: initialFilter.id || 'root',
      };
    }
    if (initialFilter) {
      // Wrap non-AND filter in an AND root
      return {
        type: 'AND',
        id: 'root',
        children: [initialFilter],
      };
    }
    return {
      type: 'AND',
      id: 'root',
      children: [],
    };
  }, [initialFilter]);

  // Track selected filter type for each node that can have children
  const [selectedFilterTypes, setSelectedFilterTypes] = useState<Record<string, string>>({});

  // Update function that calls parent's onFilterChange
  const updateFilter = useCallback((updatedFilter: FilterNode) => {
    const filterToSend = updatedFilter.children && updatedFilter.children.length > 0 ? updatedFilter : null;
    onFilterChange(filterToSend);
  }, [onFilterChange]);

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
      ...(childType === 'LAYER_ATTRIBUTE_QUERY' && {
        solution: solutions[0] || '',
        attributeFilter: undefined
      }),
      ...(childType === 'HAS_ATTRIBUTE_DIFF' && {
        sourceSolution: solutions[0] || '',
        targetMode: AttributeDiffTargetMode.AllBelow,
        targetSolutions: [],
        onlyChangedAttributes: true,
        attributeNames: [],
        attributeMatchLogic: AttributeMatchLogic.Any
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
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => addToNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: addToNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: addToNode(node.attributeFilter) };
      }
      return updated;
    };

    updateFilter(addToNode(rootFilter));
    // Clear the selected filter type after adding
    setSelectedFilterTypes(prev => ({ ...prev, [parentId]: '' }));
  };

  const removeChild = (nodeId: string) => {
    const removeFromNode = (node: FilterNode): FilterNode => {
      let updated = node;
      if (node.children) {
        updated = {
          ...updated,
          children: node.children
            .filter(child => child.id !== nodeId)
            .map(child => removeFromNode(child)),
        };
      }
      if (node.layerFilter) {
        // If layerFilter itself is the target, we shouldn't remove it entirely - leave it
        // But we should recurse into it to remove children
        updated = { ...updated, layerFilter: removeFromNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: removeFromNode(node.attributeFilter) };
      }
      return updated;
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
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
    };

    updateFilter(updateNode(rootFilter));
  };

  // Helper functions for ORDER sequence manipulation
  const addSolutionToSequenceStep = (nodeId: string, stepIndex: number, solution: string) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = [...node.sequence];
        const stepItem = newSequence[stepIndex];
        if (Array.isArray(stepItem)) {
          if (!stepItem.includes(solution)) {
            newSequence[stepIndex] = [...stepItem, solution];
          }
        } else if (typeof stepItem === 'string') {
          // Convert single solution string to array
          newSequence[stepIndex] = [stepItem, solution];
        } else {
          // stepItem is SolutionQueryNode or undefined, start fresh array
          newSequence[stepIndex] = [solution];
        }
        return { ...node, sequence: newSequence };
      }
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
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
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
    };
    updateFilter(updateNode(rootFilter));
  };

  const addSequenceStep = (nodeId: string) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        return { ...node, sequence: [...node.sequence, []] };
      }
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
    };
    updateFilter(updateNode(rootFilter));
  };

  const removeSequenceStep = (nodeId: string, stepIndex: number) => {
    const updateNode = (node: FilterNode): FilterNode => {
      if (node.id === nodeId && node.sequence) {
        const newSequence = node.sequence.filter((_: any, i: number) => i !== stepIndex);
        return { ...node, sequence: newSequence.length > 0 ? newSequence : [[]] };
      }
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
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
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
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
      let updated = node;
      if (node.children) {
        updated = { ...updated, children: node.children.map(child => updateNode(child)) };
      }
      if (node.layerFilter) {
        updated = { ...updated, layerFilter: updateNode(node.layerFilter) };
      }
      if (node.attributeFilter) {
        updated = { ...updated, attributeFilter: updateNode(node.attributeFilter) };
      }
      return updated;
    };
    updateFilter(updateNode(rootFilter));
  };

  const isSolutionQuery = (item: any): item is SolutionQueryNode => {
    return item && typeof item === 'object' && 'attribute' in item && 'operator' in item && 'value' in item;
  };

  // All available component types (static list)
  const allComponentTypes = [
    'Entity',
    'Attribute',
    'SystemForm',
    'SavedQuery',
    'SavedQueryVisualization',
    'RibbonCustomization',
    'WebResource',
    'SDKMessageProcessingStep',
    'Workflow',
    'AppModule',
    'SiteMap',
    'OptionSet',
    'PluginAssembly',
    'PluginType',
    'ServiceEndpoint',
    'CustomAPI',
    'Report',
    'EmailTemplate',
    'Dashboard',
    'Chart',
  ].sort();

  // Helper to get appropriate operators for a given attribute type
  const getOperatorsForAttribute = (attribute: AttributeTarget | string | undefined) => {
    // For ComponentType and TableLogicalName, only show Equals/NotEquals (IS/IS NOT)
    if (attribute === AttributeTarget.ComponentType || attribute === AttributeTarget.TableLogicalName) {
      return [
        { value: StringOperator.Equals, label: 'IS (Equals)' },
        { value: StringOperator.NotEquals, label: 'IS NOT (Not Equals)' },
      ];
    }
    
    // For all other string attributes, show all operators
    return [
      { value: StringOperator.Equals, label: 'Equals' },
      { value: StringOperator.NotEquals, label: 'Not Equals' },
      { value: StringOperator.Contains, label: 'Contains' },
      { value: StringOperator.NotContains, label: 'Not Contains' },
      { value: StringOperator.BeginsWith, label: 'Begins With' },
      { value: StringOperator.NotBeginsWith, label: 'Not Begins With' },
      { value: StringOperator.EndsWith, label: 'Ends With' },
      { value: StringOperator.NotEndsWith, label: 'Not Ends With' },
    ];
  };

  // Helper to determine what filter types are available based on context
  // This implements the hierarchical filter architecture from the requirements
  // AND/OR/NOT nodes inherit from their context: root-level or layer-level
  const getAvailableFilterTypes = (parentNode: FilterNode, isInLayerFilter: boolean = false, isInLayerAttributeFilter: boolean = false) => {
    const isOrderSequenceContext = parentNode.type === 'ORDER_STRICT' || parentNode.type === 'ORDER_FLEX';
    const isLayerAttributeContext = isInLayerAttributeFilter || parentNode.type === 'LAYER_ATTRIBUTE_QUERY';
    const isLayerQueryContext = isInLayerFilter || parentNode.type === 'LAYER_QUERY' || 
                                 (parentNode.layerFilter !== undefined);
    
    const options: Array<{value: string, label: string, category?: string}> = [];
    
    if (isLayerAttributeContext) {
      // Inside LAYER_ATTRIBUTE_QUERY - layer attribute predicates
      options.push(
        { value: 'HAS_RELEVANT_CHANGES', label: 'HAS_RELEVANT_CHANGES (has meaningful changes)', category: 'Layer Attribute Predicates' },
        { value: 'HAS_ATTRIBUTE_DIFF', label: 'HAS_ATTRIBUTE_DIFF (attributes differ from target)', category: 'Layer Attribute Predicates' },
        { value: 'AND', label: 'AND', category: 'Logical Operators' },
        { value: 'OR', label: 'OR', category: 'Logical Operators' },
        { value: 'NOT', label: 'NOT', category: 'Logical Operators' }
      );
    } else if (isOrderSequenceContext) {
      // Inside ORDER sequences - limited options
      options.push(
        { value: 'SOLUTION_QUERY', label: 'SOLUTION_QUERY (dynamic solution)', category: 'Nested Queries' },
        { value: 'AND', label: 'AND', category: 'Logical Operators' },
        { value: 'OR', label: 'OR', category: 'Logical Operators' },
        { value: 'NOT', label: 'NOT', category: 'Logical Operators' }
      );
    } else if (isLayerQueryContext) {
      // Inside LAYER_QUERY - allow layer filters
      // AND/OR/NOT inside layer context also get layer filters
      options.push(
        { value: 'HAS', label: 'HAS (has specific solution)', category: 'Layer Filters' },
        { value: 'HAS_ANY', label: 'HAS_ANY (has any of solutions)', category: 'Layer Filters' },
        { value: 'HAS_ALL', label: 'HAS_ALL (has all solutions)', category: 'Layer Filters' },
        { value: 'HAS_NONE', label: 'HAS_NONE (has none of solutions)', category: 'Layer Filters' },
        { value: 'ORDER_STRICT', label: 'ORDER_STRICT (exact sequence)', category: 'Layer Filters' },
        { value: 'ORDER_FLEX', label: 'ORDER_FLEX (flexible sequence)', category: 'Layer Filters' },
        { value: 'LAYER_ATTRIBUTE_QUERY', label: 'LAYER_ATTRIBUTE_QUERY (query layer attributes)', category: 'Nested Queries' },
        { value: 'SOLUTION_QUERY', label: 'SOLUTION_QUERY (dynamic solution)', category: 'Nested Queries' },
        { value: 'AND', label: 'AND', category: 'Logical Operators' },
        { value: 'OR', label: 'OR', category: 'Logical Operators' },
        { value: 'NOT', label: 'NOT', category: 'Logical Operators' }
      );
    } else {
      // Root level / Component-level filters
      // AND/OR/NOT at root level inherit the full root set
      options.push(
        { value: 'ATTRIBUTE', label: 'ATTRIBUTE (filter by component attribute)', category: 'Component Filters' },
        { value: 'LAYER_QUERY', label: 'LAYER_QUERY (query layers/solutions)', category: 'Nested Queries' },
        { value: 'SOLUTION_QUERY', label: 'SOLUTION_QUERY (match solution by name)', category: 'Nested Queries' },
        { value: 'AND', label: 'AND', category: 'Logical Operators' },
        { value: 'OR', label: 'OR', category: 'Logical Operators' },
        { value: 'NOT', label: 'NOT', category: 'Logical Operators' }
      );
    }
    
    return options;
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

  const renderNode = (node: FilterNode, depth: number = 0, isInLayerFilter: boolean = false, isInLayerAttributeFilter: boolean = false): React.ReactNode => {
    const canDelete = node.id !== 'root';
    const selectedType = selectedFilterTypes[node.id] || '';
    const availableTypes = getAvailableFilterTypes(node, isInLayerFilter, isInLayerAttributeFilter);

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
                  {availableTypes.map(opt => (
                    <Option key={opt.value} value={opt.value}>{opt.label}</Option>
                  ))}
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
                  {node.children.map(child => renderNode(child, depth + 1, isInLayerFilter, isInLayerAttributeFilter))}
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
                onOptionSelect={(_, data) => {
                  updateNodeProperty(node.id, 'attribute', data.optionValue);
                  // Reset operator to default when attribute changes
                  const newAttribute = data.optionValue as AttributeTarget;
                  const operators = getOperatorsForAttribute(newAttribute);
                  if (operators.length > 0) {
                    updateNodeProperty(node.id, 'operator', operators[0].value);
                  }
                }}
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
                {getOperatorsForAttribute(node.attribute).map(op => (
                  <Option key={op.value} value={op.value}>{op.label}</Option>
                ))}
              </Dropdown>

              <Label size="small">Value:</Label>
              {/* Show dropdown for ComponentType, text input for others */}
              {node.attribute === AttributeTarget.ComponentType ? (
                <Dropdown
                  value={node.value || ''}
                  selectedOptions={node.value ? [node.value] : []}
                  onOptionSelect={(_, data) => updateNodeProperty(node.id, 'value', data.optionValue)}
                  size="small"
                  placeholder="Select component type..."
                >
                  {allComponentTypes.map(type => (
                    <Option key={type} value={type}>{type}</Option>
                  ))}
                </Dropdown>
              ) : (
                <Input
                  value={node.value || ''}
                  onChange={(_, data) => updateNodeProperty(node.id, 'value', data.value)}
                  size="small"
                  placeholder="Enter value..."
                />
              )}
            </div>
          )}

          {/* LAYER_QUERY filter */}
          {node.type === 'LAYER_QUERY' && (
            <div style={{ marginLeft: tokens.spacingHorizontalL }}>
              <div className={styles.inline} style={{ marginBottom: tokens.spacingVerticalS }}>
                <Label size="small">Layer Filter Type:</Label>
                <Dropdown
                  placeholder="Select layer filter..."
                  value={node.layerFilter?.type || 'HAS'}
                  selectedOptions={node.layerFilter?.type ? [node.layerFilter.type] : ['HAS']}
                  onOptionSelect={(_, data) => {
                    const newType = data.optionValue || 'HAS';
                    // Create new layer filter based on type
                    let newLayerFilter: FilterNode;
                    switch (newType) {
                      case 'HAS':
                        newLayerFilter = { type: 'HAS', id: `layer-${Date.now()}-${Math.random()}`, solution: solutions[0] || '' };
                        break;
                      case 'HAS_ANY':
                        newLayerFilter = { type: 'HAS_ANY', id: `layer-${Date.now()}-${Math.random()}`, solutions: [] };
                        break;
                      case 'HAS_ALL':
                        newLayerFilter = { type: 'HAS_ALL', id: `layer-${Date.now()}-${Math.random()}`, solutions: [] };
                        break;
                      case 'HAS_NONE':
                        newLayerFilter = { type: 'HAS_NONE', id: `layer-${Date.now()}-${Math.random()}`, solutions: [] };
                        break;
                      case 'ORDER_STRICT':
                        newLayerFilter = { type: 'ORDER_STRICT', id: `layer-${Date.now()}-${Math.random()}`, sequence: [[]] };
                        break;
                      case 'ORDER_FLEX':
                        newLayerFilter = { type: 'ORDER_FLEX', id: `layer-${Date.now()}-${Math.random()}`, sequence: [[]] };
                        break;
                      case 'AND':
                        newLayerFilter = { type: 'AND', id: `layer-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      case 'OR':
                        newLayerFilter = { type: 'OR', id: `layer-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      case 'NOT':
                        newLayerFilter = { type: 'NOT', id: `layer-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      case 'LAYER_ATTRIBUTE_QUERY':
                        newLayerFilter = { type: 'LAYER_ATTRIBUTE_QUERY', id: `layer-${Date.now()}-${Math.random()}`, solution: solutions[0] || '', attributeFilter: undefined };
                        break;
                      default:
                        newLayerFilter = { type: 'HAS', id: `layer-${Date.now()}-${Math.random()}`, solution: solutions[0] || '' };
                    }
                    updateNodeProperty(node.id, 'layerFilter', newLayerFilter);
                  }}
                  size="small"
                >
                  <Option value="HAS">HAS (has specific solution)</Option>
                  <Option value="HAS_ANY">HAS_ANY (has any of solutions)</Option>
                  <Option value="HAS_ALL">HAS_ALL (has all solutions)</Option>
                  <Option value="HAS_NONE">HAS_NONE (has none of solutions)</Option>
                  <Option value="ORDER_STRICT">ORDER_STRICT (exact sequence)</Option>
                  <Option value="ORDER_FLEX">ORDER_FLEX (flexible sequence)</Option>
                  <Option value="AND">AND (combine layer filters)</Option>
                  <Option value="OR">OR (any layer filter matches)</Option>
                  <Option value="NOT">NOT (negate layer filter)</Option>
                  <Option value="LAYER_ATTRIBUTE_QUERY">LAYER_ATTRIBUTE_QUERY (query layer attributes)</Option>
                </Dropdown>
              </div>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalS, display: 'block' }}>
                Layer Filter Configuration:
              </Text>
              {node.layerFilter && renderNode(node.layerFilter, depth + 1, true, false)}
              {!node.layerFilter && (
                <Text size={200} italic>No layer filter set</Text>
              )}
            </div>
          )}

          {/* LAYER_ATTRIBUTE_QUERY filter */}
          {node.type === 'LAYER_ATTRIBUTE_QUERY' && (
            <div style={{ marginLeft: tokens.spacingHorizontalL }}>
              <div className={styles.inline} style={{ marginBottom: tokens.spacingVerticalS }}>
                <Label size="small">Target Solution:</Label>
                <Dropdown
                  value={node.solution || ''}
                  selectedOptions={node.solution ? [node.solution] : []}
                  onOptionSelect={(_, data) => updateNodeProperty(node.id, 'solution', data.optionValue)}
                  size="small"
                  placeholder="Select solution..."
                >
                  {solutions.map(sol => (
                    <Option key={sol} value={sol}>{sol}</Option>
                  ))}
                </Dropdown>
              </div>
              <div className={styles.inline} style={{ marginBottom: tokens.spacingVerticalS }}>
                <Label size="small">Attribute Filter:</Label>
                <Dropdown
                  placeholder="Select attribute filter..."
                  value={node.attributeFilter?.type || ''}
                  selectedOptions={node.attributeFilter?.type ? [node.attributeFilter.type] : []}
                  onOptionSelect={(_, data) => {
                    const newType = data.optionValue || 'HAS_RELEVANT_CHANGES';
                    let newAttributeFilter: FilterNode;
                    switch (newType) {
                      case 'HAS_RELEVANT_CHANGES':
                        newAttributeFilter = { type: 'HAS_RELEVANT_CHANGES', id: `attr-${Date.now()}-${Math.random()}` };
                        break;
                      case 'HAS_ATTRIBUTE_DIFF':
                        newAttributeFilter = {
                          type: 'HAS_ATTRIBUTE_DIFF',
                          id: `attr-${Date.now()}-${Math.random()}`,
                          sourceSolution: solutions[0] || '',
                          targetMode: AttributeDiffTargetMode.AllBelow,
                          targetSolutions: [],
                          onlyChangedAttributes: true,
                          attributeNames: [],
                          attributeMatchLogic: AttributeMatchLogic.Any
                        };
                        break;
                      case 'AND':
                        newAttributeFilter = { type: 'AND', id: `attr-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      case 'OR':
                        newAttributeFilter = { type: 'OR', id: `attr-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      case 'NOT':
                        newAttributeFilter = { type: 'NOT', id: `attr-${Date.now()}-${Math.random()}`, children: [] };
                        break;
                      default:
                        newAttributeFilter = { type: 'HAS_RELEVANT_CHANGES', id: `attr-${Date.now()}-${Math.random()}` };
                    }
                    updateNodeProperty(node.id, 'attributeFilter', newAttributeFilter);
                  }}
                  size="small"
                >
                  <Option value="HAS_RELEVANT_CHANGES">HAS_RELEVANT_CHANGES (has meaningful changes)</Option>
                  <Option value="HAS_ATTRIBUTE_DIFF">HAS_ATTRIBUTE_DIFF (attributes differ from target)</Option>
                  <Option value="AND">AND (combine attribute filters)</Option>
                  <Option value="OR">OR (any attribute filter matches)</Option>
                  <Option value="NOT">NOT (negate attribute filter)</Option>
                </Dropdown>
              </div>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalS, display: 'block' }}>
                Attribute Filter Configuration:
              </Text>
              {node.attributeFilter && renderNode(node.attributeFilter, depth + 1, false, true)}
              {!node.attributeFilter && (
                <Text size={200} italic>No attribute filter set</Text>
              )}
            </div>
          )}

          {/* HAS_RELEVANT_CHANGES filter - leaf predicate */}
          {node.type === 'HAS_RELEVANT_CHANGES' && (
            <div className={styles.inline}>
              <Text size={200}>
                Layer has meaningful changes (excludes system fields like modifiedon, solutionid, etc.)
              </Text>
            </div>
          )}

          {/* HAS_ATTRIBUTE_DIFF filter - compare attributes between layers */}
          {node.type === 'HAS_ATTRIBUTE_DIFF' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS }}>
              {/* Source Solution */}
              <div className={styles.inline}>
                <Label size="small">Source Solution:</Label>
                <Dropdown
                  value={node.sourceSolution || ''}
                  selectedOptions={node.sourceSolution ? [node.sourceSolution] : []}
                  onOptionSelect={(_, data) => updateNodeProperty(node.id, 'sourceSolution', data.optionValue)}
                  size="small"
                  placeholder="Select source solution..."
                >
                  {solutions.map(sol => (
                    <Option key={sol} value={sol}>{sol}</Option>
                  ))}
                </Dropdown>
              </div>

              {/* Target Mode */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}>
                <Label size="small">Compare Against:</Label>
                <RadioGroup
                  value={node.targetMode || AttributeDiffTargetMode.AllBelow}
                  onChange={(_, data) => updateNodeProperty(node.id, 'targetMode', data.value)}
                >
                  <Radio value={AttributeDiffTargetMode.AllBelow} label="All layers below (any layer with lower ordinal)" />
                  <Radio value={AttributeDiffTargetMode.Specific} label="Specific solutions" />
                </RadioGroup>
              </div>

              {/* Target Solutions (shown only for Specific mode) */}
              {node.targetMode === AttributeDiffTargetMode.Specific && (
                <div className={styles.inline}>
                  <Label size="small">Target Solutions:</Label>
                  <Dropdown
                    multiselect
                    selectedOptions={node.targetSolutions || []}
                    onOptionSelect={(_, data) => {
                      updateNodeProperty(node.id, 'targetSolutions', data.selectedOptions);
                    }}
                    size="small"
                    placeholder="Select target solutions..."
                  >
                    {solutions
                      .filter(sol => sol !== node.sourceSolution)
                      .map(sol => (
                        <Option key={sol} value={sol}>{sol}</Option>
                      ))}
                  </Dropdown>
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    (diff if source differs from ANY target)
                  </Text>
                </div>
              )}

              {/* Only Changed Attributes */}
              <Checkbox
                checked={node.onlyChangedAttributes !== false}
                onChange={(_, data) => updateNodeProperty(node.id, 'onlyChangedAttributes', data.checked)}
                label="Only check attributes marked as changed in source"
              />

              {/* Attribute Scope */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}>
                <Label size="small">Attribute Scope:</Label>
                <RadioGroup
                  value={(node.attributeNames && node.attributeNames.length > 0) ? 'specific' : 'any'}
                  onChange={(_, data) => {
                    if (data.value === 'any') {
                      updateNodeProperty(node.id, 'attributeNames', []);
                    }
                    // For 'specific', keep existing or empty - user will add via input
                  }}
                >
                  <Radio value="any" label="Any attribute" />
                  <Radio value="specific" label="Specific attributes" />
                </RadioGroup>
              </div>

              {/* Specific Attribute Names */}
              {node.attributeNames && node.attributeNames.length > 0 && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}>
                  <div className={styles.inline}>
                    <Label size="small">Attribute Names:</Label>
                    <TagGroup>
                      {node.attributeNames.map((name: string) => (
                        <Tag
                          key={name}
                          dismissible
                          dismissIcon={<DismissRegular />}
                          onClick={() => {
                            const updated = (node.attributeNames || []).filter((n: string) => n !== name);
                            updateNodeProperty(node.id, 'attributeNames', updated);
                          }}
                        >
                          {name}
                        </Tag>
                      ))}
                    </TagGroup>
                  </div>
                  
                  {/* Match Logic */}
                  <div className={styles.inline}>
                    <Label size="small">Match Logic:</Label>
                    <RadioGroup
                      layout="horizontal"
                      value={node.attributeMatchLogic || AttributeMatchLogic.Any}
                      onChange={(_, data) => updateNodeProperty(node.id, 'attributeMatchLogic', data.value)}
                    >
                      <Radio value={AttributeMatchLogic.Any} label="ANY differs" />
                      <Radio value={AttributeMatchLogic.All} label="ALL differ" />
                    </RadioGroup>
                  </div>
                </div>
              )}

              {/* Add attribute name input */}
              {((node.attributeNames && node.attributeNames.length > 0) || 
                (document.querySelector(`[data-attr-scope-specific="${node.id}"]`) as any)?.checked) && (
                <div className={styles.inline}>
                  <Input
                    placeholder="Enter attribute name and press Enter..."
                    size="small"
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        const input = e.target as HTMLInputElement;
                        const value = input.value.trim();
                        if (value && !(node.attributeNames || []).includes(value)) {
                          updateNodeProperty(node.id, 'attributeNames', [...(node.attributeNames || []), value]);
                          input.value = '';
                        }
                      }
                    }}
                  />
                </div>
              )}

              {/* Always show add attribute input when "specific" is selected */}
              {(!node.attributeNames || node.attributeNames.length === 0) && (
                <div className={styles.inline} style={{ marginLeft: tokens.spacingHorizontalL }}>
                  <Input
                    placeholder="Enter attribute name and press Enter to add..."
                    size="small"
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        const input = e.target as HTMLInputElement;
                        const value = input.value.trim();
                        if (value) {
                          updateNodeProperty(node.id, 'attributeNames', [value]);
                          input.value = '';
                        }
                      }
                    }}
                  />
                </div>
              )}

              <Text size={200} style={{ color: tokens.colorNeutralForeground3, fontStyle: 'italic' }}>
                Checks if source layer attributes differ from target layer(s) based on attribute hash comparison.
              </Text>
            </div>
          )}

          {/* SOLUTION_QUERY filter */}
          {node.type === 'SOLUTION_QUERY' && (
            <div className={styles.inline}>
              <Label size="small">Solution Attribute:</Label>
              <Dropdown
                value={node.attribute || 'SchemaName'}
                selectedOptions={node.attribute ? [node.attribute] : ['SchemaName']}
                onOptionSelect={(_, data) => updateNodeProperty(node.id, 'attribute', data.optionValue)}
                size="small"
              >
                <Option value="SchemaName">Schema Name (unique name)</Option>
                <Option value="FriendlyName">Friendly Name (display name)</Option>
                <Option value="PublisherName">Publisher Name</Option>
                <Option value="Version">Version</Option>
              </Dropdown>

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
