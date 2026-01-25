import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { FluentProvider, webDarkTheme } from '@fluentui/react-components';
import Plugin from './Plugin';

/**
 * Development mode entry point.
 * This renders the plugin in standalone mode for easy development/testing.
 * In production, the Plugin component is loaded via Module Federation.
 */
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <FluentProvider theme={webDarkTheme}>
      <Plugin instanceId="dev-instance" />
    </FluentProvider>
  </StrictMode>
);
