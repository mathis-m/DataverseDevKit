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
import { DragDropProvider, useDraggable, useDroppable } from '@dnd-kit/react';
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
  },
  tabBar: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
    backgroundColor: tokens.colorNeutralBackground1,
    ...shorthands.padding('0', tokens.spacingHorizontalM),
    minHeight: '48px',
  },
  tabList: {
    ...shorthands.flex(1),
    overflowX: 'auto',
    display: 'flex',
    alignItems: 'center',
    minHeight: '48px',
    '&::-webkit-scrollbar': {
      height: '6px',
    },
    '&::-webkit-scrollbar-thumb': {
      backgroundColor: tokens.colorNeutralStroke1,
      ...shorthands.borderRadius(tokens.borderRadiusSmall),
    },
  },
  tabContent: {
    ...shorthands.flex(1),
    ...shorthands.overflow('auto'),
    position: 'relative',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  tabWrapper: {
    display: 'inline-flex',
    maxWidth: '280px',
    minWidth: '120px',
    flexShrink: 0,
  },
  tabItem: {
    width: '100%',
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap(tokens.spacingHorizontalXS),
    ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
    cursor: 'grab',
    minHeight: '48px',
    '&:active': {
      cursor: 'grabbing',
    },
  },
  tabIcon: {
    fontSize: tokens.fontSizeBase400,
    color: tokens.colorNeutralForeground2,
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
    ...shorthands.padding('4px'),
    flexShrink: 0,
  },
});

interface SortableTabProps {
  tab: TabInstance;
  onActivate: () => void;
  onClose: () => void;
  onConnectionChange: (connectionId: string) => void;
}

const SortableTab: React.FC<SortableTabProps> = ({
  tab,
  onActivate,
  onClose,
  onConnectionChange,
}) => {
  const styles = useStyles();
  const connections = useConnectionStore((state) => state.connections);
  const { ref: draggableRef, isDragging } = useDraggable({
    id: tab.instanceId,
    data: { tab },
  });
  const { ref: droppableRef } = useDroppable({
    id: tab.instanceId,
  });

  const style: React.CSSProperties = {
    opacity: isDragging ? 0.5 : 1,
  };

  const currentConnection = tab.connectionId ? connections.find((c) => c.id === tab.connectionId) : null;

  // Combine refs - they are setter functions, not ref objects
  const setRefs = (element: HTMLDivElement | null) => {
    draggableRef(element);
    droppableRef(element);
  };

  return (
    <div ref={setRefs} style={style} className={styles.tabWrapper}>
      <Tab
        value={tab.instanceId}
        className={styles.tabItem}
        onClick={onActivate}
      >
        {tab.icon && <span className={styles.tabIcon}>{tab.icon}</span>}
        <span className={styles.tabTitle}>{tab.title}</span>
        {currentConnection && (
          <span className={styles.connectionBadge}>({currentConnection.name})</span>
        )}
        {tab.type === 'plugin' && (
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
                <MenuItem
                  icon={<PlugConnectedRegular />}
                  onClick={(e) => {
                    e.stopPropagation();
                  }}
                >
                  <Dropdown
                    placeholder="Change Connection"
                    value={tab.connectionId || ''}
                    onOptionSelect={(_, data) => {
                      onConnectionChange(data.optionValue as string);
                    }}
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
    </div>
  );
};

export const TabPanel: React.FC = () => {
  const styles = useStyles();
  const { tabs, activeTabId, setActiveTab, updateTab, removeTab } = usePluginStore();
  const [tabOrder, setTabOrder] = React.useState<string[]>([]);

  React.useEffect(() => {
    setTabOrder(tabs.map((t) => t.instanceId));
  }, [tabs]);

  const handleDragEnd = (event: any) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      const oldIndex = tabOrder.indexOf(active.id as string);
      const newIndex = tabOrder.indexOf(over.id as string);
      const newOrder = [...tabOrder];
      newOrder.splice(oldIndex, 1);
      newOrder.splice(newIndex, 0, active.id as string);
      setTabOrder(newOrder);
    }
  };

  const sortedTabs = tabOrder
    .map((id) => tabs.find((t) => t.instanceId === id))
    .filter(Boolean) as TabInstance[];

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
          <DragDropProvider onDragEnd={handleDragEnd}>
            <TabList className={styles.tabList} selectedValue={activeTabId || undefined}>
              {sortedTabs.map((tab) => (
                <SortableTab
                  key={tab.instanceId}
                  tab={tab}
                  onActivate={() => setActiveTab(tab.instanceId)}
                  onClose={() => removeTab(tab.instanceId)}
                  onConnectionChange={(connectionId) =>
                    updateTab(tab.instanceId, { connectionId })
                  }
                />
              ))}
            </TabList>
          </DragDropProvider>
        </div>
      )}

      <div className={styles.tabContent}>
        {renderTabContent()}
      </div>
    </div>
  );
};
