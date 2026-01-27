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
  Divider,
} from '@fluentui/react-components';
import { PlayRegular, ArrowSyncRegular } from '@fluentui/react-icons';
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
  const { indexSolutions, clearIndex, loading } = usePluginApi();
  
  const [sourceSolutions, setSourceSolutions] = useState('CoreSolution');
  const [targetSolutions, setTargetSolutions] = useState('ProjectA,ProjectB');
  const [componentTypes, setComponentTypes] = useState('SystemForm,SavedQuery,Entity,Attribute');
  const [indexStats, setIndexStats] = useState<IndexStats | null>(null);

  const handleIndex = async () => {
    const sourceList = sourceSolutions.split(',').map(s => s.trim()).filter(Boolean);
    const targetList = targetSolutions.split(',').map(s => s.trim()).filter(Boolean);
    const typeList = componentTypes.split(',').map(s => s.trim()).filter(Boolean);
    
    const stats = await indexSolutions(sourceList, targetList, typeList);
    setIndexStats(stats);
    onIndexComplete(stats);
  };

  const handleClear = async () => {
    await clearIndex();
    setIndexStats(null);
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
          />
        </Field>
        <Field label="Target Solutions (comma-separated)">
          <Input 
            value={targetSolutions} 
            onChange={(_, d) => setTargetSolutions(d.value)}
            placeholder="ProjectA,ProjectB" 
          />
        </Field>
        <Field label="Component Types (comma-separated)">
          <Input 
            value={componentTypes} 
            onChange={(_, d) => setComponentTypes(d.value)}
            placeholder="SystemForm,SavedQuery,Entity,Attribute" 
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
        {loading.indexing && <Spinner label="Indexing solutions and components..." />}
        {indexStats && (
          <Card className={styles.statsCard}>
            <Text weight="semibold">Index Complete!</Text>
            <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
            <Text>Solutions: {indexStats.stats?.solutions || 0}</Text>
            <Text>Components: {indexStats.stats?.components || 0}</Text>
            <Text>Layers: {indexStats.stats?.layers || 0}</Text>
            {indexStats.warnings && indexStats.warnings.length > 0 && (
              <>
                <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
                <Text weight="semibold" style={{ color: tokens.colorPaletteYellowForeground1 }}>
                  Warnings:
                </Text>
                {indexStats.warnings.map((w, i) => (
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
