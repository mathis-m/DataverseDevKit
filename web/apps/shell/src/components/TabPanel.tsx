import React from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Button,
  Tab,
  TabList,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  Dropdown,
  Option,
} from '@fluentui/react-components';
import {
  DismissRegular,
  MoreHorizontalRegular,
  PlugConnectedRegular,
} from '@fluentui/react-icons';
import { usePluginStore, type TabInstance } from '../stores/plugins';
import { useConnectionStore } from '../stores/connections';
import { PluginLoader } from '../utils/pluginLoader';
import { ConnectionManager } from './ConnectionManager';
import { Marketplace } from './Marketplace';
import { Settings } from './Settings';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    width: '100%',
    ...shorthands.overflow('hidden'),
  },
  tabBar: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
    backgroundColor: tokens.colorNeutralBackground1,
    minHeight: '48px',
    flexShrink: 0,
  },
  tabList: {
    ...shorthands.flex(1),
    minHeight: '48px',
  },
  tabContent: {
    ...shorthands.flex(1),
    ...shorthands.overflow('hidden'),
    position: 'relative',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  tabIcon: {
    fontSize: tokens.fontSizeBase400,
    flexShrink: 0,
  },
  tabTitle: {
    ...shorthands.flex(1),
    minWidth: 0,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  connectionBadge: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    flexShrink: 0,
  },
  closeButton: {
    minWidth: 'auto',
    flexShrink: 0,
  },
});

interface TabItemProps {
  tab: TabInstance;
  isActive: boolean;
  onActivate: () => void;
  onClose: () => void;
  onConnectionChange: (connectionId: string) => void;
}

const TabItem: React.FC<TabItemProps> = React.memo(({
  tab,
  isActive,
  onActivate,
  onClose,
  onConnectionChange,
}) => {
  const styles = useStyles();
  const connections = useConnectionStore((state) => state.connections);
  const currentConnection = tab.connectionId ? connections.find((c) => c.id === tab.connectionId) : null;

  return (
    <Tab value={tab.instanceId}>
      {tab.icon && <span className={styles.tabIcon}>{tab.icon}</span>}
      <span className={styles.tabTitle}>{tab.title}</span>
      {currentConnection && (
        <span className={styles.connectionBadge}>({currentConnection.name})</span>
      )}
      {tab.type === 'plugin' && connections.length > 0 && (
        <Menu>
          <MenuTrigger disableButtonEnhancement>
            <Button
              appearance="subtle"
              icon={<MoreHorizontalRegular />}
              size="small"
              className={styles.closeButton}
              onClick={(e) => e.stopPropagation()}
            />
          </MenuTrigger>
          <MenuPopover>
            <MenuList>
              <MenuItem icon={<PlugConnectedRegular />}>
                <Dropdown
                  placeholder="Change Connection"
                  value={tab.connectionId || ''}
                  onOptionSelect={(_, data) => {
                    onConnectionChange(data.optionValue as string);
                  }}
                  onClick={(e) => e.stopPropagation()}
                >
                  {connections.map((conn) => (
                    <Option key={conn.id} value={conn.id}>
                      {conn.name}
                    </Option>
                  ))}
                </Dropdown>
              </MenuItem>
            </MenuList>
          </MenuPopover>
        </Menu>
      )}
      <Button
        appearance="subtle"
        icon={<DismissRegular />}
        size="small"
        className={styles.closeButton}
        onClick={(e) => {
          e.stopPropagation();
          onClose();
        }}
      />
    </Tab>
  );
});

TabItem.displayName = 'TabItem';

export const TabPanel: React.FC = () => {
  const styles = useStyles();
  const { tabs, activeTabId, setActiveTab, updateTab, removeTab } = usePluginStore();
  const activeTab = tabs.find((t) => t.instanceId === activeTabId);

  const renderTabContent = () => {
    if (!activeTab) return null;

    if (activeTab.type === 'system') {
      switch (activeTab.systemView) {
        case 'connections':
          return <ConnectionManager />;
        case 'marketplace':
          return <Marketplace />;
        case 'settings':
          return <Settings />;
        default:
          return null;
      }
    }

    // Plugin tab
    if (activeTab.remoteEntry && activeTab.scope && activeTab.module) {
      return (
        <PluginLoader
          remoteEntry={activeTab.remoteEntry}
          scope={activeTab.scope}
          module={activeTab.module}
          instanceId={activeTab.instanceId}
          connectionId={activeTab.connectionId}
        />
      );
    }

    return null;
  };

  return (
    <div className={styles.container}>
      {tabs.length > 0 && (
        <div className={styles.tabBar}>
          <TabList
            className={styles.tabList}
            selectedValue={activeTabId || undefined}
            onTabSelect={(_, data) => setActiveTab(data.value as string)}
          >
            {tabs.map((tab) => (
              <TabItem
                key={tab.instanceId}
                tab={tab}
                isActive={tab.instanceId === activeTabId}
                onActivate={() => setActiveTab(tab.instanceId)}
                onClose={() => removeTab(tab.instanceId)}
                onConnectionChange={(connectionId) =>
                  updateTab(tab.instanceId, { connectionId })
                }
              />
            ))}
          </TabList>
        </div>
      )}

      <div className={styles.tabContent}>
        {renderTabContent()}
      </div>
    </div>
  );
};
