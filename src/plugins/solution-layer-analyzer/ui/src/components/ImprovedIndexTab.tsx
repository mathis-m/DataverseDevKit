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
  InfoLabel,
  Combobox,
  Option,
  TagGroup,
  InteractionTag,
  InteractionTagPrimary,
  InteractionTagSecondary,
} from '@fluentui/react-components';
import { PlayRegular, ArrowSyncRegular, CheckmarkCircleRegular, DismissCircleRegular, DismissRegular } from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
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

interface IndexTabProps {
  onIndexComplete: (stats: IndexStats) => void;
}

export const ImprovedIndexTab: React.FC<IndexTabProps> = ({ onIndexComplete }) => {
  const styles = useStyles();
  const { indexSolutions, clearIndex, loading, indexCompletion } = usePluginApi();
  
  // Get solutions and component types from global store
  const { availableSolutions, availableComponentTypes, metadataLoaded } = useAppStore();
  
  const [selectedSourceSolutions, setSelectedSourceSolutions] = useState<string[]>([]);
  const [selectedTargetSolutions, setSelectedTargetSolutions] = useState<string[]>([]);
  const [selectedComponentTypes, setSelectedComponentTypes] = useState<string[]>([]);
  
  const [operationId, setOperationId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Set all component types as selected by default once loaded
  useEffect(() => {
    if (availableComponentTypes.length > 0 && selectedComponentTypes.length === 0) {
      setSelectedComponentTypes(availableComponentTypes.map(ct => ct.name));
    }
  }, [availableComponentTypes, selectedComponentTypes.length]);

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
    const solution = availableSolutions.find(s => s.uniqueName === uniqueName);
    return solution ? `${solution.displayName} (${uniqueName})` : uniqueName;
  };

  const isIndexing = !!loading?.indexing;
  const isBusy = isIndexing || !metadataLoaded;

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

        <Field 
          label={
            <InfoLabel info="Select the base or core solutions that contain the original components. These are typically your foundation solutions that other solutions layer on top of.">
              Source Solutions
            </InfoLabel>
          }
          hint="Solutions where components originate"
          required
        >
          {!metadataLoaded ? (
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
                {availableSolutions.map((solution) => (
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

        <Field 
          label={
            <InfoLabel info="Select the project or customization solutions that may layer components on top of source solutions. These are analyzed to identify layering patterns and potential conflicts.">
              Target Solutions
            </InfoLabel>
          }
          hint="Solutions to analyze for layering"
          required
        >
          {!metadataLoaded ? (
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
                {availableSolutions.map((solution) => (
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
          label={
            <InfoLabel info="Choose which types of components to include in the index. All types are selected by default for comprehensive analysis. You can deselect types that are not relevant to your analysis.">
              Component Types
            </InfoLabel>
          }
          hint={`${selectedComponentTypes.length} of ${availableComponentTypes.length} types selected`}
        >
          {!metadataLoaded ? (
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
                {availableComponentTypes.map((type) => (
                  <Option key={type.name} value={type.name} text={type.displayName}>
                    {type.displayName}
                  </Option>
                ))}
              </Combobox>
              <div style={{ marginTop: tokens.spacingVerticalXS }}>
                <Button
                  size="small"
                  appearance="subtle"
                  onClick={() => setSelectedComponentTypes(availableComponentTypes.map(ct => ct.name))}
                  disabled={selectedComponentTypes.length === availableComponentTypes.length}
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
            disabled={isBusy}
          >
            {isIndexing ? 'Indexing...' : 'Start Indexing'}
          </Button>
          
          <Button
            appearance="secondary"
            icon={<ArrowSyncRegular />}
            onClick={handleClear}
            disabled={isIndexing}
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
                  Solutions: {indexCompletion.stats.solutions} | 
                  Components: {indexCompletion.stats.components} | 
                  Layers: {indexCompletion.stats.layers}
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
