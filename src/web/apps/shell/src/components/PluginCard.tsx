import React from 'react';
import {
  Card,
  CardHeader,
  CardPreview,
  Button,
  Text,
  Badge,
  makeStyles,
  tokens,
  shorthands,
} from '@fluentui/react-components';
import {
  AppsRegular,
  PlayRegular,
  PersonRegular,
  BuildingRegular,
} from '@fluentui/react-icons';
import type { PluginMetadata } from '@ddk/host-sdk';

const useStyles = makeStyles({
  card: {
    width: '100%',
    height: 'auto',
    minHeight: '200px',
  },
  header: {
    ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
  },
  preview: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '80px',
    backgroundColor: tokens.colorNeutralBackground3,
  },
  icon: {
    fontSize: '48px',
    color: tokens.colorBrandForeground1,
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
    ...shorthands.padding(tokens.spacingVerticalM, tokens.spacingHorizontalM),
  },
  badges: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalXS),
    flexWrap: 'wrap',
  },
  description: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    display: '-webkit-box',
    WebkitLineClamp: '2',
    WebkitBoxOrient: 'vertical',
    minHeight: '40px',
  },
});

interface PluginCardProps {
  plugin: PluginMetadata;
  onLaunch: (plugin: PluginMetadata) => void;
}

export const PluginCard: React.FC<PluginCardProps> = React.memo(({ plugin, onLaunch }) => {
  const styles = useStyles();

  return (
    <Card className={styles.card}>
      <CardPreview className={styles.preview}>
        <div className={styles.icon}>
          {plugin.icon || <AppsRegular />}
        </div>
      </CardPreview>

      <CardHeader
        header={<Text weight="semibold">{plugin.name}</Text>}
        className={styles.header}
      />

      <div className={styles.content}>
        <div className={styles.badges}>
          <Badge size="medium" appearance="outline" icon={<AppsRegular />}>
            {plugin.category}
          </Badge>
          {plugin.company && (
            <Badge size="medium" appearance="outline" icon={<BuildingRegular />}>
              {plugin.company}
            </Badge>
          )}
          <Badge size="medium" appearance="outline" icon={<PersonRegular />}>
            {plugin.author}
          </Badge>
        </div>

        <Text className={styles.description}>{plugin.description}</Text>

        <Button
          appearance="primary"
          icon={<PlayRegular />}
          onClick={() => onLaunch(plugin)}
        >
          Launch
        </Button>
      </div>
    </Card>
  );
});

PluginCard.displayName = 'PluginCard';
