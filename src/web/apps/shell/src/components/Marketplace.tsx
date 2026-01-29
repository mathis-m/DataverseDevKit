import React, { useState, useMemo } from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Input,
  Text,
  Dropdown,
  Option,
  Spinner,
} from '@fluentui/react-components';
import { SearchRegular } from '@fluentui/react-icons';
import type { PluginMetadata } from '@ddk/host-sdk';
import { usePluginStore } from '../stores/plugins';
import { useConnectionStore } from '../stores/connections';
import { usePlugins } from '../hooks/usePlugins';
import { PluginCard } from './PluginCard';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalM),
    ...shorthands.padding(tokens.spacingVerticalL),
    height: '100%',
    maxWidth: '1400px',
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
    maxWidth: '400px',
  },
  filters: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalM),
    flexWrap: 'wrap',
  },
  pluginGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
    gap: tokens.spacingVerticalL,
    ...shorthands.padding(tokens.spacingVerticalS, 0),
  },
  loading: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    ...shorthands.flex(1),
  },
  empty: {
    textAlign: 'center',
    ...shorthands.padding(tokens.spacingVerticalXXXL),
    color: tokens.colorNeutralForeground3,
  },
});

export const Marketplace: React.FC = () => {
  const styles = useStyles();
  const { addTab } = usePluginStore();
  const activeConnectionId = useConnectionStore((state) => state.activeConnectionId);
  const { availablePlugins, loading, error } = usePlugins();
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [companyFilter, setCompanyFilter] = useState<string>('all');

  const handleLaunchPlugin = (plugin: PluginMetadata) => {
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
  };

  const categories = useMemo(
    () => ['all', ...new Set(availablePlugins.map((p) => p.category))],
    [availablePlugins]
  );

  const companies = useMemo(
    () => ['all', ...new Set(availablePlugins.map((p) => p.company).filter(Boolean) as string[])],
    [availablePlugins]
  );

  const filteredPlugins = useMemo(() => {
    return availablePlugins.filter((plugin) => {
      const matchesSearch =
        searchTerm === '' ||
        plugin.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        plugin.description.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesCategory = categoryFilter === 'all' || plugin.category === categoryFilter;
      const matchesCompany = companyFilter === 'all' || plugin.company === companyFilter;
      return matchesSearch && matchesCategory && matchesCompany;
    });
  }, [availablePlugins, searchTerm, categoryFilter, companyFilter]);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <Spinner label="Loading plugins..." />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <Text size={400}>Error loading plugins: {error}</Text>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={600} weight="semibold">
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

      {filteredPlugins.length === 0 ? (
        <div className={styles.empty}>
          <Text size={400}>No plugins found matching your filters.</Text>
        </div>
      ) : (
        <div className={styles.pluginGrid}>
          {filteredPlugins.map((plugin) => (
            <PluginCard key={plugin.id} plugin={plugin} onLaunch={handleLaunchPlugin} />
          ))}
        </div>
      )}
    </div>
  );
};
