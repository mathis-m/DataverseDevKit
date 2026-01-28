import React from 'react';
import { makeStyles, shorthands, tokens, Text, ProgressBar } from '@fluentui/react-components';
import { useAppStore } from '../store/useAppStore';

const useStyles = makeStyles({
  footer: {
    position: 'fixed',
    bottom: 0,
    right: 0,
    left: 0,
    height: '40px',
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderTop('1px', 'solid', tokens.colorNeutralStroke1),
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'flex-end',
    ...shorthands.padding('0', '16px'),
    zIndex: 1000,
    boxShadow: tokens.shadow8,
  },
  operationsContainer: {
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

export const ProgressFooter: React.FC = () => {
  const styles = useStyles();
  const { operations } = useAppStore();

  // Only show footer if there are active operations
  if (operations.length === 0) {
    return null;
  }

  return (
    <div className={styles.footer}>
      <div className={styles.operationsContainer}>
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
    </div>
  );
};
