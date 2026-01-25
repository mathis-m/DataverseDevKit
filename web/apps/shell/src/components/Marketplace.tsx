import React, { useEffect, useState } from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Input,
  Button,
  Card,
  CardPreview,
  Text,
  Badge,
  Dropdown,
  Option,
} from '@fluentui/react-components';
import {
  SearchRegular,
  AppsRegular,
  PlayRegular,
  PersonRegular,
  BuildingRegular,
} from '@fluentui/react-icons';
import { hostBridge, type PluginMetadata } from '@ddk/host-sdk';
import { usePluginStore } from '../stores/plugins';
import { useConnectionStore } from '../stores/connections';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalM),
    ...shorthands.padding(tokens.spacingVerticalXL),
    height: '100%',
    maxWidth: '1600px',
    margin: '0 auto',
    width: '100%',
  },
  header: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
  },
  searchRow: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalM),
    alignItems: 'center',
  },
  searchInput: {
    ...shorthands.flex(1),
  },
  filters: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalM),
    flexWrap: 'wrap',
  },
  pluginGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
    gap: tokens.spacingVerticalM,
    overflowY: 'auto',
    ...shorthands.flex(1),
  },
  pluginCard: {
    cursor: 'pointer',
    height: '220px',
    ...shorthands.transition('all', '0.2s'),
    '&:hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  pluginIcon: {
    width: '64px',
    height: '64px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '32px',
    backgroundColor: tokens.colorBrandBackground2,
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
  },
  pluginInfo: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalXS),
    ...shorthands.padding(tokens.spacingVerticalM),
  },
  pluginMeta: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalXS),
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  description: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    display: '-webkit-box',
    WebkitLineClamp: '2',
    WebkitBoxOrient: 'vertical',
  },
});

export const Marketplace: React.FC = () => {
  const styles = useStyles();
  const { availablePlugins, setAvailablePlugins, addTab } = usePluginStore();
  const activeConnectionId = useConnectionStore((state) => state.activeConnectionId);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [companyFilter, setCompanyFilter] = useState<string>('all');

  useEffect(() => {
    loadPlugins();
  }, []);

  const loadPlugins = async () => {
    try {
      const plugins = await hostBridge.listPlugins();
      setAvailablePlugins(plugins);
    } catch (error) {
      console.error('Failed to load plugins:', error);
    }
  };

  const handleLaunchPlugin = async (plugin: PluginMetadata) => {
    try {
      const instanceId = `${plugin.id}-${Date.now()}`;
      addTab({
        instanceId,
        type: 'plugin',
        pluginId: plugin.id,
        title: plugin.name,
        connectionId: activeConnectionId || null,
        remoteEntry: plugin.uiEntry,
        scope: plugin.uiScope || 'unknownPlugin',
        module: plugin.uiModule || './Plugin',
      });
    } catch (error) {
      console.error('Failed to launch plugin:', error);
    }
  };

  const categories = ['all', ...new Set(availablePlugins.map((p) => p.category))];
  const companies = ['all', ...new Set(availablePlugins.map((p) => p.company).filter(Boolean) as string[])];

  const filteredPlugins = availablePlugins.filter((plugin) => {
    const matchesSearch =
      searchTerm === '' ||
      plugin.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      plugin.description.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = categoryFilter === 'all' || plugin.category === categoryFilter;
    const matchesCompany = companyFilter === 'all' || plugin.company === companyFilter;
    return matchesSearch && matchesCategory && matchesCompany;
  });

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          Plugin Marketplace
        </Text>

        <div className={styles.searchRow}>
          <Input
            className={styles.searchInput}
            contentBefore={<SearchRegular />}
            placeholder="Search plugins..."
            value={searchTerm}
            onChange={(_, data) => setSearchTerm(data.value)}
          />
        </div>

        <div className={styles.filters}>
          <Dropdown
            placeholder="Category"
            value={categoryFilter}
            onOptionSelect={(_, data) => setCategoryFilter(data.optionValue as string)}
          >
            {categories.map((cat) => (
              <Option key={cat} value={cat}>
                {cat.charAt(0).toUpperCase() + cat.slice(1)}
              </Option>
            ))}
          </Dropdown>

          <Dropdown
            placeholder="Company"
            value={companyFilter}
            onOptionSelect={(_, data) => setCompanyFilter(data.optionValue as string)}
          >
            {companies.map((company) => (
              <Option key={company} value={company}>
                {company.charAt(0).toUpperCase() + company.slice(1)}
              </Option>
            ))}
          </Dropdown>
        </div>
      </div>

      <div className={styles.pluginGrid}>
        {filteredPlugins.map((plugin) => (
          <Card key={plugin.id} className={styles.pluginCard}>
            <CardPreview>
              <div className={styles.pluginIcon}>
                {plugin.icon || <AppsRegular />}
              </div>
            </CardPreview>

            <div className={styles.pluginInfo}>
              <Text weight="semibold" size={400}>
                {plugin.name}
              </Text>

              <div className={styles.pluginMeta}>
                <Badge size="small" appearance="outline" icon={<AppsRegular />}>
                  {plugin.category}
                </Badge>
                {plugin.company && (
                  <Badge size="small" appearance="outline" icon={<BuildingRegular />}>
                    {plugin.company}
                  </Badge>
                )}
                <Badge size="small" appearance="outline" icon={<PersonRegular />}>
                  {plugin.author}
                </Badge>
              </div>

              <Text className={styles.description}>{plugin.description}</Text>

              <Button
                appearance="primary"
                size="small"
                icon={<PlayRegular />}
                onClick={() => handleLaunchPlugin(plugin)}
              >
                Launch
              </Button>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
};
