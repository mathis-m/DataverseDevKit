import React, { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  shorthands,
  Card,
  CardHeader,
  Text,
  Button,
  Input,
  Field,
  Badge,
} from '@fluentui/react-components';
import {
  SendRegular,
  ClockRegular,
  PlayRegular,
  StopRegular,
  CheckmarkCircleRegular,
} from '@fluentui/react-icons';
import { hostBridge, type EventCallback } from '@ddk/host-sdk';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalL),
    ...shorthands.padding(tokens.spacingVerticalL),
    height: '100%',
    overflowY: 'auto',
  },
  header: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
  },
  title: {
    fontSize: tokens.fontSizeHero800,
    fontWeight: tokens.fontWeightSemibold,
  },
  subtitle: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalM),
  },
  commandCard: {
    ...shorthands.padding(tokens.spacingVerticalL),
  },
  commandRow: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalM),
    alignItems: 'flex-end',
  },
  resultCard: {
    ...shorthands.padding(tokens.spacingVerticalM),
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
  },
  eventLog: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
    maxHeight: '200px',
    overflowY: 'auto',
    ...shorthands.padding(tokens.spacingVerticalS),
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
  },
  eventItem: {
    fontSize: tokens.fontSizeBase200,
    fontFamily: 'monospace',
  },
});

interface PluginProps {
  instanceId: string;
  connectionId?: string;
}

const Plugin: React.FC<PluginProps> = ({ connectionId }) => {
  const styles = useStyles();
  const [echoInput, setEchoInput] = useState('');
  const [echoResult, setEchoResult] = useState<string | null>(null);
  const [currentTime, setCurrentTime] = useState<string | null>(null);
  const [heartbeatActive, setHeartbeatActive] = useState(false);
  const [events, setEvents] = useState<string[]>([]);

  useEffect(() => {
    // Subscribe to heartbeat events
    const unsubscribe = hostBridge.addEventListener('heartbeat', handleEvent);
    return () => unsubscribe();
  }, []);

  const handleEvent: EventCallback = (event) => {
    const timestamp = new Date(event.timestamp).toLocaleTimeString();
    setEvents((prev) => [...prev, `[${timestamp}] ${event.type}: ${JSON.stringify(event.payload)}`]);
  };

  const handleEcho = async () => {
    try {
      const result = await hostBridge.invokePluginCommand(
        'com.contoso.ddk.helloworld',
        'echo',
        JSON.stringify({ message: echoInput })
      );
      setEchoResult(result);
    } catch (error) {
      console.error('Echo command failed:', error);
      setEchoResult('Error: ' + (error as Error).message);
    }
  };

  const handleGetTime = async () => {
    try {
      const result = await hostBridge.invokePluginCommand(
        'com.contoso.ddk.helloworld',
        'getTime',
        ''
      );
      setCurrentTime(result);
    } catch (error) {
      console.error('GetTime command failed:', error);
      setCurrentTime('Error: ' + (error as Error).message);
    }
  };

  const handleToggleHeartbeat = async () => {
    try {
      if (!heartbeatActive) {
        await hostBridge.invokePluginCommand(
          'com.contoso.ddk.helloworld',
          'startHeartbeat',
          JSON.stringify({ interval: 2000 })
        );
        setHeartbeatActive(true);
      } else {
        // In a real implementation, there would be a stopHeartbeat command
        setHeartbeatActive(false);
      }
    } catch (error) {
      console.error('Heartbeat command failed:', error);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text className={styles.title}>Hello World Plugin</Text>
        <Text className={styles.subtitle}>
          A demonstration plugin showcasing basic DevKit capabilities
        </Text>
        {connectionId && (
          <Badge appearance="outline" color="success" icon={<CheckmarkCircleRegular />}>
            Connected: {connectionId}
          </Badge>
        )}
      </div>

      <div className={styles.section}>
        <Card className={styles.commandCard}>
          <CardHeader header={<Text weight="semibold">Echo Command</Text>} />
          <div className={styles.commandRow}>
            <Field label="Message" style={{ flex: 1 }}>
              <Input
                value={echoInput}
                onChange={(_, data) => setEchoInput(data.value)}
                placeholder="Enter a message..."
              />
            </Field>
            <Button appearance="primary" icon={<SendRegular />} onClick={handleEcho}>
              Send
            </Button>
          </div>
          {echoResult && (
            <div className={styles.resultCard}>
              <Text weight="semibold">Result:</Text>
              <Text>{echoResult}</Text>
            </div>
          )}
        </Card>

        <Card className={styles.commandCard}>
          <CardHeader header={<Text weight="semibold">Get Current Time</Text>} />
          <Button appearance="primary" icon={<ClockRegular />} onClick={handleGetTime}>
            Get Time
          </Button>
          {currentTime && (
            <div className={styles.resultCard}>
              <Text weight="semibold">Current Time:</Text>
              <Text>{currentTime}</Text>
            </div>
          )}
        </Card>

        <Card className={styles.commandCard}>
          <CardHeader header={<Text weight="semibold">Heartbeat Events</Text>} />
          <Button
            appearance={heartbeatActive ? 'secondary' : 'primary'}
            icon={heartbeatActive ? <StopRegular /> : <PlayRegular />}
            onClick={handleToggleHeartbeat}
          >
            {heartbeatActive ? 'Stop' : 'Start'} Heartbeat
          </Button>
          {events.length > 0 && (
            <div className={styles.eventLog}>
              <Text weight="semibold">Event Log:</Text>
              {events.slice(-10).map((event, idx) => (
                <Text key={idx} className={styles.eventItem}>
                  {event}
                </Text>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
};

export default Plugin;
