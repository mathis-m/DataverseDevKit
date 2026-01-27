import React, { useState } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogActions,
  Button,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  dialogSurface: {
    maxWidth: '95vw',
    maxHeight: '95vh',
    width: '95vw',
    height: '95vh',
  },
  dialogBody: {
    height: 'calc(95vh - 120px)',
    overflow: 'hidden',
    position: 'relative',
  },
  visualizationContainer: {
    width: '100%',
    height: '100%',
    overflow: 'auto',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
  },
});

interface VisualizationModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
}

export const VisualizationModal: React.FC<VisualizationModalProps> = ({
  isOpen,
  onClose,
  title,
  children,
}) => {
  const styles = useStyles();

  return (
    <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface className={styles.dialogSurface}>
        <DialogBody>
          <DialogTitle
            action={
              <Button
                appearance="subtle"
                aria-label="close"
                icon={<Dismiss24Regular />}
                onClick={onClose}
              />
            }
          >
            {title}
          </DialogTitle>
          <div className={styles.dialogBody}>
            <div className={styles.visualizationContainer}>
              {children}
            </div>
          </div>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
