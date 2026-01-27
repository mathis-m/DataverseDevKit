import React, { useState } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Input,
  Field,
  Spinner,
  Badge,
} from '@fluentui/react-components';
import {
  DocumentTextRegular,
  ArrowLeftRegular,
} from '@fluentui/react-icons';
import { DiffEditor } from '@monaco-editor/react';
import { usePluginApi } from '../hooks/usePluginApi';

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
  diffContainer: {
    height: '70vh',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  backButton: {
    marginBottom: tokens.spacingVerticalM,
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
  
  const [componentId, setComponentId] = useState(initialComponentId || '');
  const [leftSolution, setLeftSolution] = useState(initialLeftSolution || '');
  const [rightSolution, setRightSolution] = useState(initialRightSolution || '');
  const [leftText, setLeftText] = useState('');
  const [rightText, setRightText] = useState('');
  const [mime, setMime] = useState('xml');
  const [warnings, setWarnings] = useState<string[]>([]);

  const handleDiff = async () => {
    if (!componentId || !leftSolution || !rightSolution) return;

    try {
      const result = await diffComponentLayers(componentId, leftSolution, rightSolution);
      setLeftText(result.leftText || '');
      setRightText(result.rightText || '');
      setMime(result.mime?.includes('json') ? 'json' : 'xml');
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

  const language = mime.includes('json') ? 'json' : 'xml';

  return (
    <Card>
      <CardHeader 
        header={<Text weight="semibold">Diff Component Layers</Text>} 
        description="Compare component payloads between solutions" 
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

        <Field label="Component ID">
          <Input 
            value={componentId} 
            onChange={(_, d) => setComponentId(d.value)}
            placeholder="Select from analysis or enter GUID" 
          />
        </Field>

        <div className={styles.inputRow}>
          <Field label="Left Solution" style={{ flex: 1 }}>
            <Input 
              value={leftSolution} 
              onChange={(_, d) => setLeftSolution(d.value)}
              placeholder="CoreSolution" 
            />
          </Field>
          <Field label="Right Solution" style={{ flex: 1 }}>
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

        {leftText && rightText && (
          <>
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, marginBottom: tokens.spacingVerticalXS }}>
              <Badge appearance="filled" color="success">{leftSolution}</Badge>
              <Text>vs</Text>
              <Badge appearance="filled" color="danger">{rightSolution}</Badge>
              <Badge appearance="outline">{language.toUpperCase()}</Badge>
            </div>
            <div className={styles.diffContainer}>
              <DiffEditor
                original={leftText}
                modified={rightText}
                language={language}
                theme="vs-dark"
                options={{
                  readOnly: true,
                  minimap: { enabled: false },
                  renderSideBySide: true,
                }}
              />
            </div>
          </>
        )}
      </div>
    </Card>
  );
};
