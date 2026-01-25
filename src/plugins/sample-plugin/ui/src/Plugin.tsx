import React, { useState, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Text,
  Button,
  Input,
  Field,
  Badge,
  Spinner,
  Divider,
} from '@fluentui/react-components';
import {
  SendRegular,
  InfoRegular,
  CheckmarkCircleRegular,
  DismissCircleRegular,
  ArrowSyncRegular,
} from '@fluentui/react-icons';
import { hostBridge } from '@ddk/host-sdk';

const PLUGIN_ID = 'com.ddk.sample';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    height: '100%',
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  title: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  subtitle: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  commandCard: {
    padding: tokens.spacingVerticalL,
  },
  commandRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'flex-end',
    marginTop: tokens.spacingVerticalM,
  },
  resultContainer: {
    marginTop: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  resultLabel: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalXS,
  },
  resultContent: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase300,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word',
  },
  errorContent: {
    color: tokens.colorPaletteRedForeground1,
  },
  statusBadge: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
  infoGrid: {
    display: 'grid',
    gridTemplateColumns: 'auto 1fr',
    gap: tokens.spacingHorizontalM,
    alignItems: 'baseline',
  },
  infoLabel: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
  },
  infoValue: {
    fontFamily: 'monospace',
  },
});

interface PluginProps {
  instanceId: string;
}

interface CommandResult {
  success: boolean;
  data?: unknown;
  error?: string;
  timestamp: Date;
}

/**
 * Sample Plugin UI Component
 * 
 * Demonstrates how to:
 * 1. Call backend plugin commands via hostBridge.invokePluginCommand()
 * 2. Handle command responses and errors
 * 3. Display results to the user
 * 
 * The hostBridge SDK handles all communication with the MAUI host application,
 * which routes the commands to the appropriate plugin backend via gRPC.
 */
const Plugin: React.FC<PluginProps> = ({ instanceId }) => {
  const styles = useStyles();
  
  // State for each command demonstration
  const [pingResult, setPingResult] = useState<CommandResult | null>(null);
  const [pingLoading, setPingLoading] = useState(false);
  
  const [echoInput, setEchoInput] = useState('Hello from the UI!');
  const [echoResult, setEchoResult] = useState<CommandResult | null>(null);
  const [echoLoading, setEchoLoading] = useState(false);
  
  const [infoResult, setInfoResult] = useState<CommandResult | null>(null);
  const [infoLoading, setInfoLoading] = useState(false);

  /**
   * Execute the 'ping' command.
   * This is the simplest command - no input required, just returns a response.
   */
  const handlePing = useCallback(async () => {
    setPingLoading(true);
    try {
      // invokePluginCommand(pluginId, commandName, payload?)
      // Result is already an object, no JSON.parse needed
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'ping');
      setPingResult({
        success: true,
        data: result,
        timestamp: new Date(),
      });
    } catch (error) {
      setPingResult({
        success: false,
        error: error instanceof Error ? error.message : String(error),
        timestamp: new Date(),
      });
    } finally {
      setPingLoading(false);
    }
  }, []);

  /**
   * Execute the 'echo' command with a message payload.
   * Demonstrates how to send data to the backend.
   */
  const handleEcho = useCallback(async () => {
    if (!echoInput.trim()) return;
    
    setEchoLoading(true);
    try {
      // Payload is sent as JSON string
      const payload = JSON.stringify({ message: echoInput });
      // Result is already an object, no JSON.parse needed
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'echo', payload);
      setEchoResult({
        success: true,
        data: result,
        timestamp: new Date(),
      });
    } catch (error) {
      setEchoResult({
        success: false,
        error: error instanceof Error ? error.message : String(error),
        timestamp: new Date(),
      });
    } finally {
      setEchoLoading(false);
    }
  }, [echoInput]);

  /**
   * Execute the 'getInfo' command.
   * Returns detailed information about the plugin.
   */
  const handleGetInfo = useCallback(async () => {
    setInfoLoading(true);
    try {
      // Result is already an object, no JSON.parse needed
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'getInfo');
      setInfoResult({
        success: true,
        data: result,
        timestamp: new Date(),
      });
    } catch (error) {
      setInfoResult({
        success: false,
        error: error instanceof Error ? error.message : String(error),
        timestamp: new Date(),
      });
    } finally {
      setInfoLoading(false);
    }
  }, []);

  const renderResult = (result: CommandResult | null, isLoading: boolean) => {
    if (isLoading) {
      return (
        <div className={styles.resultContainer}>
          <Spinner size="tiny" label="Executing..." />
        </div>
      );
    }

    if (!result) return null;

    return (
      <div className={styles.resultContainer}>
        <div className={styles.statusBadge}>
          {result.success ? (
            <Badge appearance="filled" color="success" icon={<CheckmarkCircleRegular />}>
              Success
            </Badge>
          ) : (
            <Badge appearance="filled" color="danger" icon={<DismissCircleRegular />}>
              Error
            </Badge>
          )}
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            {result.timestamp.toLocaleTimeString()}
          </Text>
        </div>
        <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
        <div className={styles.resultLabel}>Response:</div>
        <Text className={`${styles.resultContent} ${!result.success ? styles.errorContent : ''}`}>
          {result.success 
            ? JSON.stringify(result.data, null, 2)
            : result.error}
        </Text>
      </div>
    );
  };

  return (
    <div className={styles.container}>
      {/* Header */}
      <div className={styles.header}>
        <Text className={styles.title}>🔧 Sample Plugin</Text>
        <Text className={styles.subtitle}>
          Reference implementation demonstrating plugin UI ↔ backend communication
        </Text>
        <Badge appearance="outline" color="informative">
          Instance: {instanceId}
        </Badge>
      </div>

      {/* Command Demonstrations */}
      <div className={styles.section}>
        {/* Ping Command */}
        <Card className={styles.commandCard}>
          <CardHeader 
            header={<Text weight="semibold">Ping Command</Text>}
            description="Simple health check - calls the backend and returns a timestamp"
          />
          <div className={styles.commandRow}>
            <Button 
              appearance="primary" 
              icon={<ArrowSyncRegular />}
              onClick={handlePing}
              disabled={pingLoading}
            >
              Ping
            </Button>
          </div>
          {renderResult(pingResult, pingLoading)}
        </Card>

        {/* Echo Command */}
        <Card className={styles.commandCard}>
          <CardHeader 
            header={<Text weight="semibold">Echo Command</Text>}
            description="Send a message to the backend and receive it back with a timestamp"
          />
          <div className={styles.commandRow}>
            <Field label="Message" style={{ flex: 1 }}>
              <Input
                value={echoInput}
                onChange={(_, data) => setEchoInput(data.value)}
                placeholder="Enter a message to echo..."
                onKeyDown={(e) => e.key === 'Enter' && handleEcho()}
              />
            </Field>
            <Button 
              appearance="primary" 
              icon={<SendRegular />}
              onClick={handleEcho}
              disabled={echoLoading || !echoInput.trim()}
            >
              Send
            </Button>
          </div>
          {renderResult(echoResult, echoLoading)}
        </Card>

        {/* Get Info Command */}
        <Card className={styles.commandCard}>
          <CardHeader 
            header={<Text weight="semibold">Get Plugin Info</Text>}
            description="Retrieve detailed information about the running plugin instance"
          />
          <div className={styles.commandRow}>
            <Button 
              appearance="primary" 
              icon={<InfoRegular />}
              onClick={handleGetInfo}
              disabled={infoLoading}
            >
              Get Info
            </Button>
          </div>
          {renderResult(infoResult, infoLoading)}
        </Card>
      </div>

      {/* Usage Guide */}
      <Card className={styles.commandCard}>
        <CardHeader 
          header={<Text weight="semibold">💡 How It Works</Text>}
        />
        <div style={{ marginTop: tokens.spacingVerticalM }}>
          <Text>
            This UI demonstrates calling backend plugin commands using the <code>@ddk/host-sdk</code>:
          </Text>
          <pre style={{ 
            marginTop: tokens.spacingVerticalM,
            padding: tokens.spacingVerticalM,
            backgroundColor: tokens.colorNeutralBackground2,
            borderRadius: tokens.borderRadiusMedium,
            overflow: 'auto',
            fontSize: tokens.fontSizeBase200,
          }}>
{`import { hostBridge } from '@ddk/host-sdk';

// Call a plugin command
const result = await hostBridge.invokePluginCommand(
  'com.ddk.sample',  // Plugin ID
  'echo',            // Command name  
  JSON.stringify({ message: 'Hello!' }) // Payload
);

// Result is already an object (no JSON.parse needed)
console.log(result);`}
          </pre>
        </div>
      </Card>
    </div>
  );
};

export default Plugin;
