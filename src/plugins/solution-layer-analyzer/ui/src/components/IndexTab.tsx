import React, { useState, useEffect } from 'react';
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
  Divider,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import { PlayRegular, ArrowSyncRegular, CheckmarkCircleRegular, DismissCircleRegular } from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { IndexStats } from '../types';

const useStyles = makeStyles({
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  buttonRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
  statsCard: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
  },
});

interface IndexTabProps {
  onIndexComplete: (stats: IndexStats) => void;
}

export const IndexTab: React.FC<IndexTabProps> = ({ onIndexComplete }) => {
  const styles = useStyles();
  const { indexSolutions, clearIndex, loading, indexCompletion } = usePluginApi();
  
  const [sourceSolutions, setSourceSolutions] = useState('CoreSolution');
  const [targetSolutions, setTargetSolutions] = useState('ProjectA,ProjectB');
  const [componentTypes, setComponentTypes] = useState('SystemForm,SavedQuery,Entity,Attribute');
  const [operationId, setOperationId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Handle index completion events
  useEffect(() => {
    if (indexCompletion) {
      console.log('Index completion received:', indexCompletion);
      
      if (indexCompletion.success && indexCompletion.stats) {
        const stats: IndexStats = {
          stats: indexCompletion.stats,
          warnings: indexCompletion.warnings || [],
        };
        onIndexComplete(stats);
        setError(null);
      } else if (indexCompletion.errorMessage) {
        setError(indexCompletion.errorMessage);
      }
    }
  }, [indexCompletion, onIndexComplete]);

  const handleIndex = async () => {
    setError(null);
    setOperationId(null);
    
    const sourceList = sourceSolutions.split(',').map(s => s.trim()).filter(Boolean);
    const targetList = targetSolutions.split(',').map(s => s.trim()).filter(Boolean);
    const typeList = componentTypes.split(',').map(s => s.trim()).filter(Boolean);
    
    try {
      const response = await indexSolutions(sourceList, targetList, typeList);
      
      if (response.started) {
        setOperationId(response.operationId);
      } else {
        setError(response.errorMessage || 'Failed to start indexing operation');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start indexing');
    }
  };

  const handleClear = async () => {
    await clearIndex();
    setOperationId(null);
    setError(null);
  };

  return (
    <Card>
      <CardHeader 
        header={<Text weight="semibold">Build Index</Text>} 
        description="Index solutions and components from Dataverse" 
      />
      <div className={styles.section}>
        <Field label="Source Solutions (comma-separated)">
          <Input 
            value={sourceSolutions} 
            onChange={(_, d) => setSourceSolutions(d.value)}
            placeholder="CoreSolution"
            disabled={loading.indexing}
          />
        </Field>
        <Field label="Target Solutions (comma-separated)">
          <Input 
            value={targetSolutions} 
            onChange={(_, d) => setTargetSolutions(d.value)}
            placeholder="ProjectA,ProjectB"
            disabled={loading.indexing}
          />
        </Field>
        <Field label="Component Types (comma-separated)">
          <Input 
            value={componentTypes} 
            onChange={(_, d) => setComponentTypes(d.value)}
            placeholder="SystemForm,SavedQuery,Entity,Attribute"
            disabled={loading.indexing}
          />
        </Field>
        <div className={styles.buttonRow}>
          <Button 
            appearance="primary" 
            icon={<PlayRegular />}
            onClick={handleIndex}
            disabled={loading.indexing}
          >
            {loading.indexing ? 'Indexing...' : 'Start Indexing'}
          </Button>
          <Button 
            icon={<ArrowSyncRegular />}
            onClick={handleClear}
            disabled={loading.indexing}
          >
            Clear Index
          </Button>
        </div>
        
        {loading.indexing && operationId && (
          <MessageBar>
            <MessageBarBody>
              <Spinner size="tiny" style={{ marginRight: tokens.spacingHorizontalS }} />
              Indexing in progress... (Operation ID: {operationId.substring(0, 8)}...)
            </MessageBarBody>
          </MessageBar>
        )}
        
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>
              <DismissCircleRegular style={{ marginRight: tokens.spacingHorizontalS }} />
              {error}
            </MessageBarBody>
          </MessageBar>
        )}
        
        {indexCompletion?.success && indexCompletion.stats && (
          <MessageBar intent="success">
            <MessageBarBody>
              <CheckmarkCircleRegular style={{ marginRight: tokens.spacingHorizontalS }} />
              Indexing completed successfully!
            </MessageBarBody>
          </MessageBar>
        )}
        
        {indexCompletion?.stats && (
          <Card className={styles.statsCard}>
            <Text weight="semibold">Index Complete!</Text>
            <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
            <Text>Solutions: {indexCompletion.stats.solutions || 0}</Text>
            <Text>Components: {indexCompletion.stats.components || 0}</Text>
            <Text>Layers: {indexCompletion.stats.layers || 0}</Text>
            {indexCompletion.warnings && indexCompletion.warnings.length > 0 && (
              <>
                <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
                <Text weight="semibold" style={{ color: tokens.colorPaletteYellowForeground1 }}>
                  Warnings:
                </Text>
                {indexCompletion.warnings.map((w, i) => (
                  <Text key={i} size={200}>{w}</Text>
                ))}
              </>
            )}
          </Card>
        )}
      </div>
    </Card>
  );
};
