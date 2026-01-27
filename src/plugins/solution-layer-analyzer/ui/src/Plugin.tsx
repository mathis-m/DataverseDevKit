import React, { useState } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Badge,
  Tab,
  TabList,
} from '@fluentui/react-components';
import {
  DatabaseRegular,
  ChartMultipleRegular,
  CodeRegular,
} from '@fluentui/react-icons';
import { IndexTab } from './components/IndexTab';
import { AnalysisTab } from './components/AnalysisTab';
import { DiffTab } from './components/DiffTab';
import { IndexStats } from './types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    height: '100%',
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  title: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface PluginProps {
  instanceId: string;
}

const Plugin: React.FC<PluginProps> = ({ instanceId }) => {
  const styles = useStyles();
  
  const [selectedTab, setSelectedTab] = useState('index');
  const [indexStats, setIndexStats] = useState<IndexStats | null>(null);
  
  // Diff navigation state
  const [diffComponentId, setDiffComponentId] = useState<string | undefined>();
  const [diffLeftSolution, setDiffLeftSolution] = useState<string | undefined>();
  const [diffRightSolution, setDiffRightSolution] = useState<string | undefined>();

  const handleIndexComplete = (stats: IndexStats) => {
    setIndexStats(stats);
    // Auto-navigate to analysis after successful index
    setSelectedTab('analysis');
  };

  const handleNavigateToDiff = (componentId: string, leftSolution: string, rightSolution: string) => {
    setDiffComponentId(componentId);
    setDiffLeftSolution(leftSolution);
    setDiffRightSolution(rightSolution);
    setSelectedTab('diff');
  };

  const handleNavigateBack = () => {
    setSelectedTab('analysis');
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text className={styles.title}>📦 Solution Layer Analyzer</Text>
        <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
          Analyze and compare solution component layers with powerful visualizations
        </Text>
        <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
          <Badge appearance="outline" color="informative">Instance: {instanceId}</Badge>
          {indexStats && (
            <Badge appearance="filled" color="success">
              {indexStats.stats?.components || 0} components indexed
            </Badge>
          )}
        </div>
      </div>

      <TabList selectedValue={selectedTab} onTabSelect={(_, d) => setSelectedTab(d.value as string)}>
        <Tab icon={<DatabaseRegular />} value="index">Index</Tab>
        <Tab icon={<ChartMultipleRegular />} value="analysis">Analysis</Tab>
        <Tab icon={<CodeRegular />} value="diff">Diff</Tab>
      </TabList>

      {selectedTab === 'index' && (
        <IndexTab onIndexComplete={handleIndexComplete} />
      )}

      {selectedTab === 'analysis' && (
        <AnalysisTab onNavigateToDiff={handleNavigateToDiff} />
      )}

      {selectedTab === 'diff' && (
        <DiffTab
          initialComponentId={diffComponentId}
          initialLeftSolution={diffLeftSolution}
          initialRightSolution={diffRightSolution}
          onNavigateBack={selectedTab === 'diff' && diffComponentId ? handleNavigateBack : undefined}
        />
      )}
    </div>
  );
};

export default Plugin;
