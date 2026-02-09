import React, { useEffect, useState } from "react";
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
  Badge,
  Spinner,
} from "@fluentui/react-components";
import {
  PlugConnectedRegular,
  PlugDisconnectedRegular,
  AddRegular,
  MoreVerticalRegular,
  CheckmarkRegular,
  PersonRegular,
  SignOutRegular,
} from "@fluentui/react-icons";
import { hostBridge } from "@ddk/host-sdk";
import { useConnectionStore } from "../stores/connections";
import { AddConnectionDialog } from "./AddConnectionDialog";

const useStyles = makeStyles({
  container: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap(tokens.spacingVerticalM),
    ...shorthands.padding(tokens.spacingVerticalXL),
    height: "100%",
    maxWidth: "1200px",
    margin: "0 auto",
    width: "100%",
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
  },
  connectionList: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap(tokens.spacingVerticalS),
    overflowY: "auto",
    ...shorthands.flex(1),
  },
  connectionCard: {
    cursor: "pointer",
    ...shorthands.transition("all", "0.2s"),
    "&:hover": {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  activeCard: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  connectionHeader: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
    width: "100%",
  },
  connectionInfo: {
    display: "flex",
    flexDirection: "column",
    ...shorthands.gap(tokens.spacingVerticalXS),
  },
  connectionUrl: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  authInfo: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap(tokens.spacingHorizontalS),
    marginTop: tokens.spacingVerticalXS,
  },
  userBadge: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap(tokens.spacingHorizontalXS),
  },
  actionButtons: {
    display: "flex",
    alignItems: "center",
    ...shorthands.gap(tokens.spacingHorizontalS),
  },
});

export const ConnectionManager: React.FC = () => {
  const styles = useStyles();
  const {
    connections,
    activeConnectionId,
    setConnections,
    setActiveConnection,
    removeConnection,
  } = useConnectionStore();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [loggingIn, setLoggingIn] = useState<string | null>(null);

  const loadConnections = async () => {
    try {
      const conns = await hostBridge.listConnections();
      setConnections(conns);
      const active = conns.find((c) => c.isActive);
      if (active) {
        setActiveConnection(active.id);
      }
    } catch (error) {
      console.error("Failed to load connections:", error);
    }
  };

  useEffect(() => {
    loadConnections();
  }, []);


  const handleActivate = async (id: string) => {
    try {
      await hostBridge.activateConnection(id);
      setActiveConnection(id);
    } catch (error) {
      console.error("Failed to activate connection:", error);
    }
  };

  const handleRemove = async (id: string) => {
    try {
      await hostBridge.removeConnection(id);
      removeConnection(id);
    } catch (error) {
      console.error("Failed to remove connection:", error);
    }
  };

  const handleLogin = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setLoggingIn(id);
    try {
      const result = await hostBridge.login(id);
      if (result.success) {
        // Refresh connections to get updated auth state
        await loadConnections();
      } else {
        console.error("Login failed:", result.error);
      }
    } catch (error) {
      console.error("Failed to login:", error);
    } finally {
      setLoggingIn(null);
    }
  };

  const handleLogout = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await hostBridge.logout();
      // Refresh connections to get updated auth state
      await loadConnections();
    } catch (error) {
      console.error("Failed to logout:", error);
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
            className={`${styles.connectionCard} ${activeConnectionId === connection.id ? styles.activeCard : ""}`}
            onClick={() => handleActivate(connection.id)}
          >
            <CardHeader
              image={
                connection.isAuthenticated ? (
                  <PlugConnectedRegular />
                ) : (
                  <PlugDisconnectedRegular />
                )
              }
              header={
                <div className={styles.connectionHeader}>
                  <div className={styles.connectionInfo}>
                    <Text weight="semibold">{connection.name}</Text>
                    <Text className={styles.connectionUrl}>
                      {connection.url}
                    </Text>
                    <div className={styles.authInfo}>
                      {connection.isAuthenticated ? (
                        <div className={styles.userBadge}>
                          <Badge
                            appearance="filled"
                            color="success"
                            size="small"
                          >
                            <PersonRegular style={{ marginRight: 4 }} />
                            {connection.authenticatedUser || "Authenticated"}
                          </Badge>
                        </div>
                      ) : (
                        <Badge appearance="ghost" color="warning" size="small">
                          Not authenticated
                        </Badge>
                      )}
                    </div>
                  </div>
                  <div className={styles.actionButtons}>
                    {activeConnectionId === connection.id && (
                      <CheckmarkRegular />
                    )}
                  </div>
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
                      {!connection.isAuthenticated ? (
                        <MenuItem
                          icon={
                            loggingIn === connection.id ? (
                              <Spinner size="tiny" />
                            ) : (
                              <PersonRegular />
                            )
                          }
                          onClick={(e) => handleLogin(connection.id, e)}
                          disabled={loggingIn === connection.id}
                        >
                          {loggingIn === connection.id
                            ? "Signing in..."
                            : "Sign in"}
                        </MenuItem>
                      ) : (
                        <MenuItem
                          icon={<SignOutRegular />}
                          onClick={handleLogout}
                        >
                          Sign out
                        </MenuItem>
                      )}
                      <MenuItem
                        onClick={(e) => {
                          e.stopPropagation();
                          handleRemove(connection.id);
                        }}
                      >
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

      <AddConnectionDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
      />
    </div>
  );
};
