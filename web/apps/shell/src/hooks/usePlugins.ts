import { useState, useEffect } from 'react';
import { hostBridge } from '@ddk/host-sdk';
import { usePluginStore } from '../stores/plugins';

export const usePlugins = () => {
  const { availablePlugins, setAvailablePlugins } = usePluginStore();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;

    const loadPlugins = async () => {
      // Prevent loading if already loaded
      if (availablePlugins.length > 0) {
        return;
      }

      setLoading(true);
      setError(null);

      try {
        const plugins = await hostBridge.listPlugins();
        if (mounted) {
          setAvailablePlugins(plugins);
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err.message : 'Failed to load plugins');
          console.error('Failed to load plugins:', err);
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };

    loadPlugins();

    return () => {
      mounted = false;
    };
  }, []); // Only run once on mount

  return { availablePlugins, loading, error };
};
