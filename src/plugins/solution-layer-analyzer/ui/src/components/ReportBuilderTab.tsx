import React, { useState, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  Textarea,
  Badge,
  Divider,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Label,
} from '@fluentui/react-components';
import {
  AddRegular,
  ArrowUpRegular,
  ArrowDownRegular,
  DeleteRegular,
  CopyRegular,
  FolderRegular,
  DocumentRegular,
  PlayRegular,
  SaveRegular,
  FolderOpenRegular,
  ArrowExportRegular,
  FilterRegular,
  ArrowSyncRegular,
} from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
import { AdvancedFilterBuilder } from './AdvancedFilterBuilder';
import { FilterNode } from '../types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalL,
  },
  toolbar: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
    paddingBlock: tokens.spacingVerticalM,
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  groupCard: {
    marginBottom: tokens.spacingVerticalM,
  },
  groupHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
  },
  reportItem: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
  reportInfo: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    flex: 1,
  },
  reportActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  formField: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginBottom: tokens.spacingVerticalM,
  },
  filterSection: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalM,
  },
  resultsContainer: {
    marginTop: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
});

interface Report {
  name: string;
  description?: string;
  severity: 'Information' | 'Warning' | 'Critical';
  recommendedAction?: string;
  queryJson: string;
  displayOrder: number;
}

interface ReportGroup {
  name: string;
  displayOrder: number;
  reports: Report[];
}

interface ReportConfig {
  sourceSolutions: string[];
  targetSolutions: string[];
  componentTypes?: number[];
  reportGroups: ReportGroup[];
  ungroupedReports: Report[];
}

export const ReportBuilderTab: React.FC = () => {
  const styles = useStyles();
  const pluginApi = usePluginApi();
  const availableSolutions = useAppStore((state) => state.availableSolutions);
  const indexConfig = useAppStore((state) => state.indexConfig);
  
  const [config, setConfig] = useState<ReportConfig>({
    sourceSolutions: indexConfig.sourceSolutions || [],
    targetSolutions: indexConfig.targetSolutions || [],
    componentTypes: [],
    reportGroups: [],
    ungroupedReports: [],
  });

  const [editingReport, setEditingReport] = useState<{
    groupIndex: number | null;
    reportIndex: number;
    report: Report;
  } | null>(null);

  const [showFilterBuilder, setShowFilterBuilder] = useState(false);
  const [currentFilter, setCurrentFilter] = useState<FilterNode | null>(null);
  const [reportResults, setReportResults] = useState<any>(null);
  const [isRunning, setIsRunning] = useState(false);

  // Add new group
  const handleAddGroup = useCallback(() => {
    const newGroup: ReportGroup = {
      name: `New Group ${config.reportGroups.length + 1}`,
      displayOrder: config.reportGroups.length,
      reports: [],
    };
    setConfig((prev) => ({
      ...prev,
      reportGroups: [...prev.reportGroups, newGroup],
    }));
  }, [config.reportGroups.length]);

  // Add new report
  const handleAddReport = useCallback((groupIndex: number | null) => {
    const newReport: Report = {
      name: 'New Report',
      severity: 'Information',
      queryJson: JSON.stringify(null),
      displayOrder: groupIndex !== null 
        ? config.reportGroups[groupIndex].reports.length
        : config.ungroupedReports.length,
    };

    if (groupIndex !== null) {
      setConfig((prev) => {
        const newGroups = [...prev.reportGroups];
        newGroups[groupIndex].reports.push(newReport);
        return { ...prev, reportGroups: newGroups };
      });
    } else {
      setConfig((prev) => ({
        ...prev,
        ungroupedReports: [...prev.ungroupedReports, newReport],
      }));
    }

    setEditingReport({
      groupIndex,
      reportIndex: groupIndex !== null 
        ? config.reportGroups[groupIndex].reports.length 
        : config.ungroupedReports.length,
      report: newReport,
    });
  }, [config]);

  // Move group up/down
  const handleMoveGroup = useCallback((index: number, direction: 'up' | 'down') => {
    const newGroups = [...config.reportGroups];
    const targetIndex = direction === 'up' ? index - 1 : index + 1;
    
    if (targetIndex >= 0 && targetIndex < newGroups.length) {
      [newGroups[index], newGroups[targetIndex]] = [newGroups[targetIndex], newGroups[index]];
      newGroups.forEach((group, i) => {
        group.displayOrder = i;
      });
      setConfig((prev) => ({ ...prev, reportGroups: newGroups }));
    }
  }, [config.reportGroups]);

  // Move report up/down
  const handleMoveReport = useCallback((groupIndex: number | null, reportIndex: number, direction: 'up' | 'down') => {
    const targetIndex = direction === 'up' ? reportIndex - 1 : reportIndex + 1;
    
    if (groupIndex !== null) {
      const newGroups = [...config.reportGroups];
      const reports = [...newGroups[groupIndex].reports];
      
      if (targetIndex >= 0 && targetIndex < reports.length) {
        [reports[reportIndex], reports[targetIndex]] = [reports[targetIndex], reports[reportIndex]];
        reports.forEach((report, i) => {
          report.displayOrder = i;
        });
        newGroups[groupIndex].reports = reports;
        setConfig((prev) => ({ ...prev, reportGroups: newGroups }));
      }
    } else {
      const reports = [...config.ungroupedReports];
      if (targetIndex >= 0 && targetIndex < reports.length) {
        [reports[reportIndex], reports[targetIndex]] = [reports[targetIndex], reports[reportIndex]];
        reports.forEach((report, i) => {
          report.displayOrder = i;
        });
        setConfig((prev) => ({ ...prev, ungroupedReports: reports }));
      }
    }
  }, [config]);

  // Delete group
  const handleDeleteGroup = useCallback((index: number) => {
    setConfig((prev) => ({
      ...prev,
      reportGroups: prev.reportGroups.filter((_, i) => i !== index),
    }));
  }, []);

  // Delete report
  const handleDeleteReport = useCallback((groupIndex: number | null, reportIndex: number) => {
    if (groupIndex !== null) {
      setConfig((prev) => {
        const newGroups = [...prev.reportGroups];
        newGroups[groupIndex].reports = newGroups[groupIndex].reports.filter((_, i) => i !== reportIndex);
        return { ...prev, reportGroups: newGroups };
      });
    } else {
      setConfig((prev) => ({
        ...prev,
        ungroupedReports: prev.ungroupedReports.filter((_, i) => i !== reportIndex),
      }));
    }
  }, []);

  // Duplicate report
  const handleDuplicateReport = useCallback((groupIndex: number | null, reportIndex: number) => {
    const report = groupIndex !== null 
      ? config.reportGroups[groupIndex].reports[reportIndex]
      : config.ungroupedReports[reportIndex];
    
    const duplicated = {
      ...report,
      name: `${report.name} (Copy)`,
    };

    if (groupIndex !== null) {
      setConfig((prev) => {
        const newGroups = [...prev.reportGroups];
        newGroups[groupIndex].reports.splice(reportIndex + 1, 0, duplicated);
        return { ...prev, reportGroups: newGroups };
      });
    } else {
      setConfig((prev) => {
        const newReports = [...prev.ungroupedReports];
        newReports.splice(reportIndex + 1, 0, duplicated);
        return { ...prev, ungroupedReports: newReports };
      });
    }
  }, [config]);

  // Load config from file
  const handleLoadConfig = useCallback(async () => {
    try {
      const result = await pluginApi.loadIndexConfigs({ connectionId: 'default' });
      if (result) {
        // Parse and set the loaded config
        console.log('Loaded config:', result);
      }
    } catch (error) {
      console.error('Failed to load config:', error);
    }
  }, [pluginApi]);

  // Save config to file
  const handleSaveConfig = useCallback(async () => {
    try {
      await pluginApi.saveIndexConfig({
        name: 'report-config',
        connectionId: 'default',
        sourceSolutions: config.sourceSolutions,
        targetSolutions: config.targetSolutions,
        componentTypes: (config.componentTypes || []).map(String),
        payloadMode: 'lazy',
      });
    } catch (error) {
      console.error('Failed to save config:', error);
    }
  }, [pluginApi, config]);

  // Run all reports
  const handleRunReports = useCallback(async () => {
    setIsRunning(true);
    try {
      // Execute reports via query
      const results = await pluginApi.queryComponentsSync(null, 0, 1000, 'default');
      setReportResults({ components: results });
    } catch (error) {
      console.error('Failed to run reports:', error);
    } finally {
      setIsRunning(false);
    }
  }, [pluginApi]);

  // Export results
  const handleExportResults = useCallback(async (format: 'yaml' | 'json' | 'csv') => {
    try {
      // Export results by downloading as file
      const blob = new Blob([JSON.stringify(reportResults, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report-results.${format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Failed to export results:', error);
    }
  }, [reportResults]);

  return (
    <div className={styles.container}>
      {/* Toolbar */}
      <div className={styles.toolbar}>
        <Button
          icon={<AddRegular />}
          appearance="primary"
          onClick={handleAddGroup}
        >
          Add Group
        </Button>
        <Button
          icon={<AddRegular />}
          onClick={() => handleAddReport(null)}
        >
          Add Ungrouped Report
        </Button>
        <Divider vertical />
        <Button
          icon={<FolderOpenRegular />}
          onClick={handleLoadConfig}
        >
          Load Config
        </Button>
        <Button
          icon={<SaveRegular />}
          onClick={handleSaveConfig}
        >
          Save Config
        </Button>
        <Divider vertical />
        <Button
          icon={<PlayRegular />}
          appearance="primary"
          onClick={handleRunReports}
          disabled={isRunning}
        >
          {isRunning ? 'Running...' : 'Run All Reports'}
        </Button>
        {reportResults && (
          <>
            <Button
              icon={<ArrowExportRegular />}
              onClick={() => handleExportResults('yaml')}
            >
              Export YAML
            </Button>
            <Button
              icon={<ArrowExportRegular />}
              onClick={() => handleExportResults('json')}
            >
              Export JSON
            </Button>
            <Button
              icon={<ArrowExportRegular />}
              onClick={() => handleExportResults('csv')}
            >
              Export CSV
            </Button>
          </>
        )}
      </div>

      {/* Content */}
      <div className={styles.content}>
        {/* Report Groups */}
        {config.reportGroups.map((group, groupIndex) => (
          <Card key={groupIndex} className={styles.groupCard}>
            <div className={styles.groupHeader}>
              <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
                <FolderRegular />
                <Text weight="semibold">{group.name}</Text>
                <Badge>{group.reports.length} reports</Badge>
              </div>
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
                <Button
                  size="small"
                  icon={<ArrowUpRegular />}
                  onClick={() => handleMoveGroup(groupIndex, 'up')}
                  disabled={groupIndex === 0}
                />
                <Button
                  size="small"
                  icon={<ArrowDownRegular />}
                  onClick={() => handleMoveGroup(groupIndex, 'down')}
                  disabled={groupIndex === config.reportGroups.length - 1}
                />
                <Button
                  size="small"
                  icon={<AddRegular />}
                  onClick={() => handleAddReport(groupIndex)}
                />
                <Button
                  size="small"
                  icon={<DeleteRegular />}
                  onClick={() => handleDeleteGroup(groupIndex)}
                />
              </div>
            </div>

            {/* Reports in group */}
            {group.reports.map((report, reportIndex) => (
              <div key={reportIndex} className={styles.reportItem}>
                <div className={styles.reportInfo}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <DocumentRegular />
                    <Text weight="semibold">{report.name}</Text>
                    <Badge
                      color={
                        report.severity === 'Critical' ? 'danger' :
                        report.severity === 'Warning' ? 'warning' :
                        'informative'
                      }
                    >
                      {report.severity}
                    </Badge>
                  </div>
                  {report.description && (
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {report.description}
                    </Text>
                  )}
                </div>
                <div className={styles.reportActions}>
                  <Button
                    size="small"
                    icon={<ArrowUpRegular />}
                    onClick={() => handleMoveReport(groupIndex, reportIndex, 'up')}
                    disabled={reportIndex === 0}
                  />
                  <Button
                    size="small"
                    icon={<ArrowDownRegular />}
                    onClick={() => handleMoveReport(groupIndex, reportIndex, 'down')}
                    disabled={reportIndex === group.reports.length - 1}
                  />
                  <Button
                    size="small"
                    icon={<CopyRegular />}
                    onClick={() => handleDuplicateReport(groupIndex, reportIndex)}
                  />
                  <Button
                    size="small"
                    appearance="primary"
                    onClick={() => setEditingReport({ groupIndex, reportIndex, report })}
                  >
                    Edit
                  </Button>
                  <Button
                    size="small"
                    icon={<DeleteRegular />}
                    onClick={() => handleDeleteReport(groupIndex, reportIndex)}
                  />
                </div>
              </div>
            ))}
          </Card>
        ))}

        {/* Ungrouped Reports */}
        {config.ungroupedReports.length > 0 && (
          <Card className={styles.groupCard}>
            <div className={styles.groupHeader}>
              <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
                <Text weight="semibold">Ungrouped Reports</Text>
                <Badge>{config.ungroupedReports.length} reports</Badge>
              </div>
            </div>

            {config.ungroupedReports.map((report, reportIndex) => (
              <div key={reportIndex} className={styles.reportItem}>
                <div className={styles.reportInfo}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <DocumentRegular />
                    <Text weight="semibold">{report.name}</Text>
                    <Badge
                      color={
                        report.severity === 'Critical' ? 'danger' :
                        report.severity === 'Warning' ? 'warning' :
                        'informative'
                      }
                    >
                      {report.severity}
                    </Badge>
                  </div>
                  {report.description && (
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {report.description}
                    </Text>
                  )}
                </div>
                <div className={styles.reportActions}>
                  <Button
                    size="small"
                    icon={<ArrowUpRegular />}
                    onClick={() => handleMoveReport(null, reportIndex, 'up')}
                    disabled={reportIndex === 0}
                  />
                  <Button
                    size="small"
                    icon={<ArrowDownRegular />}
                    onClick={() => handleMoveReport(null, reportIndex, 'down')}
                    disabled={reportIndex === config.ungroupedReports.length - 1}
                  />
                  <Button
                    size="small"
                    icon={<CopyRegular />}
                    onClick={() => handleDuplicateReport(null, reportIndex)}
                  />
                  <Button
                    size="small"
                    appearance="primary"
                    onClick={() => setEditingReport({ groupIndex: null, reportIndex, report })}
                  >
                    Edit
                  </Button>
                  <Button
                    size="small"
                    icon={<DeleteRegular />}
                    onClick={() => handleDeleteReport(null, reportIndex)}
                  />
                </div>
              </div>
            ))}
          </Card>
        )}
      </div>

      {/* Results Display */}
      {reportResults && (
        <div className={styles.resultsContainer}>
          <Text weight="semibold" size={500}>Report Results</Text>
          <pre style={{ marginTop: tokens.spacingVerticalM, maxHeight: '400px', overflow: 'auto' }}>
            {JSON.stringify(reportResults, null, 2)}
          </pre>
        </div>
      )}

      {/* Edit Report Dialog */}
      {editingReport && (
        <Dialog
          open={!!editingReport}
          onOpenChange={(_, data) => !data.open && setEditingReport(null)}
        >
          <DialogSurface style={{ maxWidth: '800px' }}>
            <DialogBody>
              <DialogTitle>Edit Report</DialogTitle>
              <DialogContent>
                <div className={styles.formField}>
                  <Label htmlFor="report-name">Name</Label>
                  <Input
                    id="report-name"
                    value={editingReport.report.name}
                    onChange={(_, data) => {
                      setEditingReport({
                        ...editingReport,
                        report: { ...editingReport.report, name: data.value },
                      });
                    }}
                  />
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="report-description">Description</Label>
                  <Textarea
                    id="report-description"
                    value={editingReport.report.description || ''}
                    onChange={(_, data) => {
                      setEditingReport({
                        ...editingReport,
                        report: { ...editingReport.report, description: data.value },
                      });
                    }}
                  />
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="report-severity">Severity</Label>
                  <Dropdown
                    id="report-severity"
                    value={editingReport.report.severity}
                    onOptionSelect={(_, data) => {
                      setEditingReport({
                        ...editingReport,
                        report: { ...editingReport.report, severity: data.optionValue as any },
                      });
                    }}
                  >
                    <Option value="Information">Information</Option>
                    <Option value="Warning">Warning</Option>
                    <Option value="Critical">Critical</Option>
                  </Dropdown>
                </div>

                <div className={styles.formField}>
                  <Label htmlFor="report-action">Recommended Action</Label>
                  <Textarea
                    id="report-action"
                    value={editingReport.report.recommendedAction || ''}
                    onChange={(_, data) => {
                      setEditingReport({
                        ...editingReport,
                        report: { ...editingReport.report, recommendedAction: data.value },
                      });
                    }}
                  />
                </div>

                <div className={styles.filterSection}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalM }}>
                    <Label>Filter Query</Label>
                    <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
                      <Button
                        size="small"
                        icon={<ArrowSyncRegular />}
                        onClick={() => {
                          // Set filter from analysis tab
                          const analysisFilter = useAppStore.getState().filterBarState.advancedFilter;
                          setCurrentFilter(analysisFilter);
                          setEditingReport({
                            ...editingReport,
                            report: {
                              ...editingReport.report,
                              queryJson: JSON.stringify(analysisFilter),
                            },
                          });
                        }}
                      >
                        From Analysis
                      </Button>
                      <Button
                        size="small"
                        icon={<FilterRegular />}
                        onClick={() => setShowFilterBuilder(!showFilterBuilder)}
                      >
                        {showFilterBuilder ? 'Hide' : 'Show'} Builder
                      </Button>
                    </div>
                  </div>

                  {showFilterBuilder && (
                    <AdvancedFilterBuilder
                      initialFilter={currentFilter}
                      onFilterChange={(newFilter: FilterNode | null) => {
                        setCurrentFilter(newFilter);
                        setEditingReport({
                          ...editingReport,
                          report: {
                            ...editingReport.report,
                            queryJson: JSON.stringify(newFilter),
                          },
                        });
                      }}
                      solutions={availableSolutions.map(s => s.uniqueName)}
                    />
                  )}
                </div>
              </DialogContent>
              <DialogActions>
                <Button onClick={() => setEditingReport(null)}>Cancel</Button>
                <Button
                  appearance="primary"
                  onClick={() => {
                    // Save the edited report back to config
                    if (editingReport.groupIndex !== null) {
                      setConfig((prev) => {
                        const newGroups = [...prev.reportGroups];
                        newGroups[editingReport.groupIndex!].reports[editingReport.reportIndex] = editingReport.report;
                        return { ...prev, reportGroups: newGroups };
                      });
                    } else {
                      setConfig((prev) => {
                        const newReports = [...prev.ungroupedReports];
                        newReports[editingReport.reportIndex] = editingReport.report;
                        return { ...prev, ungroupedReports: newReports };
                      });
                    }
                    setEditingReport(null);
                  }}
                >
                  Save
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}
    </div>
  );
};
