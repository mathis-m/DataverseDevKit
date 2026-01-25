import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { FluentProvider, webDarkTheme } from '@fluentui/react-components';
import Plugin from './Plugin';

// Development mode preview
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <FluentProvider theme={webDarkTheme}>
      <Plugin instanceId="dev-instance" connectionId="dev-connection" />
    </FluentProvider>
  </StrictMode>
);
