import React, { useState, useEffect } from 'react';
import {
  Spinner,
  Text,
  Title2,
  Button,
  MessageBar,
  MessageBarBody,
  Select,
  Card,
} from '@fluentui/react-components';
import { Grid24Regular, List24Regular } from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { AnalyticsData } from '../types/analytics';
import {
  ForceDirectedNetwork,
  HierarchicalTree,
  ChordDiagram,
  UpSetPlot,
  EnhancedRiskHeatmap
} from '../visualizations';
import { ViolationPanel } from './ViolationPanel';
import { VisualizationModal } from './VisualizationModal';

interface AnalysisDashboardProps {
  connectionId?: string;
}

type VisualizationType = 'force' | 'tree' | 'chord' | 'upset' | 'heatmap';

export const AnalysisDashboard: React.FC<AnalysisDashboardProps> = ({
  connectionId = 'default'
}) => {
  const api = usePluginApi();
  const [analytics, setAnalytics] = useState<AnalyticsData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedVisualization, setSelectedVisualization] = useState<VisualizationType>('force');
  const [selectedNodes, setSelectedNodes] = useState<string[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'single'>('single');

  useEffect(() => {
    loadAnalytics();
  }, [connectionId]);

  const loadAnalytics = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.getAnalytics(connectionId);
      setAnalytics(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load analytics');
      console.error('Failed to load analytics:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleNodeClick = (nodeId: string) => {
    setSelectedNodeId(nodeId);
    if (selectedNodes.includes(nodeId)) {
      setSelectedNodes(selectedNodes.filter(id => id !== nodeId));
    } else {
      setSelectedNodes([...selectedNodes, nodeId]);
    }
  };

  const handleVisualizationChange = (viz: VisualizationType) => {
    setSelectedVisualization(viz);
    setSelectedNodes([]);
    setSelectedNodeId(null);
  };

  const renderVisualization = (viz: VisualizationType, size: 'normal' | 'fullscreen' = 'normal') => {
    if (!analytics) return null;

    const width = size === 'fullscreen' ? window.innerWidth * 0.9 : 1000;
    const height = size === 'fullscreen' ? window.innerHeight * 0.8 : 700;

    switch (viz) {
      case 'force':
        return (
          <ForceDirectedNetwork
            data={analytics.networkData}
            width={width}
            height={height}
            onNodeClick={handleNodeClick}
            selectedNodes={selectedNodes}
          />
        );
      case 'tree':
        return (
          <HierarchicalTree
            data={analytics.hierarchyData}
            width={width}
            height={height}
            onNodeClick={handleNodeClick}
            selectedNodeId={selectedNodeId}
          />
        );
      case 'chord':
        return (
          <ChordDiagram
            data={analytics.chordData}
            width={Math.min(width, height)}
            height={Math.min(width, height)}
            onChordClick={(source, target) => {
              console.log('Chord clicked:', source, target);
            }}
          />
        );
      case 'upset':
        return (
          <UpSetPlot
            data={analytics.upSetData}
            width={width}
            height={height}
            onIntersectionClick={(intersection) => {
              console.log('Intersection clicked:', intersection);
            }}
          />
        );
      case 'heatmap':
        return (
          <EnhancedRiskHeatmap
            data={analytics.solutionOverlaps}
            width={width}
            height={height}
            onCellClick={(sol1, sol2) => {
              console.log('Cell clicked:', sol1, sol2);
            }}
          />
        );
    }
  };

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '400px', flexDirection: 'column', gap: '16px' }}>
        <Spinner size="large" label="Loading analytics..." />
        <Text>Computing solution overlaps, risks, and violations...</Text>
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ padding: '20px' }}>
        <MessageBar intent="error">
          <MessageBarBody>
            <strong>Error loading analytics:</strong> {error}
          </MessageBarBody>
        </MessageBar>
        <Button style={{ marginTop: '16px' }} onClick={loadAnalytics}>
          Retry
        </Button>
      </div>
    );
  }

  if (!analytics) {
    return (
      <div style={{ padding: '20px', textAlign: 'center' }}>
        <Text>No analytics data available. Please index solutions first.</Text>
      </div>
    );
  }

  return (
    <div style={{ padding: '16px' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title2>Analytics Dashboard</Title2>
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <Button
            icon={<Grid24Regular />}
            appearance={viewMode === 'grid' ? 'primary' : 'secondary'}
            onClick={() => setViewMode('grid')}
          >
            Grid
          </Button>
          <Button
            icon={<List24Regular />}
            appearance={viewMode === 'single' ? 'primary' : 'secondary'}
            onClick={() => setViewMode('single')}
          >
            Single
          </Button>
          <Button onClick={loadAnalytics}>Refresh</Button>
        </div>
      </div>

      {/* Summary Stats */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '12px', marginBottom: '16px' }}>
        <Card>
          <div style={{ padding: '12px' }}>
            <Text size={300} weight="semibold">Solutions</Text>
            <Text size={600} style={{ display: 'block' }}>{analytics.solutionMetrics.length}</Text>
          </div>
        </Card>
        <Card>
          <div style={{ padding: '12px' }}>
            <Text size={300} weight="semibold">Components at Risk</Text>
            <Text size={600} style={{ display: 'block' }}>{analytics.componentRisks.length}</Text>
          </div>
        </Card>
        <Card>
          <div style={{ padding: '12px' }}>
            <Text size={300} weight="semibold">Violations</Text>
            <Text size={600} style={{ display: 'block', color: analytics.violations.length > 0 ? '#D32F2F' : '#2E7D32' }}>
              {analytics.violations.length}
            </Text>
          </div>
        </Card>
        <Card>
          <div style={{ padding: '12px' }}>
            <Text size={300} weight="semibold">High Risk</Text>
            <Text size={600} style={{ display: 'block', color: '#D32F2F' }}>
              {analytics.componentRisks.filter(c => c.riskScore > 60).length}
            </Text>
          </div>
        </Card>
      </div>

      {viewMode === 'single' ? (
        <>
          {/* Visualization Selector */}
          <div style={{ marginBottom: '16px' }}>
            <Select
              value={selectedVisualization}
              onChange={(e, data) => handleVisualizationChange(data.value as VisualizationType)}
            >
              <option value="force">Force-Directed Network</option>
              <option value="tree">Hierarchical Tree</option>
              <option value="chord">Chord Diagram</option>
              <option value="upset">UpSet Plot</option>
              <option value="heatmap">Risk Heatmap</option>
            </Select>
            <Button style={{ marginLeft: '8px' }} onClick={() => setShowModal(true)}>
              Fullscreen
            </Button>
          </div>

          {/* Main Visualization */}
          <div style={{ marginBottom: '20px' }}>
            {renderVisualization(selectedVisualization)}
          </div>
        </>
      ) : (
        /* Grid View */
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px', marginBottom: '20px' }}>
          <Card>
            <div style={{ padding: '12px' }}>
              <Text weight="semibold">Force Network</Text>
              {renderVisualization('force', 'normal')}
            </div>
          </Card>
          <Card>
            <div style={{ padding: '12px' }}>
              <Text weight="semibold">Hierarchical Tree</Text>
              {renderVisualization('tree', 'normal')}
            </div>
          </Card>
          <Card>
            <div style={{ padding: '12px' }}>
              <Text weight="semibold">Chord Diagram</Text>
              {renderVisualization('chord', 'normal')}
            </div>
          </Card>
          <Card>
            <div style={{ padding: '12px' }}>
              <Text weight="semibold">Risk Heatmap</Text>
              {renderVisualization('heatmap', 'normal')}
            </div>
          </Card>
        </div>
      )}

      {/* Violation Panel */}
      <Card>
        <ViolationPanel violations={analytics.violations} />
      </Card>

      {/* Fullscreen Modal */}
      {showModal && (
        <VisualizationModal onClose={() => setShowModal(false)}>
          {renderVisualization(selectedVisualization, 'fullscreen')}
        </VisualizationModal>
      )}
    </div>
  );
};
