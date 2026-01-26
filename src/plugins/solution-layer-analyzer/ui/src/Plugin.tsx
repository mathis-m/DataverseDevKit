import React, { useState, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Input,
  Field,
  Badge,
  Spinner,
  Divider,
  Tab,
  TabList,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
} from '@fluentui/react-components';
import {
  DatabaseRegular,
  FilterRegular,
  DocumentTextRegular,
  CodeRegular,
  PlayRegular,
} from '@fluentui/react-icons';
import { hostBridge } from '@ddk/host-sdk';
import { DiffEditor } from '@monaco-editor/react';

const PLUGIN_ID = 'com.ddk.solutionlayeranalyzer';

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
  resultsContainer: {
    minHeight: '400px',
  },
  diffContainer: {
    height: '600px',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
  },
});

interface PluginProps {
  instanceId: string;
}

interface ComponentResult {
  componentId: string;
  componentType: string;
  logicalName: string;
  displayName?: string;
  layerSequence: string[];
  isManaged: boolean;
  publisher?: string;
  tableLogicalName?: string;
}

const Plugin: React.FC<PluginProps> = ({ instanceId }) => {
  const styles = useStyles();
  
  // Index state
  const [sourceSolutions, setSourceSolutions] = useState('CoreSolution');
  const [targetSolutions, setTargetSolutions] = useState('ProjectA,ProjectB');
  const [indexing, setIndexing] = useState(false);
  const [indexStats, setIndexStats] = useState<any>(null);

  // Query state
  const [components, setComponents] = useState<ComponentResult[]>([]);
  const [querying, setQuerying] = useState(false);
  const [selectedTab, setSelectedTab] = useState('index');

  // Diff state
  const [selectedComponentId, setSelectedComponentId] = useState<string | null>(null);
  const [leftSolution, setLeftSolution] = useState('');
  const [rightSolution, setRightSolution] = useState('');
  const [leftText, setLeftText] = useState('');
  const [rightText, setRightText] = useState('');
  const [diffing, setDiffing] = useState(false);

  const handleIndex = useCallback(async () => {
    setIndexing(true);
    try {
      const payload = JSON.stringify({
        connectionId: 'default',
        sourceSolutions: sourceSolutions.split(',').map(s => s.trim()),
        targetSolutions: targetSolutions.split(',').map(s => s.trim()),
        includeComponentTypes: ['SystemForm', 'SavedQuery', 'RibbonCustomization'],
        payloadMode: 'lazy',
      });
      
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'index', payload);
      setIndexStats(result);
    } catch (error) {
      console.error('Index error:', error);
    } finally {
      setIndexing(false);
    }
  }, [sourceSolutions, targetSolutions]);

  const handleQuery = useCallback(async () => {
    setQuerying(true);
    try {
      const payload = JSON.stringify({
        filters: null, // No filters for simple query
        paging: { skip: 0, take: 100 },
        sort: [{ field: 'componentType', dir: 'asc' }],
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'query', payload);
      setComponents(result.rows || []);
    } catch (error) {
      console.error('Query error:', error);
    } finally {
      setQuerying(false);
    }
  }, []);

  const handleDiff = useCallback(async () => {
    if (!selectedComponentId || !leftSolution || !rightSolution) return;
    
    setDiffing(true);
    try {
      const payload = JSON.stringify({
        componentId: selectedComponentId,
        connectionId: 'default',
        left: { solutionName: leftSolution, payloadType: 'auto' },
        right: { solutionName: rightSolution, payloadType: 'auto' },
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'diff', payload);
      setLeftText(result.leftText || '');
      setRightText(result.rightText || '');
    } catch (error) {
      console.error('Diff error:', error);
    } finally {
      setDiffing(false);
    }
  }, [selectedComponentId, leftSolution, rightSolution]);

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text className={styles.title}>📦 Solution Layer Analyzer</Text>
        <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
          Analyze and compare solution component layers across your Dataverse environment
        </Text>
        <Badge appearance="outline" color="informative">Instance: {instanceId}</Badge>
      </div>

      <TabList selectedValue={selectedTab} onTabSelect={(_, d) => setSelectedTab(d.value as string)}>
        <Tab icon={<DatabaseRegular />} value="index">Index</Tab>
        <Tab icon={<FilterRegular />} value="query">Query</Tab>
        <Tab icon={<CodeRegular />} value="diff">Diff</Tab>
      </TabList>

      {selectedTab === 'index' && (
        <Card>
          <CardHeader header={<Text weight="semibold">Build Index</Text>} 
            description="Index solutions and components from Dataverse" />
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
            <Button 
              appearance="primary" 
              icon={<PlayRegular />}
              onClick={handleIndex}
              disabled={indexing}
            >
              {indexing ? 'Indexing...' : 'Start Indexing'}
            </Button>
            {indexing && <Spinner label="Indexing solutions and components..." />}
            {indexStats && (
              <Card style={{ padding: tokens.spacingVerticalM, backgroundColor: tokens.colorNeutralBackground2 }}>
                <Text weight="semibold">Index Complete!</Text>
                <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
                <Text>Solutions: {indexStats.stats?.solutions || 0}</Text>
                <Text>Components: {indexStats.stats?.components || 0}</Text>
                <Text>Layers: {indexStats.stats?.layers || 0}</Text>
              </Card>
            )}
          </div>
        </Card>
      )}

      {selectedTab === 'query' && (
        <Card>
          <CardHeader header={<Text weight="semibold">Query Components</Text>} 
            description="View indexed components and their layer sequences" />
          <div className={styles.section}>
            <Button 
              appearance="primary" 
              icon={<FilterRegular />}
              onClick={handleQuery}
              disabled={querying}
            >
              {querying ? 'Querying...' : 'Query All Components'}
            </Button>
            {querying && <Spinner label="Querying components..." />}
            {components.length > 0 && (
              <div className={styles.resultsContainer}>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell>Type</TableHeaderCell>
                      <TableHeaderCell>Logical Name</TableHeaderCell>
                      <TableHeaderCell>Display Name</TableHeaderCell>
                      <TableHeaderCell>Layer Sequence</TableHeaderCell>
                      <TableHeaderCell>Managed</TableHeaderCell>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {components.map((c) => (
                      <TableRow 
                        key={c.componentId}
                        onClick={() => {
                          setSelectedComponentId(c.componentId);
                          if (c.layerSequence.length >= 2) {
                            setLeftSolution(c.layerSequence[0]);
                            setRightSolution(c.layerSequence[1]);
                          }
                          setSelectedTab('diff');
                        }}
                        style={{ cursor: 'pointer' }}
                      >
                        <TableCell>{c.componentType}</TableCell>
                        <TableCell>{c.logicalName}</TableCell>
                        <TableCell>{c.displayName || '-'}</TableCell>
                        <TableCell>
                          {c.layerSequence.map((s, i) => (
                            <Badge key={i} appearance="outline" style={{ marginRight: tokens.spacingHorizontalXS }}>
                              {s}
                            </Badge>
                          ))}
                        </TableCell>
                        <TableCell>{c.isManaged ? 'Yes' : 'No'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </div>
        </Card>
      )}

      {selectedTab === 'diff' && (
        <Card>
          <CardHeader header={<Text weight="semibold">Diff Component Layers</Text>} 
            description="Compare component payloads between solutions" />
          <div className={styles.section}>
            <Field label="Component ID">
              <Input 
                value={selectedComponentId || ''} 
                onChange={(_, d) => setSelectedComponentId(d.value)}
                placeholder="Select from query results or enter GUID" 
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
              disabled={diffing || !selectedComponentId}
            >
              {diffing ? 'Loading Diff...' : 'Show Diff'}
            </Button>
            {diffing && <Spinner label="Retrieving payloads..." />}
            {leftText && rightText && (
              <div className={styles.diffContainer}>
                <DiffEditor
                  original={leftText}
                  modified={rightText}
                  language="xml"
                  theme="vs-dark"
                  options={{
                    readOnly: true,
                    minimap: { enabled: false },
                  }}
                />
              </div>
            )}
          </div>
        </Card>
      )}
    </div>
  );
};

export default Plugin;
