import React, { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Spinner,
  Divider,
  MessageBar,
  MessageBarBody,
  Field,
  Combobox,
  Option,
  Tag,
  TagGroup,
  InteractionTag,
  InteractionTagPrimary,
  InteractionTagSecondary,
} from '@fluentui/react-components';
import { PlayRegular, ArrowSyncRegular, CheckmarkCircleRegular, DismissCircleRegular, DismissRegular } from '@fluentui/react-icons';
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
  tagGroup: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

interface Solution {
  uniqueName: string;
  displayName: string;
  version: string;
  isManaged: boolean;
  publisher?: string;
}

interface ComponentType {
  name: string;
  displayName: string;
  typeCode: number;
}

interface IndexTabProps {
  onIndexComplete: (stats: IndexStats) => void;
}

export const ImprovedIndexTab: React.FC<IndexTabProps> = ({ onIndexComplete }) => {
  const styles = useStyles();
  const { indexSolutions, clearIndex, loading, indexCompletion } = usePluginApi();
  
  const [allSolutions, setAllSolutions] = useState<Solution[]>([]);
  const [allComponentTypes, setAllComponentTypes] = useState<ComponentType[]>([]);
  const [loadingSolutions, setLoadingSolutions] = useState(false);
  const [loadingComponentTypes, setLoadingComponentTypes] = useState(false);
  
  const [selectedSourceSolutions, setSelectedSourceSolutions] = useState<string[]>([]);
  const [selectedTargetSolutions, setSelectedTargetSolutions] = useState<string[]>([]);
  const [selectedComponentTypes, setSelectedComponentTypes] = useState<string[]>([]);
  
  const [operationId, setOperationId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Load solutions and component types on mount
  useEffect(() => {
    loadSolutions();
    loadComponentTypes();
  }, []);

  // Set all component types as selected by default once loaded
  useEffect(() => {
    if (allComponentTypes.length > 0 && selectedComponentTypes.length === 0) {
      setSelectedComponentTypes(allComponentTypes.map(ct => ct.name));
    }
  }, [allComponentTypes]);

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

  const loadSolutions = async () => {
    setLoadingSolutions(true);
    try {
      const response = await fetch('/api/plugins/execute', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          pluginId: 'com.ddk.solutionlayeranalyzer',
          command: 'fetchSolutions',
          payload: JSON.stringify({ connectionId: 'default' }),
        }),
      });
      
      if (response.ok) {
        const data = await response.json();
        setAllSolutions(data.solutions || []);
      }
    } catch (err) {
      console.error('Failed to load solutions:', err);
    } finally {
      setLoadingSolutions(false);
    }
  };

  const loadComponentTypes = async () => {
    setLoadingComponentTypes(true);
    try {
      const response = await fetch('/api/plugins/execute', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          pluginId: 'com.ddk.solutionlayeranalyzer',
          command: 'getComponentTypes',
          payload: JSON.stringify({}),
        }),
      });
      
      if (response.ok) {
        const data = await response.json();
        setAllComponentTypes(data.componentTypes || []);
      }
    } catch (err) {
      console.error('Failed to load component types:', err);
    } finally {
      setLoadingComponentTypes(false);
    }
  };

  const handleIndex = async () => {
    setError(null);
    setOperationId(null);
    
    if (selectedSourceSolutions.length === 0 && selectedTargetSolutions.length === 0) {
      setError('Please select at least one source or target solution');
      return;
    }
    
    if (selectedComponentTypes.length === 0) {
      setError('Please select at least one component type');
      return;
    }
    
    try {
      const response = await indexSolutions(
        selectedSourceSolutions,
        selectedTargetSolutions,
        selectedComponentTypes
      );
      
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

  const getSolutionDisplay = (uniqueName: string) => {
    const solution = allSolutions.find(s => s.uniqueName === uniqueName);
    return solution ? `${solution.displayName} (${uniqueName})` : uniqueName;
  };

  const getComponentTypeDisplay = (name: string) => {
    const type = allComponentTypes.find(ct => ct.name === name);
    return type ? type.displayName : name;
  };

  return (
    <Card>
      <CardHeader 
        header={<Text weight="semibold">Build Index</Text>} 
        description="Index solutions and components from Dataverse" 
      />

      <div className={styles.section}>
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>{error}</MessageBarBody>
          </MessageBar>
        )}

        <Field label="Source Solutions" hint="Solutions where components originate">
          {loadingSolutions ? (
            <Spinner size="tiny" />
          ) : (
            <>
              <Combobox
                placeholder="Select source solutions..."
                multiselect
                selectedOptions={selectedSourceSolutions}
                onOptionSelect={(_, data) => {
                  setSelectedSourceSolutions(data.selectedOptions);
                }}
              >
                {allSolutions.map((solution) => (
                  <Option key={solution.uniqueName} value={solution.uniqueName} text={solution.displayName}>
                    {solution.displayName} ({solution.uniqueName})
                  </Option>
                ))}
              </Combobox>
              {selectedSourceSolutions.length > 0 && (
                <TagGroup className={styles.tagGroup}>
                  {selectedSourceSolutions.map((uniqueName) => (
                    <InteractionTag
                      key={uniqueName}
                      value={uniqueName}
                      appearance="filled"
                      color="brand"
                    >
                      <InteractionTagPrimary>
                        {getSolutionDisplay(uniqueName)}
                      </InteractionTagPrimary>
                      <InteractionTagSecondary
                        onClick={() => {
                          setSelectedSourceSolutions(prev => prev.filter(s => s !== uniqueName));
                        }}
                      >
                        <DismissRegular />
                      </InteractionTagSecondary>
                    </InteractionTag>
                  ))}
                </TagGroup>
              )}
            </>
          )}
        </Field>

        <Field label="Target Solutions" hint="Solutions to analyze for layering">
          {loadingSolutions ? (
            <Spinner size="tiny" />
          ) : (
            <>
              <Combobox
                placeholder="Select target solutions..."
                multiselect
                selectedOptions={selectedTargetSolutions}
                onOptionSelect={(_, data) => {
                  setSelectedTargetSolutions(data.selectedOptions);
                }}
              >
                {allSolutions.map((solution) => (
                  <Option key={solution.uniqueName} value={solution.uniqueName} text={solution.displayName}>
                    {solution.displayName} ({solution.uniqueName})
                  </Option>
                ))}
              </Combobox>
              {selectedTargetSolutions.length > 0 && (
                <TagGroup className={styles.tagGroup}>
                  {selectedTargetSolutions.map((uniqueName) => (
                    <InteractionTag
                      key={uniqueName}
                      value={uniqueName}
                      appearance="filled"
                      color="informative"
                    >
                      <InteractionTagPrimary>
                        {getSolutionDisplay(uniqueName)}
                      </InteractionTagPrimary>
                      <InteractionTagSecondary
                        onClick={() => {
                          setSelectedTargetSolutions(prev => prev.filter(s => s !== uniqueName));
                        }}
                      >
                        <DismissRegular />
                      </InteractionTagSecondary>
                    </InteractionTag>
                  ))}
                </TagGroup>
              )}
            </>
          )}
        </Field>

        <Field 
          label="Component Types" 
          hint={`${selectedComponentTypes.length} of ${allComponentTypes.length} types selected`}
        >
          {loadingComponentTypes ? (
            <Spinner size="tiny" />
          ) : (
            <>
              <Combobox
                placeholder="Select component types..."
                multiselect
                selectedOptions={selectedComponentTypes}
                onOptionSelect={(_, data) => {
                  setSelectedComponentTypes(data.selectedOptions);
                }}
              >
                {allComponentTypes.map((type) => (
                  <Option key={type.name} value={type.name} text={type.displayName}>
                    {type.displayName}
                  </Option>
                ))}
              </Combobox>
              <div style={{ marginTop: tokens.spacingVerticalXS }}>
                <Button
                  size="small"
                  appearance="subtle"
                  onClick={() => setSelectedComponentTypes(allComponentTypes.map(ct => ct.name))}
                  disabled={selectedComponentTypes.length === allComponentTypes.length}
                >
                  Select All
                </Button>
                <Button
                  size="small"
                  appearance="subtle"
                  onClick={() => setSelectedComponentTypes([])}
                  disabled={selectedComponentTypes.length === 0}
                  style={{ marginLeft: tokens.spacingHorizontalS }}
                >
                  Clear All
                </Button>
              </div>
            </>
          )}
        </Field>

        <Divider />

        <div className={styles.buttonRow}>
          <Button
            appearance="primary"
            icon={<PlayRegular />}
            onClick={handleIndex}
            disabled={loading || loadingSolutions || loadingComponentTypes}
          >
            {loading ? 'Indexing...' : 'Start Indexing'}
          </Button>
          
          <Button
            appearance="secondary"
            icon={<ArrowSyncRegular />}
            onClick={handleClear}
            disabled={loading}
          >
            Clear Index
          </Button>
        </div>

        {operationId && (
          <MessageBar intent="info">
            <MessageBarBody>
              Indexing operation started. Check the footer for progress updates.
            </MessageBarBody>
          </MessageBar>
        )}

        {indexCompletion && indexCompletion.success && (
          <div className={styles.statsCard}>
            <Text weight="semibold" size={400}>
              <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenForeground1 }} />
              {' '}Indexing Complete
            </Text>
            {indexCompletion.stats && (
              <div style={{ marginTop: tokens.spacingVerticalS }}>
                <Text size={300}>
                  Solutions: {indexCompletion.stats.solutionCount} | 
                  Components: {indexCompletion.stats.componentCount} | 
                  Layers: {indexCompletion.stats.layerCount}
                </Text>
              </div>
            )}
          </div>
        )}

        {indexCompletion && !indexCompletion.success && indexCompletion.errorMessage && (
          <MessageBar intent="error">
            <MessageBarBody>
              <DismissCircleRegular />
              {' '}{indexCompletion.errorMessage}
            </MessageBarBody>
          </MessageBar>
        )}
      </div>
    </Card>
  );
};
