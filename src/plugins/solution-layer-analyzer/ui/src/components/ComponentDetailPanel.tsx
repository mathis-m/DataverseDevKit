import React, { useEffect, useState } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  Text,
  Badge,
  Button,
  Divider,
  Spinner,
} from '@fluentui/react-components';
import {
  DismissRegular,
  BranchCompareRegular,
  EyeRegular,
} from '@fluentui/react-icons';
import { ComponentResult, Layer } from '../types';
import { usePluginApi } from '../hooks/usePluginApi';
import { LayerViewerDialog } from './LayerViewerDialog';

const useStyles = makeStyles({
  panel: {
    position: 'fixed',
    right: 0,
    top: 0,
    bottom: 0,
    width: '400px',
    backgroundColor: tokens.colorNeutralBackground1,
    borderLeft: `1px solid ${tokens.colorNeutralStroke1}`,
    boxShadow: tokens.shadow28,
    display: 'flex',
    flexDirection: 'column',
    zIndex: 1000,
  },
  header: {
    padding: tokens.spacingVerticalL,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  content: {
    flex: 1,
    overflowY: 'auto',
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  layerCard: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground2Hover,
    },
  },
  layerHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXS,
  },
  propertyRow: {
    display: 'flex',
    justifyContent: 'space-between',
    paddingVertical: tokens.spacingVerticalXXS,
  },
});

interface ComponentDetailPanelProps {
  component: ComponentResult;
  onClose: () => void;
  onDiff: (leftSolution: string, rightSolution: string) => void;
}

export const ComponentDetailPanel: React.FC<ComponentDetailPanelProps> = ({
  component,
  onClose,
  onDiff,
}) => {
  const styles = useStyles();
  const { getComponentDetails } = usePluginApi();
  const [layers, setLayers] = useState<Layer[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedLayers, setSelectedLayers] = useState<string[]>([]);
  const [viewLayerDialogOpen, setViewLayerDialogOpen] = useState(false);
  const [layerToView, setLayerToView] = useState<Layer | null>(null);

  useEffect(() => {
    const loadDetails = async () => {
      setLoading(true);
      try {
        const details = await getComponentDetails(component.componentId);
        if (details.layers) {
          setLayers(details.layers);
        }
      } catch (error) {
        console.error('Failed to load component details:', error);
      } finally {
        setLoading(false);
      }
    };
    loadDetails();
  }, [component.componentId, getComponentDetails]);

  const handleLayerClick = (solutionName: string) => {
    if (selectedLayers.includes(solutionName)) {
      setSelectedLayers(selectedLayers.filter(s => s !== solutionName));
    } else if (selectedLayers.length < 2) {
      setSelectedLayers([...selectedLayers, solutionName]);
    } else {
      // Replace the oldest selection
      setSelectedLayers([selectedLayers[1], solutionName]);
    }
  };

  const handleDiff = () => {
    if (selectedLayers.length === 2) {
      onDiff(selectedLayers[0], selectedLayers[1]);
    }
  };

  const handleViewLayer = () => {
    if (selectedLayers.length === 1) {
      const layer = layers.find(l => l.solutionName === selectedLayers[0]);
      if (layer && layer.componentJson) {
        setLayerToView(layer);
        setViewLayerDialogOpen(true);
      }
    }
  };

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <div>
          <Text size={500} weight="semibold">{component.displayName || component.logicalName}</Text>
          <br />
          <Badge appearance="outline" size="small">{component.componentType}</Badge>
        </div>
        <Button 
          appearance="subtle" 
          icon={<DismissRegular />} 
          onClick={onClose}
        />
      </div>

      <div className={styles.content}>
        <div className={styles.section}>
          <Text weight="semibold">Component Details</Text>
          <Card style={{ padding: tokens.spacingVerticalS }}>
            <div className={styles.propertyRow}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>ID:</Text>
              <Text size={200}>{component.componentId}</Text>
            </div>
            <div className={styles.propertyRow}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Logical Name:</Text>
              <Text size={200}>{component.logicalName}</Text>
            </div>
            {component.tableLogicalName && (
              <div className={styles.propertyRow}>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Table:</Text>
                <Text size={200}>{component.tableLogicalName}</Text>
              </div>
            )}
            <div className={styles.propertyRow}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Managed:</Text>
              <Badge appearance={component.isManaged ? 'filled' : 'outline'} size="small">
                {component.isManaged ? 'Yes' : 'No'}
              </Badge>
            </div>
            {component.publisher && (
              <div className={styles.propertyRow}>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>Publisher:</Text>
                <Text size={200}>{component.publisher}</Text>
              </div>
            )}
          </Card>
        </div>

        <Divider />

        <div className={styles.section}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Text weight="semibold">
              Layer Stack <Badge appearance="outline">{layers.length || component.layerSequence.length}</Badge>
            </Text>
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
              {selectedLayers.length === 1 && layers.some(l => l.solutionName === selectedLayers[0] && l.componentJson) && (
                <Button
                  size="small"
                  appearance="secondary"
                  icon={<EyeRegular />}
                  onClick={handleViewLayer}
                >
                  View
                </Button>
              )}
              {selectedLayers.length === 2 && (
                <Button
                  size="small"
                  appearance="primary"
                  icon={<BranchCompareRegular />}
                  onClick={handleDiff}
                >
                  Compare
                </Button>
              )}
            </div>
          </div>
          {loading && <Spinner size="small" label="Loading layers..." />}
          {!loading && layers.length === 0 && component.layerSequence.length > 0 && (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              Layer details not available. Showing sequence from query.
            </Text>
          )}
          
          {/* Show actual layers if available, otherwise show sequence */}
          {layers.length > 0 ? (
            layers
              .sort((a, b) => a.ordinal - b.ordinal)
              .map((layer, index) => (
                <Card
                  key={index}
                  className={styles.layerCard}
                  onClick={() => handleLayerClick(layer.solutionName)}
                  style={{
                    border: selectedLayers.includes(layer.solutionName)
                      ? `2px solid ${tokens.colorBrandBackground}`
                      : `1px solid ${tokens.colorNeutralStroke1}`,
                  }}
                >
                  <div className={styles.layerHeader}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                      <Badge appearance="filled" color={index === 0 ? 'success' : index === layers.length - 1 ? 'danger' : 'warning'}>
                        {index + 1}
                      </Badge>
                      <Text weight="semibold">{layer.solutionName}</Text>
                    </div>
                    <Badge appearance={layer.managed ? 'filled' : 'outline'} size="small">
                      {layer.managed ? 'Managed' : 'Unmanaged'}
                    </Badge>
                  </div>
                  {layer.publisher && (
                    <Text size={200}>Publisher: {layer.publisher}</Text>
                  )}
                  {layer.version && (
                    <Text size={200}>Version: {layer.version}</Text>
                  )}
                  {layer.createdOn && (
                    <Text size={200}>Created: {new Date(layer.createdOn).toLocaleDateString()}</Text>
                  )}
                  <Text size={100} style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalXXS }}>
                    Ordinal: {layer.ordinal}
                  </Text>
                </Card>
              ))
          ) : (
            component.layerSequence.map((solution, index) => (
              <Card
                key={index}
                className={styles.layerCard}
                onClick={() => handleLayerClick(solution)}
                style={{
                  border: selectedLayers.includes(solution)
                    ? `2px solid ${tokens.colorBrandBackground}`
                    : `1px solid ${tokens.colorNeutralStroke1}`,
                }}
              >
                <div className={styles.layerHeader}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Badge appearance="filled" color={index === 0 ? 'success' : index === component.layerSequence.length - 1 ? 'danger' : 'warning'}>
                      {index + 1}
                    </Badge>
                    <Text weight="semibold">{solution}</Text>
                  </div>
                </div>
              </Card>
            ))
          )}
        </div>

        {selectedLayers.length > 0 && (
          <Card style={{ padding: tokens.spacingVerticalS, backgroundColor: tokens.colorBrandBackground2 }}>
            <Text size={200}>
              Selected for {selectedLayers.length === 1 ? 'viewing' : 'comparison'}: <strong>{selectedLayers.join(' vs ')}</strong>
            </Text>
            <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
              {selectedLayers.length === 1 ? 'Click View to see attributes or select another to compare' : 'Click Compare to view diff'}
            </Text>
          </Card>
        )}
      </div>

      {/* Layer Viewer Dialog */}
      {layerToView && (
        <LayerViewerDialog
          isOpen={viewLayerDialogOpen}
          onClose={() => {
            setViewLayerDialogOpen(false);
            setLayerToView(null);
          }}
          solutionName={layerToView.solutionName}
          componentJson={layerToView.componentJson}
          changedAttributesJson={layerToView.changes}
        />
      )}
    </div>
  );
};
