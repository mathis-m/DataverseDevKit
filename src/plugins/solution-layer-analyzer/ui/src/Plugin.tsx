import React, { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Badge,
  Tab,
  TabList,
  shorthands,
  mergeClasses
} from '@fluentui/react-components';
import {
  DatabaseRegular,
  ChartMultipleRegular,
  CodeRegular,
  ChartPerson24Regular,
} from '@fluentui/react-icons';
import { ImprovedIndexTab } from './components/ImprovedIndexTab';
import { AnalysisTab } from './components/AnalysisTab';
import { DiffTab } from './components/DiffTab';
import { AnalysisDashboard } from './components/AnalysisDashboard';
import { Footer } from './components/Footer';
import { ProgressIndicator } from './components/ProgressIndicator';
import { SaveConfigDialog } from './components/SaveConfigDialog';
import { LoadConfigDialog } from './components/LoadConfigDialog';
import { IndexStats } from './types';
import { useAppStore } from './store/useAppStore';
import { usePluginApi } from './hooks/usePluginApi';
import { hostBridge } from '@ddk/host-sdk';

const useStyles = makeStyles({
  wrapper: {
    display: 'grid',
    gridTemplateRows: '1fr auto',
    gridTemplateColumns: '1fr',
    height: '100%',
  },
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    paddingBlock: tokens.spacingVerticalL,
    paddingInline: tokens.spacingHorizontalL,
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  headerContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  configButtons: {
    display: 'flex',
    ...shorthands.gap('8px'),
  },
  title: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
});

const useGlobalStyles = makeStyles({
  root: {
    height: '100%',
    ':global(*)': {
      scrollbarWidth: 'thin',
      scrollbarColor: `${tokens.colorNeutralStroke1} ${tokens.colorNeutralBackground1}`,
    },
    ':global(*::-webkit-scrollbar)': {
      width: '8px',
      height: '8px',
    },
    ':global(*::-webkit-scrollbar-track)': {
      background: tokens.colorNeutralBackground1,
      borderRadius: tokens.borderRadiusMedium,
    },
    ':global(*::-webkit-scrollbar-thumb)': {
      background: tokens.colorNeutralStroke1,
      borderRadius: tokens.borderRadiusMedium,
      border: `2px solid ${tokens.colorNeutralBackground1}`,
    },
    ':global(*::-webkit-scrollbar-thumb:hover)': {
      background: tokens.colorNeutralStroke1Hover,
    },
  },
});

interface PluginProps {
  instanceId: string;
}

const Plugin: React.FC<PluginProps> = ({ instanceId }) => {
  const styles = useStyles();
  const globalStyles = useGlobalStyles();

  
  const [indexStats, setIndexStats] = useState<IndexStats | null>(null);
  
  // Get state from store
  const { 
    diffState, setDiffState, indexConfig, 
    addOperation, updateOperation, removeOperation,
    setAvailableSolutions, setAvailableComponentTypes, setMetadataLoaded, metadataLoaded,
    selectedTab, setSelectedTab
  } = useAppStore();
  
  const { fetchSolutions, getComponentTypes } = usePluginApi();

  // Load global metadata (solutions and component types) once on mount
  useEffect(() => {
    if (metadataLoaded) return;
    
    const loadMetadata = async () => {
      try {
        const [solutionsData, typesData] = await Promise.all([
          fetchSolutions('default'),
          getComponentTypes()
        ]);
        setAvailableSolutions(solutionsData.solutions || []);
        setAvailableComponentTypes(typesData.componentTypes || []);
        setMetadataLoaded(true);
      } catch (error) {
        console.error('Failed to load metadata:', error);
      }
    };
    
    loadMetadata();
  }, [metadataLoaded, fetchSolutions, getComponentTypes, setAvailableSolutions, setAvailableComponentTypes, setMetadataLoaded]);

  // Listen for progress events
  useEffect(() => {
    const unsubscribe = hostBridge.addEventListener('plugin:sla:progress', (event) => {
      try {
        const payload = typeof event.payload === 'string' ? JSON.parse(event.payload) : event.payload;
        
        // Update or create operation
        const operationId = 'index-operation'; // Could be dynamic based on payload
        if (payload.phase === 'complete') {
          removeOperation(operationId);
        } else {
          const existing = useAppStore.getState().operations.find(op => op.id === operationId);
          if (existing) {
            updateOperation(operationId, {
              percent: payload.percent || 0,
              message: payload.message || 'Processing...',
              phase: payload.phase,
            });
          } else {
            addOperation({
              id: operationId,
              type: 'index',
              message: payload.message || 'Indexing...',
              percent: payload.percent || 0,
              phase: payload.phase,
            });
          }
        }
      } catch (error) {
        console.error('Failed to parse progress event:', error);
      }
    });

    return () => unsubscribe();
  }, [addOperation, updateOperation, removeOperation]);

  const handleIndexComplete = (stats: IndexStats) => {
    setIndexStats(stats);
    // Auto-navigate to analysis after successful index
    setSelectedTab('analysis');
  };

  const handleNavigateToDiff = (componentId: string, leftSolution: string, rightSolution: string) => {
    setDiffState({
      componentId,
      leftSolution,
      rightSolution,
    });
    setSelectedTab('diff');
  };

  const handleNavigateBack = () => {
    setSelectedTab('analysis');
  };

  const handleLoadIndexConfig = (config: any) => {
    // This would typically trigger re-indexing or update the store
    console.log('Loading index config:', config);
  };

  const handleLoadFilterConfig = (config: any) => {
    // This would update the filter in the store
    console.log('Loading filter config:', config);
  };

  return (
    <div className={mergeClasses(globalStyles.root, styles.wrapper)}>
      <div className={styles.container}>
        <div className={styles.header}>
          <div className={styles.headerContent}>
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
          <div className={styles.configButtons}>
            <SaveConfigDialog 
              currentConnectionId={indexConfig?.connectionId}
              currentIndexHash={indexConfig ? generateIndexHash(indexConfig) : undefined}
            />
            <LoadConfigDialog
              currentConnectionId={indexConfig?.connectionId}
              currentIndexHash={indexConfig ? generateIndexHash(indexConfig) : undefined}
              onLoadIndex={handleLoadIndexConfig}
              onLoadFilter={handleLoadFilterConfig}
            />
          </div>
        </div>

        <TabList selectedValue={selectedTab} onTabSelect={(_, d) => setSelectedTab(d.value as string)}>
          <Tab icon={<DatabaseRegular />} value="index">Index</Tab>
          <Tab icon={<ChartMultipleRegular />} value="analysis">Analysis</Tab>
          <Tab icon={<ChartPerson24Regular />} value="advanced">Advanced Analytics</Tab>
          <Tab icon={<CodeRegular />} value="diff">Diff</Tab>
        </TabList>

        {selectedTab === 'index' && (
          <ImprovedIndexTab onIndexComplete={handleIndexComplete} />
        )}

        {selectedTab === 'analysis' && (
          <AnalysisTab onNavigateToDiff={handleNavigateToDiff} />
        )}

        {selectedTab === 'advanced' && (
          <AnalysisDashboard connectionId="default" />
        )}

        {selectedTab === 'diff' && (
          <DiffTab
            initialComponentId={diffState?.componentId}
            initialLeftSolution={diffState?.leftSolution}
            initialRightSolution={diffState?.rightSolution}
            onNavigateBack={diffState?.componentId ? handleNavigateBack : undefined}
          />
        )}
      </div>
      <Footer rightContent={<ProgressIndicator />} />
    </div>
  );
};

// Helper function to generate index hash (matching backend logic)
function generateIndexHash(config: {
  connectionId?: string;
  sourceSolutions?: string[];
  targetSolutions?: string[];
  componentTypes?: string[];
}): string {
  const sortedSource = (config.sourceSolutions || []).sort().join('|');
  const sortedTarget = (config.targetSolutions || []).sort().join('|');
  const sortedTypes = (config.componentTypes || []).sort().join('|');
  const hashInput = `${config.connectionId || ''}:${sortedSource}:${sortedTarget}:${sortedTypes}`;
  
  // Simple hash for frontend (backend uses SHA256, this is just for UI matching)
  let hash = 0;
  for (let i = 0; i < hashInput.length; i++) {
    const char = hashInput.charCodeAt(i);
    hash = ((hash << 5) - hash) + char;
    hash = hash & hash;
  }
  return Math.abs(hash).toString(16).padStart(16, '0').substring(0, 16);
}

export default Plugin;
