import React from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Text,
  Switch,
  Dropdown,
  Option,
} from '@fluentui/react-components';
import { useSettingsStore, type Theme } from '../stores/settings';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalL),
    ...shorthands.padding(tokens.spacingVerticalXL),
    maxWidth: '800px',
    margin: '0 auto',
    width: '100%',
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalM),
  },
  sectionTitle: {
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: tokens.spacingVerticalS,
  },
  settingRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    ...shorthands.padding(tokens.spacingVerticalM, 0),
  },
  settingInfo: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalXS),
  },
  settingDescription: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
});

export const Settings: React.FC = () => {
  const styles = useStyles();
  const { settings, updateSettings } = useSettingsStore();

  return (
    <div className={styles.container}>
      <Text size={600} weight="bold">
        Settings
      </Text>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>Appearance</div>

        <div className={styles.settingRow}>
          <div className={styles.settingInfo}>
            <Text weight="semibold">Theme</Text>
            <Text className={styles.settingDescription}>Choose your preferred color theme</Text>
          </div>
          <Dropdown
            value={settings.theme}
            onOptionSelect={(_, data) => updateSettings({ theme: data.optionValue as Theme })}
          >
            <Option value="light">Light</Option>
            <Option value="dark">Dark</Option>
            <Option value="system">System</Option>
          </Dropdown>
        </div>

        <div className={styles.settingRow}>
          <div className={styles.settingInfo}>
            <Text weight="semibold">Collapse Sidebar</Text>
            <Text className={styles.settingDescription}>
              Minimize the sidebar to save screen space
            </Text>
          </div>
          <Switch
            checked={settings.sidebarCollapsed}
            onChange={(_, data) => updateSettings({ sidebarCollapsed: data.checked })}
          />
        </div>
      </div>
    </div>
  );
};
