import React, { useState, useCallback } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Dropdown,
  Option,
  Checkbox,
  Label,
  makeStyles,
  tokens,
  Text,
  Spinner,
} from '@fluentui/react-components';
import { PlayRegular } from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
import { ReportVerbosity, ReportOutputFormat, ReportConfig } from '../types';

const useStyles = makeStyles({
  formField: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginBottom: tokens.spacingVerticalM,
  },
  row: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  checkboxRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
  },
  formatDropdown: {
    marginLeft: tokens.spacingHorizontalL,
  },
  info: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalS,
  },
  reportCount: {
    padding: tokens.spacingVerticalS,
    paddingInline: tokens.spacingHorizontalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalM,
  },
});

interface RunReportsDialogProps {
  open: boolean;
  onClose: () => void;
}

export const RunReportsDialog: React.FC<RunReportsDialogProps> = ({ open, onClose }) => {
  const styles = useStyles();
  const pluginApi = usePluginApi();
  const indexConfig = useAppStore((state) => state.indexConfig);
  const reportBuilderState = useAppStore((state) => state.reportBuilderState);
  const reportRunState = useAppStore((state) => state.reportRunState);
  
  const [verbosity, setVerbosity] = useState<ReportVerbosity>('Basic');
  const [generateFile, setGenerateFile] = useState(false);
  const [outputFormat, setOutputFormat] = useState<ReportOutputFormat>('Json');
  
  // Count total reports
  const totalReports = reportBuilderState.reportGroups.reduce(
    (sum, group) => sum + group.reports.length,
    reportBuilderState.ungroupedReports.length
  );
  
  const handleRun = useCallback(async () => {
    // Build the report config from current state
    const config: ReportConfig = {
      sourceSolutions: indexConfig.sourceSolutions || [],
      targetSolutions: indexConfig.targetSolutions || [],
      componentTypes: [],
      reportGroups: reportBuilderState.reportGroups,
      ungroupedReports: reportBuilderState.ungroupedReports,
    };
    
    try {
      await pluginApi.executeReports({
        config,
        verbosity,
        format: generateFile ? outputFormat : undefined,
        generateFile,
      });
      // Close dialog immediately - results will come via events
      onClose();
    } catch (error) {
      console.error('Failed to start report execution:', error);
      // Error state is handled in the hook
    }
  }, [pluginApi, indexConfig, reportBuilderState, verbosity, generateFile, outputFormat, onClose]);

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Run All Reports</DialogTitle>
          <DialogContent>
            <div className={styles.reportCount}>
              <Text>
                <strong>{totalReports}</strong> report{totalReports !== 1 ? 's' : ''} will be executed
              </Text>
            </div>
            
            <div className={styles.formField}>
              <Label htmlFor="verbosity-select">Verbosity Level</Label>
              <Dropdown
                id="verbosity-select"
                value={verbosity}
                onOptionSelect={(_, data) => setVerbosity(data.optionValue as ReportVerbosity)}
              >
                <Option value="Basic">Basic - Summary only</Option>
                <Option value="Medium">Medium - Include changed attributes</Option>
                <Option value="Verbose">Verbose - Full attribute values</Option>
              </Dropdown>
              <Text size={200} className={styles.info}>
                {verbosity === 'Basic' && 'Shows component counts and summary statistics.'}
                {verbosity === 'Medium' && 'Adds list of changed attributes per layer.'}
                {verbosity === 'Verbose' && 'Includes full before/after values for all changed attributes.'}
              </Text>
            </div>
            
            <div className={styles.checkboxRow}>
              <Checkbox
                checked={generateFile}
                onChange={(_, data) => setGenerateFile(data.checked === true)}
                label="Generate downloadable report file"
              />
            </div>
            
            {generateFile && (
              <div className={styles.formField} style={{ marginTop: tokens.spacingVerticalM }}>
                <Label htmlFor="format-select">Output Format</Label>
                <Dropdown
                  id="format-select"
                  value={outputFormat}
                  onOptionSelect={(_, data) => setOutputFormat(data.optionValue as ReportOutputFormat)}
                >
                  <Option value="Json">JSON</Option>
                  <Option value="Yaml">YAML</Option>
                  <Option value="Csv">CSV</Option>
                </Dropdown>
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              icon={reportRunState.isRunning ? <Spinner size="tiny" /> : <PlayRegular />}
              onClick={handleRun}
              disabled={reportRunState.isRunning || totalReports === 0}
            >
              {reportRunState.isRunning ? 'Running...' : 'Run Reports'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
