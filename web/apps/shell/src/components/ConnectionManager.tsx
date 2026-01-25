import React, { useEffect, useState } from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Button,
  Card,
  CardHeader,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  Text,
} from '@fluentui/react-components';
import {
  PlugConnectedRegular,
  PlugDisconnectedRegular,
  AddRegular,
  MoreVerticalRegular,
  CheckmarkRegular,
} from '@fluentui/react-icons';
import { hostBridge } from '@ddk/host-sdk';
import { useConnectionStore } from '../stores/connections';
import { AddConnectionDialog } from './AddConnectionDialog';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalM),
    ...shorthands.padding(tokens.spacingVerticalXL),
    height: '100%',
    maxWidth: '1200px',
    margin: '0 auto',
    width: '100%',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  connectionList: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
    overflowY: 'auto',
    ...shorthands.flex(1),
  },
  connectionCard: {
    cursor: 'pointer',
    ...shorthands.transition('all', '0.2s'),
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  activeCard: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  connectionHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  connectionInfo: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalXS),
  },
  connectionUrl: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
});

export const ConnectionManager: React.FC = () => {
  const styles = useStyles();
  const { connections, activeConnectionId, setConnections, setActiveConnection, removeConnection } =
    useConnectionStore();
  const [dialogOpen, setDialogOpen] = useState(false);

  useEffect(() => {
    loadConnections();
  }, []);

  const loadConnections = async () => {
    try {
      const conns = await hostBridge.listConnections();
      setConnections(conns);
      const active = conns.find((c) => c.isActive);
      if (active) {
        setActiveConnection(active.id);
      }
    } catch (error) {
      console.error('Failed to load connections:', error);
    }
  };

  const handleActivate = async (id: string) => {
    try {
      await hostBridge.activateConnection(id);
      setActiveConnection(id);
    } catch (error) {
      console.error('Failed to activate connection:', error);
    }
  };

  const handleRemove = async (id: string) => {
    try {
      await hostBridge.removeConnection(id);
      removeConnection(id);
    } catch (error) {
      console.error('Failed to remove connection:', error);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          Connections
        </Text>
        <Button
          appearance="primary"
          icon={<AddRegular />}
          size="small"
          onClick={() => setDialogOpen(true)}
        >
          Add
        </Button>
      </div>

      <div className={styles.connectionList}>
        {connections.map((connection) => (
          <Card
            key={connection.id}
            className={`${styles.connectionCard} ${activeConnectionId === connection.id ? styles.activeCard : ''}`}
            onClick={() => handleActivate(connection.id)}
          >
            <CardHeader
              image={
                connection.isActive ? <PlugConnectedRegular /> : <PlugDisconnectedRegular />
              }
              header={
                <div className={styles.connectionHeader}>
                  <div className={styles.connectionInfo}>
                    <Text weight="semibold">{connection.name}</Text>
                    <Text className={styles.connectionUrl}>{connection.url}</Text>
                  </div>
                  {activeConnectionId === connection.id && <CheckmarkRegular />}
                </div>
              }
              action={
                <Menu>
                  <MenuTrigger disableButtonEnhancement>
                    <Button
                      appearance="subtle"
                      icon={<MoreVerticalRegular />}
                      size="small"
                      onClick={(e) => e.stopPropagation()}
                    />
                  </MenuTrigger>

                  <MenuPopover>
                    <MenuList>
                      <MenuItem onClick={(e) => { e.stopPropagation(); handleRemove(connection.id); }}>
                        Remove
                      </MenuItem>
                    </MenuList>
                  </MenuPopover>
                </Menu>
              }
            />
          </Card>
        ))}
      </div>

      <AddConnectionDialog open={dialogOpen} onClose={() => setDialogOpen(false)} />
    </div>
  );
};
