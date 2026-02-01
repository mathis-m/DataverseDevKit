import React, { useState, useEffect } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Input,
  Textarea,
  Dropdown,
  Option,
  Label,
  makeStyles,
  tokens,
  Radio,
  RadioGroup,
} from '@fluentui/react-components';
import { usePluginApi } from '../hooks/usePluginApi';
import { FilterNode } from '../types';

const useStyles = makeStyles({
  formField: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginBottom: tokens.spacingVerticalM,
  },
});

interface SaveToReportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  currentFilter: FilterNode | null;
}

export const SaveToReportDialog: React.FC<SaveToReportDialogProps> = ({
  open,
  onOpenChange,
  currentFilter,
}) => {
  const styles = useStyles();
  const pluginApi = usePluginApi();

  const [mode, setMode] = useState<'new' | 'existing'>('new');
  const [reportName, setReportName] = useState('');
  const [description, setDescription] = useState('');
  const [severity, setSeverity] = useState<'Information' | 'Warning' | 'Critical'>('Information');
  const [recommendedAction, setRecommendedAction] = useState('');
  const [group, setGroup] = useState('');
  const [existingReports, setExistingReports] = useState<any[]>([]);
  const [selectedReport, setSelectedReport] = useState<string>('');

  useEffect(() => {
    if (open && mode === 'existing') {
      // Load existing reports
      pluginApi.loadFilterConfigs({ connectionId: 'default' })
        .then((result: { configs: Array<{ id: string; name: string; groupName?: string }> }) => {
          if (result && result.configs) {
            setExistingReports(result.configs.map(c => ({
              id: c.id,
              name: c.name,
              groupName: c.groupName,
            })));
          }
        })
        .catch((error: Error) => {
          console.error('Failed to load reports:', error);
        });
    }
  }, [open, mode, pluginApi]);

  const handleSave = async () => {
    try {
      if (mode === 'new') {
        // Create new report using saveFilterConfig
        await pluginApi.saveFilterConfig({
          connectionId: 'default',
          name: reportName,
          filter: currentFilter,
        });
      } else {
        // Update existing report
        const report = existingReports.find(r => r.id === selectedReport);
        if (report) {
          await pluginApi.saveFilterConfig({
            connectionId: 'default',
            name: report.name,
            filter: currentFilter,
          });
        }
      }
      onOpenChange(false);
      // Reset form
      setReportName('');
      setDescription('');
      setSeverity('Information');
      setRecommendedAction('');
      setGroup('');
    } catch (error) {
      console.error('Failed to save report:', error);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Save Filter to Report</DialogTitle>
          <DialogContent>
            <div className={styles.formField}>
              <Label>Mode</Label>
              <RadioGroup value={mode} onChange={(_, data) => setMode(data.value as 'new' | 'existing')}>
                <Radio value="new" label="Create New Report" />
                <Radio value="existing" label="Update Existing Report" />
              </RadioGroup>
            </div>

            {mode === 'new' ? (
              <>
                <div className={styles.formField}>
                  <Label htmlFor="report-name" required>
                    Report Name
                  </Label>
                  <Input
                    id="report-name"
                    value={reportName}
                    onChange={(_, data) => setReportName(data.value)}
                    placeholder="Enter report name"
                  />
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="description">Description</Label>
                  <Textarea
                    id="description"
                    value={description}
                    onChange={(_, data) => setDescription(data.value)}
                    placeholder="Describe what this report checks"
                  />
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="severity" required>
                    Severity
                  </Label>
                  <Dropdown
                    id="severity"
                    value={severity}
                    onOptionSelect={(_, data) => setSeverity(data.optionValue as any)}
                  >
                    <Option value="Information">Information</Option>
                    <Option value="Warning">Warning</Option>
                    <Option value="Critical">Critical</Option>
                  </Dropdown>
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="action">Recommended Action</Label>
                  <Textarea
                    id="action"
                    value={recommendedAction}
                    onChange={(_, data) => setRecommendedAction(data.value)}
                    placeholder="What should be done with findings"
                  />
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="group">Group (Optional)</Label>
                  <Input
                    id="group"
                    value={group}
                    onChange={(_, data) => setGroup(data.value)}
                    placeholder="Enter group name"
                  />
                </div>
              </>
            ) : (
              <div className={styles.formField}>
                <Label htmlFor="existing-report" required>
                  Select Report to Update
                </Label>
                <Dropdown
                  id="existing-report"
                  placeholder="Choose a report"
                  value={
                    existingReports.find(r => r.id === selectedReport)?.name || 'Select a report'
                  }
                  onOptionSelect={(_, data) => setSelectedReport(data.optionValue as string)}
                >
                  {existingReports.map((report) => (
                    <Option key={report.id} value={report.id} text={report.name}>
                      {report.groupName ? `${report.groupName} / ` : ''}{report.name}
                    </Option>
                  ))}
                </Dropdown>
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button
              appearance="primary"
              onClick={handleSave}
              disabled={mode === 'new' ? !reportName || !severity : !selectedReport}
            >
              Save
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
