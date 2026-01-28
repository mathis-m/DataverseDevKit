import React, { useState } from 'react';
import {
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Checkbox,
  Input,
  Field,
  InfoLabel,
  makeStyles,
  shorthands,
} from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import { useAppStore } from '../store/useAppStore';
import { usePluginApi } from '../hooks/usePluginApi';

const useStyles = makeStyles({
  content: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap('16px'),
  },
  checkboxRow: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap('12px'),
  },
  input: {
    flex: 1,
  },
});

interface SaveConfigDialogProps {
  currentConnectionId?: string;
  currentIndexHash?: string;
}

export const SaveConfigDialog: React.FC<SaveConfigDialogProps> = ({
  currentConnectionId,
  currentIndexHash,
}) => {
  const styles = useStyles();
  const [open, setOpen] = useState(false);
  const [saveIndex, setSaveIndex] = useState(false);
  const [saveFilter, setSaveFilter] = useState(false);
  const [indexName, setIndexName] = useState('');
  const [filterName, setFilterName] = useState('');
  const [saving, setSaving] = useState(false);

  const { indexConfig, filterConfig } = useAppStore();
  const { saveIndexConfig, saveFilterConfig } = usePluginApi();

  const handleSave = async () => {
    setSaving(true);
    try {
      if (saveIndex && indexConfig) {
        await saveIndexConfig({
          name: indexName,
          connectionId: indexConfig.connectionId || 'default',
          sourceSolutions: indexConfig.sourceSolutions || [],
          targetSolutions: indexConfig.targetSolutions || [],
          componentTypes: indexConfig.componentTypes || [],
          payloadMode: indexConfig.payloadMode || 'lazy',
        });
      }

      if (saveFilter && filterConfig?.filters) {
        await saveFilterConfig({
          name: filterName,
          connectionId: currentConnectionId,
          originatingIndexHash: currentIndexHash,
          filter: filterConfig.filters,
        });
      }

      setOpen(false);
      setSaveIndex(false);
      setSaveFilter(false);
      setIndexName('');
      setFilterName('');
    } catch (error) {
      console.error('Error saving config:', error);
    } finally {
      setSaving(false);
    }
  };

  const canSave = (saveIndex && indexName.trim()) || (saveFilter && filterName.trim());

  return (
    <Dialog open={open} onOpenChange={(_, data) => setOpen(data.open)}>
      <DialogTrigger disableButtonEnhancement>
        <Button icon={<Save24Regular />} appearance="subtle">
          Save Config
        </Button>
      </DialogTrigger>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Save Configuration</DialogTitle>
          <DialogContent className={styles.content}>
            <div className={styles.checkboxRow}>
              <Checkbox
                checked={saveIndex}
                onChange={(_, data) => {
                  setSaveIndex(!!data.checked);
                  if (data.checked && !indexName && !filterName) {
                    setFilterName('My Config');
                  }
                }}
                label="Save Index Config"
              />
            </div>
            {saveIndex && (
              <Field
                label={
                  <InfoLabel info="Give your index configuration a descriptive name to easily identify it later. This includes solutions and component types selected.">
                    Index Configuration Name
                  </InfoLabel>
                }
                required
              >
                <Input
                  value={indexName}
                  onChange={(_, data) => {
                    setIndexName(data.value);
                    if (!filterName && saveFilter) {
                      setFilterName(data.value);
                    }
                  }}
                  placeholder="e.g., Production Index"
                />
              </Field>
            )}

            <div className={styles.checkboxRow}>
              <Checkbox
                checked={saveFilter}
                onChange={(_, data) => setSaveFilter(!!data.checked)}
                label="Save Filter Config"
              />
            </div>
            {saveFilter && (
              <Field
                label={
                  <InfoLabel info="Name your filter configuration to save your current analysis criteria. This will auto-fill from index name if available.">
                    Filter Configuration Name
                  </InfoLabel>
                }
                required
              >
                <Input
                  value={filterName}
                  onChange={(_, data) => setFilterName(data.value)}
                  placeholder={indexName || 'e.g., My Filter'}
                />
              </Field>
            )}
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="secondary">Cancel</Button>
            </DialogTrigger>
            <Button appearance="primary" onClick={handleSave} disabled={!canSave || saving}>
              {saving ? 'Saving...' : 'Save'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
