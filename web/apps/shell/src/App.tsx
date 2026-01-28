import React, { useEffect, useState } from 'react';
import {
  FluentProvider,
  webDarkTheme,
  webLightTheme,
  makeStyles,
  tokens,
  shorthands,
  Button,
  Tooltip,
} from '@fluentui/react-components';
import {
  AppsRegular,
  PlugConnectedRegular,
  SettingsRegular,
} from '@fluentui/react-icons';
import { useSettingsStore } from './stores/settings';
import { usePluginStore } from './stores/plugins';
import { TabPanel } from './components/TabPanel';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    height: '100vh',
    width: '100vw',
    backgroundColor: tokens.colorNeutralBackground3,
  },
  sidebar: {
    width: '56px',
    backgroundColor: tokens.colorNeutralBackground2,
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.borderRight('1px', 'solid', tokens.colorNeutralStroke1),
  },
  sidebarNav: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
    ...shorthands.padding(tokens.spacingVerticalS),
    ...shorthands.flex(1),
  },
  navButton: {
    justifyContent: 'center',
    minWidth: '40px',
    width: '40px',
    height: '40px',
  },
  mainContent: {
    ...shorthands.flex(1),
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.overflow('hidden'),
  },
  sidebarFooter: {
    ...shorthands.padding(tokens.spacingVerticalS),
    ...shorthands.borderTop('1px', 'solid', tokens.colorNeutralStroke1),
  },
});

const App: React.FC = () => {
  const styles = useStyles();
  const { settings } = useSettingsStore();
  const { openSystemView, tabs } = usePluginStore();
  const [theme, setTheme] = useState(webDarkTheme);

  // Open marketplace tab on initial load
  useEffect(() => {
    if (tabs.length === 0) {
      openSystemView('marketplace', 'Marketplace', <AppsRegular />);
    }
  }, []);  // Run only once on mount

  useEffect(() => {
    // Determine theme based on settings
    let selectedTheme = webDarkTheme;
    if (settings.theme === 'light') {
      selectedTheme = webLightTheme;
    } else if (settings.theme === 'system') {
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      selectedTheme = prefersDark ? webDarkTheme : webLightTheme;
    }
    setTheme(selectedTheme);
  }, [settings.theme]);

  useEffect(() => {
    // Listen for system theme changes
    if (settings.theme === 'system') {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => {
        setTheme(e.matches ? webDarkTheme : webLightTheme);
      };
      mediaQuery.addEventListener('change', handler);
      return () => mediaQuery.removeEventListener('change', handler);
    }
  }, [settings.theme]);

  return (
    <FluentProvider theme={theme}>
      <div className={styles.root}>
        <div className={styles.sidebar}>
          <div className={styles.sidebarNav}>
            <Tooltip content="Connections" relationship="label" positioning="after">
              <Button
                appearance="subtle"
                icon={<PlugConnectedRegular />}
                className={styles.navButton}
                onClick={() => openSystemView('connections', 'Connections', <PlugConnectedRegular />)}
              />
            </Tooltip>

            <Tooltip content="Marketplace" relationship="label" positioning="after">
              <Button
                appearance="subtle"
                icon={<AppsRegular />}
                className={styles.navButton}
                onClick={() => openSystemView('marketplace', 'Marketplace', <AppsRegular />)}
              />
            </Tooltip>
          </div>

          <div className={styles.sidebarFooter}>
            <Tooltip content="Settings" relationship="label" positioning="after">
              <Button
                appearance="subtle"
                icon={<SettingsRegular />}
                className={styles.navButton}
                onClick={() => openSystemView('settings', 'Settings', <SettingsRegular />)}
              />
            </Tooltip>
          </div>
        </div>

        <div className={styles.mainContent}>
          <TabPanel />
        </div>
      </div>
    </FluentProvider>
  );
};

export default App;
