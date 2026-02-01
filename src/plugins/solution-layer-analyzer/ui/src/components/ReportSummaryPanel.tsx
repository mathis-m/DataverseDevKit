import React, { useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  Text,
  Button,
  Badge,
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Divider,
} from '@fluentui/react-components';
import {
  ChevronDownRegular,
  ChevronUpRegular,
  ArrowDownloadRegular,
  DismissRegular,
  ErrorCircleRegular,
  WarningRegular,
  InfoRegular,
} from '@fluentui/react-icons';
import { useAppStore } from '../store/useAppStore';
import { ReportVerbosity } from '../types';

const useStyles = makeStyles({
  container: {
    marginBottom: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    paddingInline: tokens.spacingHorizontalL,
    cursor: 'pointer',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground3,
    },
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  summaryBadges: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
  content: {
    padding: tokens.spacingVerticalM,
    paddingInline: tokens.spacingHorizontalL,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  summaryGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  summaryCard: {
    padding: tokens.spacingVerticalM,
    textAlign: 'center',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  summaryValue: {
    fontSize: tokens.fontSizeHero700,
    fontWeight: tokens.fontWeightSemibold,
  },
  summaryLabel: {
    color: tokens.colorNeutralForeground3,
  },
  reportCard: {
    marginBottom: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
  },
  reportHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  reportTitle: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  componentList: {
    maxHeight: '200px',
    overflow: 'auto',
    marginTop: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  componentItem: {
    padding: tokens.spacingVerticalXS,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    '&:last-child': {
      borderBottom: 'none',
    },
  },
  progressContainer: {
    padding: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  errorContainer: {
    padding: tokens.spacingVerticalM,
    color: tokens.colorPaletteRedForeground1,
  },
});

interface ReportSummaryPanelProps {
  verbosity?: ReportVerbosity;
}

export const ReportSummaryPanel: React.FC<ReportSummaryPanelProps> = ({ verbosity = 'Basic' }) => {
  const styles = useStyles();
  const reportRunState = useAppStore((state) => state.reportRunState);
  const setReportRunState = useAppStore((state) => state.setReportRunState);
  const clearReportResults = useAppStore((state) => state.clearReportResults);
  
  const { results, outputContent, outputFormat, summaryCollapsed, isRunning, progress, lastError } = reportRunState;
  
  // Don't render if no results, not running, and no error
  if (!results && !isRunning && !lastError) {
    return null;
  }
  
  const toggleCollapsed = useCallback(() => {
    setReportRunState({ summaryCollapsed: !summaryCollapsed });
  }, [summaryCollapsed, setReportRunState]);
  
  const handleDownload = useCallback(() => {
    if (!outputContent || !outputFormat) return;
    
    const mimeTypes: Record<string, string> = {
      Json: 'application/json',
      Yaml: 'application/x-yaml',
      Csv: 'text/csv',
    };
    const extensions: Record<string, string> = {
      Json: 'json',
      Yaml: 'yaml',
      Csv: 'csv',
    };
    
    const blob = new Blob([outputContent], { type: mimeTypes[outputFormat] });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `report-results.${extensions[outputFormat]}`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }, [outputContent, outputFormat]);
  
  const handleClose = useCallback(() => {
    clearReportResults();
  }, [clearReportResults]);
  
  const getSeverityIcon = (severity: string) => {
    switch (severity) {
      case 'Critical':
        return <ErrorCircleRegular style={{ color: tokens.colorPaletteRedForeground1 }} />;
      case 'Warning':
        return <WarningRegular style={{ color: tokens.colorPaletteYellowForeground1 }} />;
      default:
        return <InfoRegular style={{ color: tokens.colorPaletteBlueForeground2 }} />;
    }
  };
  
  const getSeverityColor = (severity: string): 'danger' | 'warning' | 'informative' => {
    switch (severity) {
      case 'Critical':
        return 'danger';
      case 'Warning':
        return 'warning';
      default:
        return 'informative';
    }
  };
  
  return (
    <div className={styles.container}>
      <div className={styles.header} onClick={toggleCollapsed}>
        <div className={styles.headerLeft}>
          {summaryCollapsed ? <ChevronDownRegular /> : <ChevronUpRegular />}
          <Text weight="semibold" size={400}>
            {isRunning ? 'Running Reports...' : lastError ? 'Report Execution Failed' : 'Report Results'}
          </Text>
          {results && !summaryCollapsed && (
            <div className={styles.summaryBadges}>
              <Badge color="danger" icon={<ErrorCircleRegular />}>
                {results.summary.criticalFindings}
              </Badge>
              <Badge color="warning" icon={<WarningRegular />}>
                {results.summary.warningFindings}
              </Badge>
              <Badge color="informative" icon={<InfoRegular />}>
                {results.summary.informationalFindings}
              </Badge>
            </div>
          )}
        </div>
        <div className={styles.headerRight}>
          {outputContent && (
            <Button
              size="small"
              icon={<ArrowDownloadRegular />}
              onClick={(e) => {
                e.stopPropagation();
                handleDownload();
              }}
            >
              Download {outputFormat}
            </Button>
          )}
          <Button
            size="small"
            icon={<DismissRegular />}
            appearance="subtle"
            onClick={(e) => {
              e.stopPropagation();
              handleClose();
            }}
            title="Close results"
          />
        </div>
      </div>
      
      {!summaryCollapsed && (
        <div className={styles.content}>
          {isRunning && progress && (
            <div className={styles.progressContainer}>
              <Text size={300}>
                {progress.phase === 'starting' && 'Starting report execution...'}
                {progress.phase === 'executing' && `Executing report ${progress.currentReport} of ${progress.totalReports}: ${progress.currentReportName || ''}`}
                {progress.phase === 'generating-output' && 'Generating output file...'}
              </Text>
              <div style={{ marginTop: tokens.spacingVerticalS }}>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                  {progress.percent}% complete
                </Text>
              </div>
            </div>
          )}
          
          {lastError && (
            <div className={styles.errorContainer}>
              <ErrorCircleRegular /> {lastError}
            </div>
          )}
          
          {results && (
            <>
              <div className={styles.summaryGrid}>
                <Card className={styles.summaryCard}>
                  <div className={styles.summaryValue}>{results.summary.totalReports}</div>
                  <Text className={styles.summaryLabel}>Total Reports</Text>
                </Card>
                <Card className={styles.summaryCard}>
                  <div className={styles.summaryValue} style={{ color: tokens.colorPaletteRedForeground1 }}>
                    {results.summary.criticalFindings}
                  </div>
                  <Text className={styles.summaryLabel}>Critical</Text>
                </Card>
                <Card className={styles.summaryCard}>
                  <div className={styles.summaryValue} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                    {results.summary.warningFindings}
                  </div>
                  <Text className={styles.summaryLabel}>Warning</Text>
                </Card>
                <Card className={styles.summaryCard}>
                  <div className={styles.summaryValue} style={{ color: tokens.colorPaletteBlueForeground2 }}>
                    {results.summary.informationalFindings}
                  </div>
                  <Text className={styles.summaryLabel}>Info</Text>
                </Card>
                <Card className={styles.summaryCard}>
                  <div className={styles.summaryValue}>{results.summary.totalComponents}</div>
                  <Text className={styles.summaryLabel}>Components</Text>
                </Card>
              </div>
              
              <Divider style={{ marginBlock: tokens.spacingVerticalM }} />
              
              <Accordion collapsible>
                {results.reports.map((report, idx) => (
                  <AccordionItem key={idx} value={`report-${idx}`}>
                    <AccordionHeader>
                      <div className={styles.reportTitle}>
                        {getSeverityIcon(report.severity)}
                        <Text weight="semibold">{report.name}</Text>
                        <Badge color={getSeverityColor(report.severity)} size="small">
                          {report.severity}
                        </Badge>
                        {report.group && (
                          <Badge appearance="outline" size="small">
                            {report.group}
                          </Badge>
                        )}
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                          ({report.totalMatches} matches)
                        </Text>
                      </div>
                    </AccordionHeader>
                    <AccordionPanel>
                      {report.recommendedAction && (
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginBottom: tokens.spacingVerticalS, display: 'block' }}>
                          <strong>Action:</strong> {report.recommendedAction}
                        </Text>
                      )}
                      
                      {report.components && report.components.length > 0 && (
                        <div className={styles.componentList}>
                          {report.components.map((comp, compIdx) => (
                            <div key={compIdx} className={styles.componentItem}>
                              <Text size={200}>
                                {comp.displayName || comp.logicalName || comp.componentId}
                              </Text>
                              <Text size={100} style={{ color: tokens.colorNeutralForeground3, marginLeft: tokens.spacingHorizontalS }}>
                                ({comp.componentTypeName})
                              </Text>
                              {comp.solutions && comp.solutions.length > 0 && (
                                <Text size={100} style={{ color: tokens.colorNeutralForeground3, display: 'block' }}>
                                  Solutions: {comp.solutions.join(', ')}
                                </Text>
                              )}
                              {verbosity !== 'Basic' && comp.layers && comp.layers.length > 0 && (
                                <div style={{ marginTop: tokens.spacingVerticalXXS }}>
                                  <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>
                                    Layers: {comp.layers.map((l) => l.solutionName).join(' → ')}
                                  </Text>
                                  {verbosity === 'Verbose' && comp.layers.some((l) => l.changedAttributes?.length) && (
                                    <Text size={100} style={{ color: tokens.colorNeutralForeground3, display: 'block' }}>
                                      Changed: {comp.layers
                                        .flatMap((l) => (l.changedAttributes || []).map((a) => a.attributeName))
                                        .join(', ')}
                                    </Text>
                                  )}
                                </div>
                              )}
                              {comp.makePortalUrl && (
                                <Button
                                  size="small"
                                  appearance="subtle"
                                  as="a"
                                  href={comp.makePortalUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  style={{ marginTop: tokens.spacingVerticalXS }}
                                >
                                  View in Portal
                                </Button>
                              )}
                            </div>
                          ))}
                        </div>
                      )}
                    </AccordionPanel>
                  </AccordionItem>
                ))}
              </Accordion>
            </>
          )}
        </div>
      )}
    </div>
  );
};
