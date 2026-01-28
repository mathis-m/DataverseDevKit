import React, { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  makeStyles,
  shorthands,
  tokens,
  Text,
  Badge,
  Divider,
  Spinner,
} from '@fluentui/react-components';
import { FolderOpen24Regular, CheckmarkCircle20Regular } from '@fluentui/react-icons';
import { usePluginApi } from '../hooks/usePluginApi';

const useStyles = makeStyles({
  content: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap('12px'),
    maxHeight: '500px',
    overflowY: 'auto',
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap('8px'),
  },
  sectionTitle: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase300,
  },
  configItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    ...shorthands.padding('8px', '12px'),
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  configItemHighlighted: {
    backgroundColor: tokens.colorBrandBackground2,
    ':hover': {
      backgroundColor: tokens.colorBrandBackground2Hover,
    },
  },
  configInfo: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap('4px'),
    flex: 1,
  },
  configName: {
    fontWeight: tokens.fontWeightSemibold,
  },
  configMeta: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
  },
  badges: {
    display: 'flex',
    ...shorthands.gap('4px'),
  },
  emptyState: {
    textAlign: 'center',
    ...shorthands.padding('24px'),
    color: tokens.colorNeutralForeground2,
  },
  loading: {
    display: 'flex',
    justifyContent: 'center',
    ...shorthands.padding('24px'),
  },
});

interface LoadConfigDialogProps {
  currentConnectionId?: string;
  currentIndexHash?: string;
  onLoadIndex?: (config: any) => void;
  onLoadFilter?: (config: any) => void;
}

export const LoadConfigDialog: React.FC<LoadConfigDialogProps> = ({
  currentConnectionId = "default",
  currentIndexHash,
  onLoadIndex,
  onLoadFilter,
}) => {
  const styles = useStyles();
  const [open, setOpen] = useState(false);
  const [indexConfigs, setIndexConfigs] = useState<any[]>([]);
  const [filterConfigs, setFilterConfigs] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  const { loadIndexConfigs, loadFilterConfigs } = usePluginApi();

  useEffect(() => {
    if (open) {
      loadConfigs();
    }
  }, [open]);

  const loadConfigs = async () => {
    setLoading(true);
    try {
      const [indexRes, filterRes] = await Promise.all([
        loadIndexConfigs({ connectionId: currentConnectionId }),
        loadFilterConfigs({ connectionId: currentConnectionId, currentIndexHash }),
      ]);
      setIndexConfigs(indexRes.configs || []);
      setFilterConfigs(filterRes.configs || []);
    } catch (error) {
      console.error('Error loading configs:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleLoadIndex = (config: any) => {
    onLoadIndex?.(config);
    setOpen(false);
  };

  const handleLoadFilter = (config: any) => {
    onLoadFilter?.(config);
    setOpen(false);
  };

  // Group index configs by environment
  const indexConfigsByEnv = indexConfigs.reduce((acc, config) => {
    const key = config.isSameEnvironment ? 'Same Environment' : `Other (${config.connectionId})`;
    if (!acc[key]) acc[key] = [];
    acc[key].push(config);
    return acc;
  }, {} as Record<string, any[]>);

  // Group filter configs by matching status and environment
  const prioritizedFilters = filterConfigs.filter(c => c.matchesCurrentIndex && c.isSameEnvironment);
  const sameEnvFilters = filterConfigs.filter(c => !c.matchesCurrentIndex && c.isSameEnvironment);
  const otherFilters = filterConfigs.filter(c => !c.isSameEnvironment);

  return (
    <Dialog open={open} onOpenChange={(_, data) => setOpen(data.open)}>
      <DialogTrigger disableButtonEnhancement>
        <Button icon={<FolderOpen24Regular />} appearance="subtle">
          Load Config
        </Button>
      </DialogTrigger>
      <DialogSurface style={{ minWidth: '600px' }}>
        <DialogBody>
          <DialogTitle>Load Configuration</DialogTitle>
          <DialogContent className={styles.content}>
            {loading ? (
              <div className={styles.loading}>
                <Spinner label="Loading configurations..." />
              </div>
            ) : (
              <>
                {/* Index Configs Section */}
                <div className={styles.section}>
                  <Text className={styles.sectionTitle}>Index Configurations</Text>
                  {indexConfigs.length === 0 ? (
                    <div className={styles.emptyState}>
                      <Text>No saved index configurations</Text>
                    </div>
                  ) : (
                    (Object.entries(indexConfigsByEnv) as [string, any[]][]).map(([envName, configs]) => (
                      <div key={envName}>
                        <Text size={200} weight="semibold" style={{ marginBottom: '4px' }}>
                          {envName}
                        </Text>
                        {configs.map((config: any) => (
                          <div
                            key={config.id}
                            className={`${styles.configItem} ${
                              config.isSameEnvironment ? styles.configItemHighlighted : ''
                            }`}
                            onClick={() => handleLoadIndex(config)}
                          >
                            <div className={styles.configInfo}>
                              <Text className={styles.configName}>{config.name}</Text>
                              <Text className={styles.configMeta}>
                                {config.sourceSolutions?.length || 0} source, {config.targetSolutions?.length || 0} target solutions
                              </Text>
                            </div>
                            <div className={styles.badges}>
                              {config.isSameEnvironment && (
                                <Badge appearance="filled" color="success" icon={<CheckmarkCircle20Regular />}>
                                  Same Env
                                </Badge>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    ))
                  )}
                </div>

                <Divider />

                {/* Filter Configs Section */}
                <div className={styles.section}>
                  <Text className={styles.sectionTitle}>Filter Configurations</Text>
                  {filterConfigs.length === 0 ? (
                    <div className={styles.emptyState}>
                      <Text>No saved filter configurations</Text>
                    </div>
                  ) : (
                    <>
                      {prioritizedFilters.length > 0 && (
                        <div>
                          <Text size={200} weight="semibold" style={{ marginBottom: '4px' }}>
                            Matching Current Index
                          </Text>
                          {prioritizedFilters.map((config: any) => (
                            <div
                              key={config.id}
                              className={`${styles.configItem} ${styles.configItemHighlighted}`}
                              onClick={() => handleLoadFilter(config)}
                            >
                              <div className={styles.configInfo}>
                                <Text className={styles.configName}>{config.name}</Text>
                              </div>
                              <div className={styles.badges}>
                                <Badge appearance="filled" color="success" icon={<CheckmarkCircle20Regular />}>
                                  Matches Index
                                </Badge>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}

                      {sameEnvFilters.length > 0 && (
                        <div>
                          <Text size={200} weight="semibold" style={{ marginBottom: '4px' }}>
                            Same Environment
                          </Text>
                          {sameEnvFilters.map((config: any) => (
                            <div
                              key={config.id}
                              className={styles.configItem}
                              onClick={() => handleLoadFilter(config)}
                            >
                              <div className={styles.configInfo}>
                                <Text className={styles.configName}>{config.name}</Text>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}

                      {otherFilters.length > 0 && (
                        <div>
                          <Text size={200} weight="semibold" style={{ marginBottom: '4px' }}>
                            Other Configurations
                          </Text>
                          {otherFilters.map((config: any) => (
                            <div
                              key={config.id}
                              className={styles.configItem}
                              onClick={() => handleLoadFilter(config)}
                            >
                              <div className={styles.configInfo}>
                                <Text className={styles.configName}>{config.name}</Text>
                                <Text className={styles.configMeta}>From {config.connectionId}</Text>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </>
                  )}
                </div>
              </>
            )}
          </DialogContent>
          <DialogActions>
            <DialogTrigger disableButtonEnhancement>
              <Button appearance="secondary">Close</Button>
            </DialogTrigger>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
