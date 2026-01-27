import { useState, useCallback } from 'react';
import { hostBridge } from '@ddk/host-sdk';
import { ComponentResult, IndexStats } from '../types';

const PLUGIN_ID = 'com.ddk.solutionlayeranalyzer';

export const usePluginApi = () => {
  const [indexing, setIndexing] = useState(false);
  const [querying, setQuerying] = useState(false);
  const [diffing, setDiffing] = useState(false);

  const indexSolutions = useCallback(async (
    sourceSolutions: string[],
    targetSolutions: string[],
    componentTypes?: string[]
  ): Promise<IndexStats> => {
    setIndexing(true);
    try {
      const payload = JSON.stringify({
        connectionId: 'default',
        sourceSolutions,
        targetSolutions,
        includeComponentTypes: componentTypes || ['SystemForm', 'SavedQuery', 'RibbonCustomization', 'Entity', 'Attribute'],
        payloadMode: 'lazy',
      });
      
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'index', payload);
      return result as IndexStats;
    } catch (error) {
      console.error('Index error:', error);
      throw error;
    } finally {
      setIndexing(false);
    }
  }, []);

  const queryComponents = useCallback(async (
    filters?: any,
    skip = 0,
    take = 1000
  ): Promise<ComponentResult[]> => {
    setQuerying(true);
    try {
      const payload = JSON.stringify({
        filters: filters || null,
        paging: { skip, take },
        sort: [{ field: 'componentType', dir: 'asc' }],
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'query', payload);
      return (result.rows || []) as ComponentResult[];
    } catch (error) {
      console.error('Query error:', error);
      throw error;
    } finally {
      setQuerying(false);
    }
  }, []);

  const getComponentDetails = useCallback(async (componentId: string): Promise<any> => {
    try {
      const payload = JSON.stringify({ componentId });
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'details', payload);
      return result;
    } catch (error) {
      console.error('Details error:', error);
      throw error;
    }
  }, []);

  const diffComponentLayers = useCallback(async (
    componentId: string,
    leftSolution: string,
    rightSolution: string
  ): Promise<{ leftText: string; rightText: string; mime: string; warnings?: string[] }> => {
    setDiffing(true);
    try {
      const payload = JSON.stringify({
        componentId,
        connectionId: 'default',
        left: { solutionName: leftSolution, payloadType: 'auto' },
        right: { solutionName: rightSolution, payloadType: 'auto' },
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'diff', payload);
      return result;
    } catch (error) {
      console.error('Diff error:', error);
      throw error;
    } finally {
      setDiffing(false);
    }
  }, []);

  const clearIndex = useCallback(async (): Promise<void> => {
    try {
      await hostBridge.invokePluginCommand(PLUGIN_ID, 'clear', '{}');
    } catch (error) {
      console.error('Clear error:', error);
      throw error;
    }
  }, []);

  return {
    indexSolutions,
    queryComponents,
    getComponentDetails,
    diffComponentLayers,
    clearIndex,
    loading: {
      indexing,
      querying,
      diffing,
    },
  };
};
