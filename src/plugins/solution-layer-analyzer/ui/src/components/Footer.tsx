import React from 'react';
import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  footer: {
    height: '40px',
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderTop('1px', 'solid', tokens.colorNeutralStroke1),
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    ...shorthands.padding('0', '16px'),
    boxShadow: tokens.shadow8,
  },
  leftSection: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap('16px'),
  },
  rightSection: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap('16px'),
  },
});

interface FooterProps {
  leftContent?: React.ReactNode;
  rightContent?: React.ReactNode;
}

export const Footer: React.FC<FooterProps> = ({ leftContent, rightContent }) => {
  const styles = useStyles();

  // Only show footer if there's content
  if (!leftContent && !rightContent) {
    return null;
  }

  return (
    <div className={styles.footer}>
      <div className={styles.leftSection}>{leftContent}</div>
      <div className={styles.rightSection}>{rightContent}</div>
    </div>
  );
};

