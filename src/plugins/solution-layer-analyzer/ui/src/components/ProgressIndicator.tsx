import React from 'react';
import { makeStyles, shorthands, tokens, Text, ProgressBar } from '@fluentui/react-components';
import { useAppStore } from '../store/useAppStore';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    ...shorthands.gap('16px'),
    alignItems: 'center',
  },
  operation: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap('8px'),
    minWidth: '300px',
  },
  progressBar: {
    flex: 1,
    minWidth: '150px',
  },
  message: {
    minWidth: '120px',
    fontSize: tokens.fontSizeBase200,
  },
});

export const ProgressIndicator: React.FC = () => {
  const styles = useStyles();
  const { operations } = useAppStore();

  // Don't render if no operations
  if (operations.length === 0) {
    return null;
  }

  return (
    <div className={styles.container}>
      {operations.map((op) => (
        <div key={op.id} className={styles.operation}>
          <Text className={styles.message} size={200}>
            {op.message}
          </Text>
          <ProgressBar
            className={styles.progressBar}
            value={op.percent / 100}
            thickness="medium"
          />
          <Text size={200}>{op.percent}%</Text>
        </div>
      ))}
    </div>
  );
};
