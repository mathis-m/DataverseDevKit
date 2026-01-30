import React, { useState, useMemo } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Input,
  Field,
  InfoLabel,
  Spinner,
  Badge,
  Divider,
} from '@fluentui/react-components';
import {
  DocumentTextRegular,
  ArrowLeftRegular,
  SearchRegular,
} from '@fluentui/react-icons';
import { DiffEditor } from '@monaco-editor/react';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
import { AttributeDiff as AttributeDiffType } from '../types';

const useStyles = makeStyles({
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  inputRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'flex-end',
  },
  backButton: {
    marginBottom: tokens.spacingVerticalM,
  },
  attributesList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    maxHeight: '70vh',
    overflowY: 'auto',
    padding: tokens.spacingVerticalS,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  attributeItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  attributeHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  simpleDiff: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
    fontFamily: 'monospace',
    fontSize: '12px',
  },
  diffValue: {
    padding: tokens.spacingVerticalXS,
    borderRadius: tokens.borderRadiusSmall,
    wordBreak: 'break-word',
  },
  leftValue: {
    backgroundColor: tokens.colorPaletteRedBackground2,
    borderLeft: `3px solid ${tokens.colorPaletteRedBorder2}`,
  },
  rightValue: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    borderLeft: `3px solid ${tokens.colorPaletteGreenBorder2}`,
  },
  onlyLeft: {
    backgroundColor: tokens.colorPaletteRedBackground1,
  },
  onlyRight: {
    backgroundColor: tokens.colorPaletteGreenBackground1,
  },
  editorContainer: {
    height: '300px',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
});

interface DiffTabProps {
  initialComponentId?: string;
  initialLeftSolution?: string;
  initialRightSolution?: string;
  onNavigateBack?: () => void;
}

export const DiffTab: React.FC<DiffTabProps> = ({
  initialComponentId,
  initialLeftSolution,
  initialRightSolution,
  onNavigateBack,
}) => {
  const styles = useStyles();
  const { diffComponentLayers, loading } = usePluginApi();
  const { diffState, setDiffState } = useAppStore();
  
  // Initialize from props or store
  const [componentId, setComponentId] = useState(initialComponentId || diffState?.componentId || '');
  const [leftSolution, setLeftSolution] = useState(initialLeftSolution || diffState?.leftSolution || '');
  const [rightSolution, setRightSolution] = useState(initialRightSolution || diffState?.rightSolution || '');
  const [attributeDiffs, setAttributeDiffs] = useState<AttributeDiffType[]>(diffState?.attributeDiffs || []);
  const [warnings, setWarnings] = useState<string[]>(diffState?.warnings || []);
  const [searchTerm, setSearchTerm] = useState(diffState?.searchTerm || '');

  // Sync state to store when it changes
  React.useEffect(() => {
    setDiffState({
      componentId,
      leftSolution,
      rightSolution,
      attributeDiffs,
      warnings,
      searchTerm,
    });
  }, [componentId, leftSolution, rightSolution, attributeDiffs, warnings, searchTerm, setDiffState]);

  // Attribute type enum values from backend
  const AttributeTypeEnum = {
    String: 0,
    Number: 1,
    Boolean: 2,
    DateTime: 3,
    Json: 4,
    Xml: 5,
    EntityReference: 6,
    OptionSet: 7,
    Money: 8,
    Lookup: 9,
  };

  // Filter diffs based on search term
  const filteredDiffs = useMemo(() => {
    if (!searchTerm) return attributeDiffs;
    
    const term = searchTerm.toLowerCase();
    return attributeDiffs.filter(diff => {
      const keyMatch = diff.attributeName.toLowerCase().includes(term);
      const leftValueMatch = (diff.leftValue ?? '').toLowerCase().includes(term);
      const rightValueMatch = (diff.rightValue ?? '').toLowerCase().includes(term);
      return keyMatch || leftValueMatch || rightValueMatch;
    });
  }, [attributeDiffs, searchTerm]);

  const handleDiff = async () => {
    if (!componentId || !leftSolution || !rightSolution) return;

    try {
      const result = await diffComponentLayers(componentId, leftSolution, rightSolution);
      setAttributeDiffs(result.attributes || []);
      setWarnings(result.warnings || []);
    } catch (error) {
      console.error('Diff failed:', error);
    }
  };

  // Auto-load if all parameters provided
  React.useEffect(() => {
    if (initialComponentId && initialLeftSolution && initialRightSolution) {
      handleDiff();
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Render simple diff (for primitive values)
  const renderSimpleDiff = (diff: AttributeDiffType) => {
    if (diff.onlyInLeft) {
      return (
        <div className={styles.simpleDiff}>
          <div className={`${styles.diffValue} ${styles.leftValue}`}>
            <Text size={200} weight="semibold">Left ({leftSolution}):</Text>
            <Text size={200}>{diff.leftValue || '(null)'}</Text>
          </div>
          <div className={`${styles.diffValue} ${styles.onlyLeft}`}>
            <Text size={200} weight="semibold">Right ({rightSolution}):</Text>
            <Text size={200} italic>(not present)</Text>
          </div>
        </div>
      );
    }
    
    if (diff.onlyInRight) {
      return (
        <div className={styles.simpleDiff}>
          <div className={`${styles.diffValue} ${styles.onlyRight}`}>
            <Text size={200} weight="semibold">Left ({leftSolution}):</Text>
            <Text size={200} italic>(not present)</Text>
          </div>
          <div className={`${styles.diffValue} ${styles.rightValue}`}>
            <Text size={200} weight="semibold">Right ({rightSolution}):</Text>
            <Text size={200}>{diff.rightValue || '(null)'}</Text>
          </div>
        </div>
      );
    }
    
    return (
      <div className={styles.simpleDiff}>
        <div className={`${styles.diffValue} ${styles.leftValue}`}>
          <Text size={200} weight="semibold">Left ({leftSolution}):</Text>
          <Text size={200}>{diff.leftValue || '(null)'}</Text>
        </div>
        <div className={`${styles.diffValue} ${styles.rightValue}`}>
          <Text size={200} weight="semibold">Right ({rightSolution}):</Text>
          <Text size={200}>{diff.rightValue || '(null)'}</Text>
        </div>
      </div>
    );
  };

  // Render Monaco diff (for complex values)
  const renderMonacoDiff = (diff: AttributeDiffType) => {
    const leftText = diff.leftValue || '';
    const rightText = diff.rightValue || '';
    
    // Determine language from attribute type (5=Xml, 4=Json)
    let language = 'plaintext';
    if (diff.attributeType === AttributeTypeEnum.Xml) {
      language = 'xml';
    } else if (diff.attributeType === AttributeTypeEnum.Json) {
      language = 'json';
    } else {
      // Fallback heuristic for content that wasn't typed
      const hasXml = leftText.trim().startsWith('<') || rightText.trim().startsWith('<');
      const hasJson = leftText.trim().startsWith('{') || leftText.trim().startsWith('[') ||
                      rightText.trim().startsWith('{') || rightText.trim().startsWith('[');
      if (hasXml) language = 'xml';
      else if (hasJson) language = 'json';
    }
    
    return (
      <div className={styles.editorContainer}>
        <DiffEditor
          original={leftText}
          modified={rightText}
          language={language}
          theme="vs-dark"
          options={{
            readOnly: true,
            minimap: { enabled: false },
            renderSideBySide: true,
            fontSize: 12,
          }}
        />
      </div>
    );
  };

  return (
    <Card>
      <CardHeader 
        header={<Text weight="semibold">Diff Component Attributes</Text>} 
        description="Compare component attributes between solutions" 
      />
      <div className={styles.section}>
        {onNavigateBack && (
          <Button
            className={styles.backButton}
            appearance="subtle"
            icon={<ArrowLeftRegular />}
            onClick={onNavigateBack}
          >
            Back to Analysis
          </Button>
        )}

        <Field 
          label={
            <InfoLabel info="Enter the component GUID to compare. You can select a component from the Analysis tab or paste a GUID directly.">
              Component ID
            </InfoLabel>
          }
          required
        >
          <Input 
            value={componentId} 
            onChange={(_, d) => setComponentId(d.value)}
            placeholder="Select from analysis or enter GUID" 
          />
        </Field>

        <div className={styles.inputRow}>
          <Field 
            label={
              <InfoLabel info="The base or source solution to compare from. This is typically your core or earlier solution.">
                Left Solution
              </InfoLabel>
            }
            style={{ flex: 1 }}
            required
          >
            <Input 
              value={leftSolution} 
              onChange={(_, d) => setLeftSolution(d.value)}
              placeholder="CoreSolution" 
            />
          </Field>
          <Field 
            label={
              <InfoLabel info="The target solution to compare against. This shows changes made by this solution on top of the left solution.">
                Right Solution
              </InfoLabel>
            }
            style={{ flex: 1 }}
            required
          >
            <Input 
              value={rightSolution} 
              onChange={(_, d) => setRightSolution(d.value)}
              placeholder="ProjectA" 
            />
          </Field>
        </div>

        <Button 
          appearance="primary" 
          icon={<DocumentTextRegular />}
          onClick={handleDiff}
          disabled={loading.diffing || !componentId || !leftSolution || !rightSolution}
        >
          {loading.diffing ? 'Loading Diff...' : 'Show Diff'}
        </Button>

        {loading.diffing && <Spinner label="Retrieving payloads..." />}

        {warnings.length > 0 && (
          <Card style={{ padding: tokens.spacingVerticalS, backgroundColor: tokens.colorPaletteYellowBackground2 }}>
            <Text weight="semibold">Warnings:</Text>
            {warnings.map((w, i) => (
              <Text key={i} size={200}>{w}</Text>
            ))}
          </Card>
        )}

        {attributeDiffs.length > 0 && (
          <>
            <Divider />
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, alignItems: 'center' }}>
                <Badge appearance="filled" color="success">{leftSolution}</Badge>
                <Text>vs</Text>
                <Badge appearance="filled" color="danger">{rightSolution}</Badge>
                <Badge appearance="outline">{filteredDiffs.length} differences</Badge>
              </div>
              <Field label="Search attributes" style={{ maxWidth: '300px' }}>
                <Input 
                  value={searchTerm}
                  onChange={(_, d) => setSearchTerm(d.value)}
                  placeholder="Search by key or value..."
                  contentBefore={<SearchRegular />}
                />
              </Field>
            </div>

            {filteredDiffs.length === 0 ? (
              <Card style={{ padding: tokens.spacingVerticalL, textAlign: 'center' }}>
                <Text>
                  {searchTerm ? 'No attributes match your search' : 'No differences found'}
                </Text>
              </Card>
            ) : (
              <div className={styles.attributesList}>
                {filteredDiffs.map((diff) => (
                  <div key={diff.attributeName} className={styles.attributeItem}>
                    <div className={styles.attributeHeader}>
                      <Text weight="semibold" size={300}>{diff.attributeName}</Text>
                      <div style={{ display: 'flex', gap: tokens.spacingHorizontalXS }}>
                        {diff.onlyInLeft && (
                          <Badge appearance="filled" color="danger">Only in {leftSolution}</Badge>
                        )}
                        {diff.onlyInRight && (
                          <Badge appearance="filled" color="success">Only in {rightSolution}</Badge>
                        )}
                        {diff.isDifferent && !diff.onlyInLeft && !diff.onlyInRight && (
                          <Badge appearance="filled" color="warning">Modified</Badge>
                        )}
                        {diff.isComplex && (
                          <Badge appearance="outline">Complex</Badge>
                        )}
                      </div>
                    </div>
                    
                    {diff.isComplex ? renderMonacoDiff(diff) : renderSimpleDiff(diff)}
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </Card>
  );
};
