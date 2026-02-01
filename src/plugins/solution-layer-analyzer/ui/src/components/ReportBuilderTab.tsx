import React, { useState, useCallback, useRef } from 'react';
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
  Spinner,
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
  FilterRegular,
  ArrowSyncRegular,
  EditRegular,
  CheckmarkRegular,
  DismissRegular,
} from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';
import { useAppStore } from '../store/useAppStore';
import { AdvancedFilterBuilder } from './AdvancedFilterBuilder';
import { RunReportsDialog } from './RunReportsDialog';
import { ReportSummaryPanel } from './ReportSummaryPanel';
import { FilterNode, Report, ReportGroup, ReportSeverity, ReportConfigFormat } from '../types';

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
    flexWrap: 'wrap',
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
  groupNameContainer: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  groupNameInput: {
    minWidth: '200px',
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
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

/** Generates a unique ID */
const generateId = () => `${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

export const ReportBuilderTab: React.FC = () => {
  const styles = useStyles();
  const pluginApi = usePluginApi();
  const fileInputRef = useRef<HTMLInputElement>(null);
  
  // Get state from Zustand store
  const availableSolutions = useAppStore((state) => state.availableSolutions);
  const indexConfig = useAppStore((state) => state.indexConfig);
  const reportBuilderState = useAppStore((state) => state.reportBuilderState);
  const reportRunState = useAppStore((state) => state.reportRunState);
  const setFilterBarState = useAppStore((state) => state.setFilterBarState);
  const setSelectedTab = useAppStore((state) => state.setSelectedTab);
  
  // Report builder actions from Zustand
  const addReportGroup = useAppStore((state) => state.addReportGroup);
  const updateReportGroup = useAppStore((state) => state.updateReportGroup);
  const deleteReportGroup = useAppStore((state) => state.deleteReportGroup);
  const addReport = useAppStore((state) => state.addReport);
  const updateReport = useAppStore((state) => state.updateReport);
  const deleteReport = useAppStore((state) => state.deleteReport);
  const reorderReportGroups = useAppStore((state) => state.reorderReportGroups);
  const reorderReports = useAppStore((state) => state.reorderReports);
  const setReportBuilderState = useAppStore((state) => state.setReportBuilderState);

  // Local UI state
  const [editingReport, setEditingReport] = useState<{
    groupId: string | null;
    report: Report;
  } | null>(null);
  const [showFilterBuilder, setShowFilterBuilder] = useState(false);
  const [currentFilter, setCurrentFilter] = useState<FilterNode | null>(null);
  const [showRunDialog, setShowRunDialog] = useState(false);
  const [editingGroupId, setEditingGroupId] = useState<string | null>(null);
  const [editingGroupName, setEditingGroupName] = useState('');
  const [saveFormat, setSaveFormat] = useState<ReportConfigFormat>('yaml');
  const [isSaving, setIsSaving] = useState(false);

  const { reportGroups, ungroupedReports } = reportBuilderState;

  // Execute filter - navigate to Analysis tab with the report's filter
  const handleExecuteFilter = useCallback((report: Report) => {
    try {
      const filter = JSON.parse(report.queryJson);
      setFilterBarState({ 
        advancedFilter: filter,
        advancedMode: true 
      });
      setSelectedTab('analysis');
    } catch (error) {
      console.error('Failed to parse filter:', error);
    }
  }, [setFilterBarState, setSelectedTab]);

  // Add new group
  const handleAddGroup = useCallback(() => {
    const newGroup: ReportGroup = {
      id: generateId(),
      name: `New Group ${reportGroups.length + 1}`,
      displayOrder: reportGroups.length,
      reports: [],
    };
    addReportGroup(newGroup);
  }, [reportGroups.length, addReportGroup]);

  // Start editing group name
  const handleStartEditGroupName = useCallback((group: ReportGroup) => {
    setEditingGroupId(group.id);
    setEditingGroupName(group.name);
  }, []);

  // Save group name
  const handleSaveGroupName = useCallback(() => {
    if (editingGroupId && editingGroupName.trim()) {
      updateReportGroup(editingGroupId, { name: editingGroupName.trim() });
    }
    setEditingGroupId(null);
    setEditingGroupName('');
  }, [editingGroupId, editingGroupName, updateReportGroup]);

  // Cancel editing group name
  const handleCancelEditGroupName = useCallback(() => {
    setEditingGroupId(null);
    setEditingGroupName('');
  }, []);

  // Add new report
  const handleAddReport = useCallback((groupId: string | null) => {
    const newReport: Report = {
      id: generateId(),
      name: 'New Report',
      severity: 'Information',
      queryJson: JSON.stringify(null),
      displayOrder: groupId 
        ? (reportGroups.find(g => g.id === groupId)?.reports.length || 0)
        : ungroupedReports.length,
    };

    addReport(newReport, groupId || undefined);
    
    setEditingReport({
      groupId,
      report: newReport,
    });
  }, [reportGroups, ungroupedReports.length, addReport]);

  // Move group up/down
  const handleMoveGroup = useCallback((groupId: string, direction: 'up' | 'down') => {
    const currentIndex = reportGroups.findIndex(g => g.id === groupId);
    if (currentIndex === -1) return;
    
    const targetIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;
    if (targetIndex < 0 || targetIndex >= reportGroups.length) return;
    
    const newOrder = [...reportGroups.map(g => g.id)];
    [newOrder[currentIndex], newOrder[targetIndex]] = [newOrder[targetIndex], newOrder[currentIndex]];
    reorderReportGroups(newOrder);
  }, [reportGroups, reorderReportGroups]);

  // Move report up/down
  const handleMoveReport = useCallback((groupId: string | null, reportId: string, direction: 'up' | 'down') => {
    const reports = groupId 
      ? reportGroups.find(g => g.id === groupId)?.reports || []
      : ungroupedReports;
    
    const currentIndex = reports.findIndex(r => r.id === reportId);
    if (currentIndex === -1) return;
    
    const targetIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;
    if (targetIndex < 0 || targetIndex >= reports.length) return;
    
    const newOrder = [...reports.map(r => r.id)];
    [newOrder[currentIndex], newOrder[targetIndex]] = [newOrder[targetIndex], newOrder[currentIndex]];
    reorderReports(groupId, newOrder);
  }, [reportGroups, ungroupedReports, reorderReports]);

  // Delete group
  const handleDeleteGroup = useCallback((groupId: string) => {
    deleteReportGroup(groupId);
  }, [deleteReportGroup]);

  // Delete report
  const handleDeleteReport = useCallback((groupId: string | null, reportId: string) => {
    deleteReport(reportId, groupId || undefined);
  }, [deleteReport]);

  // Duplicate report
  const handleDuplicateReport = useCallback((groupId: string | null, report: Report) => {
    const duplicated: Report = {
      ...report,
      id: generateId(),
      name: `${report.name} (Copy)`,
    };
    addReport(duplicated, groupId || undefined);
  }, [addReport]);

  // Load config from file
  const handleLoadConfig = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  const handleFileSelected = useCallback(async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    
    try {
      const content = await file.text();
      const format = file.name.endsWith('.json') ? 'json' 
        : file.name.endsWith('.xml') ? 'xml' 
        : 'yaml';
      
      const result = await pluginApi.parseReportConfig(content, format);
      
      if (result.config) {
        setReportBuilderState({
          reportGroups: result.config.reportGroups || [],
          ungroupedReports: result.config.ungroupedReports || [],
        });
      }
      
      if (result.warnings?.length) {
        console.warn('Config load warnings:', result.warnings);
      }
    } catch (error) {
      console.error('Failed to load config:', error);
    }
    
    // Reset file input
    event.target.value = '';
  }, [pluginApi, setReportBuilderState]);

  // Save config to file
  const handleSaveConfig = useCallback(async () => {
    setIsSaving(true);
    try {
      const config = {
        sourceSolutions: indexConfig.sourceSolutions || [],
        targetSolutions: indexConfig.targetSolutions || [],
        componentTypes: [],
        reportGroups,
        ungroupedReports,
      };
      
      const result = await pluginApi.serializeReportConfig(config, saveFormat);
      
      const mimeTypes: Record<ReportConfigFormat, string> = {
        json: 'application/json',
        yaml: 'application/x-yaml',
        xml: 'application/xml',
      };
      
      const blob = new Blob([result.content], { type: mimeTypes[saveFormat] });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report-config.${saveFormat}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Failed to save config:', error);
    } finally {
      setIsSaving(false);
    }
  }, [pluginApi, indexConfig, reportGroups, ungroupedReports, saveFormat]);

  // Save edited report
  const handleSaveReport = useCallback(() => {
    if (!editingReport) return;
    
    updateReport(editingReport.report.id, editingReport.report, editingReport.groupId || undefined);
    setEditingReport(null);
  }, [editingReport, updateReport]);

  const getSeverityColor = (severity: ReportSeverity): 'danger' | 'warning' | 'informative' => {
    switch (severity) {
      case 'Critical': return 'danger';
      case 'Warning': return 'warning';
      default: return 'informative';
    }
  };

  const totalReports = reportGroups.reduce((sum, g) => sum + g.reports.length, ungroupedReports.length);

  return (
    <div className={styles.container}>
      {/* Hidden file input for loading configs */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".yaml,.yml,.json,.xml"
        style={{ display: 'none' }}
        onChange={handleFileSelected}
      />
      
      {/* Report Summary Panel (shown when results exist) */}
      <ReportSummaryPanel />
      
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
        <Dropdown
          value={saveFormat}
          onOptionSelect={(_, d) => setSaveFormat(d.optionValue as ReportConfigFormat)}
          style={{ minWidth: '80px' }}
        >
          <Option value="yaml">YAML</Option>
          <Option value="json">JSON</Option>
          <Option value="xml">XML</Option>
        </Dropdown>
        <Button
          icon={isSaving ? <Spinner size="tiny" /> : <SaveRegular />}
          onClick={handleSaveConfig}
          disabled={isSaving}
        >
          Save Config
        </Button>
        <Divider vertical />
        <Button
          icon={<PlayRegular />}
          appearance="primary"
          onClick={() => setShowRunDialog(true)}
          disabled={reportRunState.isRunning || totalReports === 0}
        >
          {reportRunState.isRunning ? 'Running...' : 'Run Reports'}
        </Button>
      </div>

      {/* Content */}
      <div className={styles.content}>
        {/* Empty state */}
        {reportGroups.length === 0 && ungroupedReports.length === 0 && (
          <div className={styles.emptyState}>
            <Text size={400}>No reports configured</Text>
            <Text size={300} style={{ display: 'block', marginTop: tokens.spacingVerticalS }}>
              Add a group or ungrouped report to get started, or load a configuration file.
            </Text>
          </div>
        )}
        
        {/* Report Groups */}
        {reportGroups.map((group, groupIndex) => (
          <Card key={group.id} className={styles.groupCard}>
            <div className={styles.groupHeader}>
              <div className={styles.groupNameContainer}>
                <FolderRegular />
                {editingGroupId === group.id ? (
                  <>
                    <Input
                      className={styles.groupNameInput}
                      value={editingGroupName}
                      onChange={(_, d) => setEditingGroupName(d.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') handleSaveGroupName();
                        if (e.key === 'Escape') handleCancelEditGroupName();
                      }}
                      autoFocus
                    />
                    <Button
                      size="small"
                      icon={<CheckmarkRegular />}
                      appearance="subtle"
                      onClick={handleSaveGroupName}
                    />
                    <Button
                      size="small"
                      icon={<DismissRegular />}
                      appearance="subtle"
                      onClick={handleCancelEditGroupName}
                    />
                  </>
                ) : (
                  <>
                    <Text weight="semibold">{group.name}</Text>
                    <Button
                      size="small"
                      icon={<EditRegular />}
                      appearance="subtle"
                      onClick={() => handleStartEditGroupName(group)}
                      title="Rename group"
                    />
                  </>
                )}
                <Badge>{group.reports.length} reports</Badge>
              </div>
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
                <Button
                  size="small"
                  icon={<ArrowUpRegular />}
                  onClick={() => handleMoveGroup(group.id, 'up')}
                  disabled={groupIndex === 0}
                />
                <Button
                  size="small"
                  icon={<ArrowDownRegular />}
                  onClick={() => handleMoveGroup(group.id, 'down')}
                  disabled={groupIndex === reportGroups.length - 1}
                />
                <Button
                  size="small"
                  icon={<AddRegular />}
                  onClick={() => handleAddReport(group.id)}
                />
                <Button
                  size="small"
                  icon={<DeleteRegular />}
                  onClick={() => handleDeleteGroup(group.id)}
                />
              </div>
            </div>

            {/* Reports in group */}
            {group.reports.map((report, reportIndex) => (
              <div key={report.id} className={styles.reportItem}>
                <div className={styles.reportInfo}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <DocumentRegular />
                    <Text weight="semibold">{report.name}</Text>
                    <Badge color={getSeverityColor(report.severity)}>
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
                    icon={<FilterRegular />}
                    onClick={() => handleExecuteFilter(report)}
                    title="Execute Filter in Analysis Tab"
                  />
                  <Button
                    size="small"
                    icon={<ArrowUpRegular />}
                    onClick={() => handleMoveReport(group.id, report.id, 'up')}
                    disabled={reportIndex === 0}
                  />
                  <Button
                    size="small"
                    icon={<ArrowDownRegular />}
                    onClick={() => handleMoveReport(group.id, report.id, 'down')}
                    disabled={reportIndex === group.reports.length - 1}
                  />
                  <Button
                    size="small"
                    icon={<CopyRegular />}
                    onClick={() => handleDuplicateReport(group.id, report)}
                  />
                  <Button
                    size="small"
                    appearance="primary"
                    onClick={() => setEditingReport({ groupId: group.id, report: { ...report } })}
                  >
                    Edit
                  </Button>
                  <Button
                    size="small"
                    icon={<DeleteRegular />}
                    onClick={() => handleDeleteReport(group.id, report.id)}
                  />
                </div>
              </div>
            ))}
            
            {group.reports.length === 0 && (
              <div style={{ padding: tokens.spacingVerticalM, textAlign: 'center', color: tokens.colorNeutralForeground3 }}>
                <Text size={200}>No reports in this group. Click + to add one.</Text>
              </div>
            )}
          </Card>
        ))}

        {/* Ungrouped Reports */}
        {ungroupedReports.length > 0 && (
          <Card className={styles.groupCard}>
            <div className={styles.groupHeader}>
              <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
                <Text weight="semibold">Ungrouped Reports</Text>
                <Badge>{ungroupedReports.length} reports</Badge>
              </div>
            </div>

            {ungroupedReports.map((report, reportIndex) => (
              <div key={report.id} className={styles.reportItem}>
                <div className={styles.reportInfo}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <DocumentRegular />
                    <Text weight="semibold">{report.name}</Text>
                    <Badge color={getSeverityColor(report.severity)}>
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
                    icon={<FilterRegular />}
                    onClick={() => handleExecuteFilter(report)}
                    title="Execute Filter in Analysis Tab"
                  />
                  <Button
                    size="small"
                    icon={<ArrowUpRegular />}
                    onClick={() => handleMoveReport(null, report.id, 'up')}
                    disabled={reportIndex === 0}
                  />
                  <Button
                    size="small"
                    icon={<ArrowDownRegular />}
                    onClick={() => handleMoveReport(null, report.id, 'down')}
                    disabled={reportIndex === ungroupedReports.length - 1}
                  />
                  <Button
                    size="small"
                    icon={<CopyRegular />}
                    onClick={() => handleDuplicateReport(null, report)}
                  />
                  <Button
                    size="small"
                    appearance="primary"
                    onClick={() => setEditingReport({ groupId: null, report: { ...report } })}
                  >
                    Edit
                  </Button>
                  <Button
                    size="small"
                    icon={<DeleteRegular />}
                    onClick={() => handleDeleteReport(null, report.id)}
                  />
                </div>
              </div>
            ))}
          </Card>
        )}
      </div>

      {/* Run Reports Dialog */}
      <RunReportsDialog
        open={showRunDialog}
        onClose={() => setShowRunDialog(false)}
      />

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
                        report: { ...editingReport.report, severity: data.optionValue as ReportSeverity },
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
                        icon={<FilterRegular />}
                        onClick={() => handleExecuteFilter(editingReport.report)}
                      >
                        Execute Filter
                      </Button>
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
                <Button appearance="primary" onClick={handleSaveReport}>
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
