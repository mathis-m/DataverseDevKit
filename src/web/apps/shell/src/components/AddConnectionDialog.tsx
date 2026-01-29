import React, { useState } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Input,
  Field,
  Dropdown,
  Option,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { hostBridge } from '@ddk/host-sdk';
import { useConnectionStore } from '../stores/connections';

const useStyles = makeStyles({
  field: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
});

interface AddConnectionDialogProps {
  open: boolean;
  onClose: () => void;
}

export const AddConnectionDialog: React.FC<AddConnectionDialogProps> = ({ open, onClose }) => {
  const styles = useStyles();
  const addConnection = useConnectionStore((state) => state.addConnection);
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [authType, setAuthType] = useState<'OAuth' | 'ClientSecret'>('OAuth');
  const [clientId, setClientId] = useState('');
  const [clientSecret, setClientSecret] = useState('');
  const [tenantId, setTenantId] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    setLoading(true);
    try {
      const connection = await hostBridge.addConnection({
        name,
        url,
        authType,
        clientId: clientId || undefined,
        clientSecret: clientSecret || undefined,
        tenantId: tenantId || undefined,
      });
      addConnection(connection);
      onClose();
      resetForm();
    } catch (error) {
      console.error('Failed to add connection:', error);
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setName('');
    setUrl('');
    setAuthType('OAuth');
    setClientId('');
    setClientSecret('');
    setTenantId('');
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Add Connection</DialogTitle>
          <DialogContent>
            <div className={styles.field}>
              <Field label="Connection Name" required>
                <Input value={name} onChange={(_, data) => setName(data.value)} />
              </Field>

              <Field label="Environment URL" required>
                <Input
                  value={url}
                  onChange={(_, data) => setUrl(data.value)}
                  placeholder="https://yourorg.crm.dynamics.com"
                />
              </Field>

              <Field label="Authentication Type">
                <Dropdown
                  value={authType}
                  onOptionSelect={(_, data) =>
                    setAuthType(data.optionValue as 'OAuth' | 'ClientSecret')
                  }
                >
                  <Option value="OAuth">OAuth</Option>
                  <Option value="ClientSecret">Client Secret</Option>
                </Dropdown>
              </Field>

              {authType === 'ClientSecret' && (
                <>
                  <Field label="Client ID">
                    <Input value={clientId} onChange={(_, data) => setClientId(data.value)} />
                  </Field>

                  <Field label="Client Secret">
                    <Input
                      type="password"
                      value={clientSecret}
                      onChange={(_, data) => setClientSecret(data.value)}
                    />
                  </Field>

                  <Field label="Tenant ID">
                    <Input value={tenantId} onChange={(_, data) => setTenantId(data.value)} />
                  </Field>
                </>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose} disabled={loading}>
              Cancel
            </Button>
            <Button appearance="primary" onClick={handleSubmit} disabled={loading || !name || !url}>
              Add Connection
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
