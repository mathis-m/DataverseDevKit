import React, { useEffect, useState, useCallback } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Spinner,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Warning24Regular } from '@fluentui/react-icons';
import { hostBridge, type SessionExpiredPayload } from '@ddk/host-sdk';

const useStyles = makeStyles({
  warningIcon: {
    color: tokens.colorPaletteYellowForeground1,
    marginRight: tokens.spacingHorizontalS,
  },
  titleContainer: {
    display: 'flex',
    alignItems: 'center',
  },
  content: {
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalM,
  },
});

interface SessionExpiredInfo extends SessionExpiredPayload {
  timestamp: number;
}

/**
 * Global dialog component that listens for session:expired events
 * and prompts the user to reauthenticate.
 */
export const SessionExpiredDialog: React.FC = () => {
  const styles = useStyles();
  const [sessionExpired, setSessionExpired] = useState<SessionExpiredInfo | null>(null);
  const [isReauthenticating, setIsReauthenticating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Subscribe to session expired events
    const unsubscribe = hostBridge.onSessionExpired((payload) => {
      console.log('[SessionExpiredDialog] Received session:expired event', payload);
      setSessionExpired({
        ...payload,
        timestamp: Date.now(),
      });
      setError(null);
    });

    return () => {
      unsubscribe();
    };
  }, []);

  const handleReauthenticate = useCallback(async () => {
    if (!sessionExpired) return;

    setIsReauthenticating(true);
    setError(null);

    try {
      const result = await hostBridge.reauthenticate(sessionExpired.connectionId);
      
      if (result.success) {
        console.log('[SessionExpiredDialog] Reauthentication successful');
        setSessionExpired(null);
      } else {
        setError(result.error || 'Authentication failed');
      }
    } catch (err) {
      console.error('[SessionExpiredDialog] Reauthentication error:', err);
      setError(err instanceof Error ? err.message : 'An error occurred during authentication');
    } finally {
      setIsReauthenticating(false);
    }
  }, [sessionExpired]);

  const handleDismiss = useCallback(() => {
    setSessionExpired(null);
    setError(null);
  }, []);

  if (!sessionExpired) {
    return null;
  }

  return (
    <Dialog open={true} onOpenChange={(_, data) => !data.open && handleDismiss()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>
            <div className={styles.titleContainer}>
              <Warning24Regular className={styles.warningIcon} />
              Session Expired
            </div>
          </DialogTitle>
          <DialogContent className={styles.content}>
            <p>
              Your session for <strong>{sessionExpired.connectionName || sessionExpired.connectionId}</strong> has expired.
            </p>
            <p>Please reauthenticate to continue using this connection.</p>
            {error && (
              <p style={{ color: tokens.colorPaletteRedForeground1, marginTop: tokens.spacingVerticalS }}>
                {error}
              </p>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={handleDismiss} disabled={isReauthenticating}>
              Dismiss
            </Button>
            <Button appearance="primary" onClick={handleReauthenticate} disabled={isReauthenticating}>
              {isReauthenticating ? (
                <>
                  <Spinner size="tiny" style={{ marginRight: tokens.spacingHorizontalS }} />
                  Authenticating...
                </>
              ) : (
                'Reauthenticate'
              )}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
