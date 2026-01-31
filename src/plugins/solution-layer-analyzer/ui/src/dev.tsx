import React from 'react';
import ReactDOM from 'react-dom/client';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import Plugin from './Plugin';



const App: React.FC = () => {
  return (
      <Plugin instanceId="dev-instance" />
  );
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <FluentProvider theme={webLightTheme}>
      <App />
    </FluentProvider>
  </React.StrictMode>
);
