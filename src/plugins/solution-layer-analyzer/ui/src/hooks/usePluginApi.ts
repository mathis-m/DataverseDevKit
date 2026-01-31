import { useState, useCallback, useEffect, useRef } from 'react';
import { hostBridge } from '@ddk/host-sdk';
import { ComponentResult, IndexResponse, IndexCompletionEvent, FilterNode, AttributeDiff, IndexMetadata, QueryResultEvent, QueryAcknowledgment } from '../types';
import { AnalyticsData } from '../types/analytics';
import { transformFilterForBackend } from '../utils/filterTransform';
import { useAppStore } from '../store/useAppStore';

const PLUGIN_ID = 'com.ddk.solutionlayeranalyzer';

/** Generates a unique query ID */
const generateQueryId = () => `q_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

export const usePluginApi = () => {
  const [indexing, setIndexing] = useState(false);
  const [diffing, setDiffing] = useState(false);
  const [indexCompletion, setIndexCompletion] = useState<IndexCompletionEvent | null>(null);
  
  // Get store actions for query state management
  const { setQueryState, setAnalysisState, queryState } = useAppStore();
  
  // Ref to track the latest query ID for event handler
  const latestQueryIdRef = useRef<string | null>(null);

  // Listen for index completion events
  useEffect(() => {
    const unsubscribe = hostBridge.addEventListener('plugin:sla:index-complete', (event) => {
      console.log('Index completion event received:', event);
      try {
        const payload = typeof event.payload === 'string' 
          ? JSON.parse(event.payload) 
          : event.payload;
        setIndexCompletion(payload as IndexCompletionEvent);
        setIndexing(false);
      } catch (error) {
        console.error('Failed to parse index completion event:', error);
        setIndexing(false);
      }
    });

    return () => {
      unsubscribe();
    };
  }, []);

  // Listen for query result events
  useEffect(() => {
    const unsubscribe = hostBridge.addEventListener('plugin:sla:query-result', (event) => {
      console.log('Query result event received:', event);
      try {
        const payload = typeof event.payload === 'string'
          ? JSON.parse(event.payload)
          : event.payload;
        const result = payload as QueryResultEvent;
        
        // Only process if this is the latest query
        if (result.queryId !== latestQueryIdRef.current) {
          console.log(`Ignoring stale query result: ${result.queryId} (latest: ${latestQueryIdRef.current})`);
          return;
        }
        
        if (result.success) {
          // Update analysis state with the results
          setAnalysisState({
            allComponents: result.rows || [],
            filteredComponents: result.rows || [],
          });
          
          setQueryState({
            isQuerying: false,
            lastQueryStats: result.stats || null,
            lastError: null,
          });
        } else {
          console.error('Query failed:', result.errorMessage);
          setQueryState({
            isQuerying: false,
            lastError: result.errorMessage || 'Query failed',
          });
        }
      } catch (error) {
        console.error('Failed to parse query result event:', error);
        setQueryState({
          isQuerying: false,
          lastError: 'Failed to parse query result',
        });
      }
    });

    return () => {
      unsubscribe();
    };
  }, [setAnalysisState, setQueryState]);

  const indexSolutions = useCallback(async (
    sourceSolutions: string[],
    targetSolutions: string[],
    componentTypes?: string[],
    connectionId: string = 'default'
  ): Promise<IndexResponse> => {
    setIndexing(true);
    setIndexCompletion(null);
    try {
      const payload = JSON.stringify({
        connectionId,
        sourceSolutions,
        targetSolutions,
        includeComponentTypes: componentTypes || ['SystemForm', 'SavedQuery', 'RibbonCustomization', 'Entity', 'Attribute'],
        payloadMode: 'lazy',
      });
      
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'index', payload);
      return result as IndexResponse;
    } catch (error) {
      console.error('Index error:', error);
      setIndexing(false);
      throw error;
    }
  }, []);

  const queryComponents = useCallback(async (
    filters?: FilterNode | null,
    skip = 0,
    take = 1000,
    connectionId: string = 'default'
  ): Promise<ComponentResult[]> => {
    // Generate a new query ID
    const queryId = generateQueryId();
    latestQueryIdRef.current = queryId;
    
    setQueryState({
      latestQueryId: queryId,
      isQuerying: true,
      lastError: null,
    });
    
    try {
      const payload = JSON.stringify({
        queryId,
        connectionId,
        filters: transformFilterForBackend(filters ?? null),
        paging: { skip, take },
        sort: [{ field: 'componentType', dir: 'asc' }],
        useEventResponse: true, // Use event-based response
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'query', payload);
      const ack = result as QueryAcknowledgment;
      
      if (!ack.started) {
        throw new Error(ack.errorMessage || 'Query failed to start');
      }
      
      // Return empty array immediately - actual results will come via event
      // The caller should rely on the store state for results
      return [];
    } catch (error) {
      console.error('Query error:', error);
      setQueryState({
        isQuerying: false,
        lastError: error instanceof Error ? error.message : 'Query failed',
      });
      throw error;
    }
  }, [setQueryState]);

  /**
   * Synchronous query that waits for results (for backward compatibility).
   * Use queryComponents for event-based queries.
   */
  const queryComponentsSync = useCallback(async (
    filters?: FilterNode | null,
    skip = 0,
    take = 1000,
    connectionId: string = 'default'
  ): Promise<ComponentResult[]> => {
    setQueryState({ isQuerying: true, lastError: null });
    try {
      const payload = JSON.stringify({
        connectionId,
        filters: transformFilterForBackend(filters ?? null),
        paging: { skip, take },
        sort: [{ field: 'componentType', dir: 'asc' }],
        useEventResponse: false, // Synchronous response
      });
      
      const result: any = await hostBridge.invokePluginCommand(PLUGIN_ID, 'query', payload);
      return (result.rows || []) as ComponentResult[];
    } catch (error) {
      console.error('Query error:', error);
      throw error;
    } finally {
      setQueryState({ isQuerying: false });
    }
  }, [setQueryState]);

  const getComponentDetails = useCallback(async (
    componentId: string,
    connectionId: string = 'default'
  ): Promise<any> => {
    try {
      const payload = JSON.stringify({ componentId, connectionId });
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
    rightSolution: string,
    connectionId: string = 'default'
  ): Promise<{ attributes: AttributeDiff[]; warnings?: string[] }> => {
    setDiffing(true);
    try {
      const payload = JSON.stringify({
        componentId,
        connectionId,
        left: { solutionName: leftSolution },
        right: { solutionName: rightSolution },
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

  const clearIndex = useCallback(async (connectionId: string = 'default'): Promise<void> => {
    try {
      await hostBridge.invokePluginCommand(PLUGIN_ID, 'clear', JSON.stringify({ connectionId }));
    } catch (error) {
      console.error('Clear error:', error);
      throw error;
    }
  }, []);

  const saveIndexConfig = useCallback(async (config: {
    name: string;
    connectionId: string;
    sourceSolutions: string[];
    targetSolutions: string[];
    componentTypes: string[];
    payloadMode: string;
  }): Promise<{ configId: number; configHash: string }> => {
    try {
      const payload = JSON.stringify(config);
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'saveIndexConfig', payload);
      return result;
    } catch (error) {
      console.error('Save index config error:', error);
      throw error;
    }
  }, []);

  const loadIndexConfigs = useCallback(async (request: {
    connectionId?: string;
  }): Promise<{ configs: any[] }> => {
    try {
      const payload = JSON.stringify(request);
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'loadIndexConfigs', payload);
      return result;
    } catch (error) {
      console.error('Load index configs error:', error);
      throw error;
    }
  }, []);

  const saveFilterConfig = useCallback(async (config: {
    name: string;
    connectionId?: string;
    originatingIndexHash?: string;
    filter: FilterNode | null;
  }): Promise<{ configId: number }> => {
    try {
      const payload = JSON.stringify({
        ...config,
        filter: transformFilterForBackend(config.filter),
      });
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'saveFilterConfig', payload);
      return result;
    } catch (error) {
      console.error('Save filter config error:', error);
      throw error;
    }
  }, []);

  const loadFilterConfigs = useCallback(async (request: {
    connectionId?: string;
    currentIndexHash?: string;
  }): Promise<{ configs: any[] }> => {
    try {
      const payload = JSON.stringify(request);
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'loadFilterConfigs', payload);
      return result;
    } catch (error) {
      console.error('Load filter configs error:', error);
      throw error;
    }
  }, []);

  const fetchSolutions = useCallback(async (connectionId: string = 'default'): Promise<{ solutions: any[] }> => {
    try {
      const payload = JSON.stringify({ connectionId });
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'fetchSolutions', payload);
      return result;
    } catch (error) {
      console.error('Fetch solutions error:', error);
      throw error;
    }
  }, []);

  const getComponentTypes = useCallback(async (): Promise<{ componentTypes: any[] }> => {
    try {
      const payload = JSON.stringify({});
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'getComponentTypes', payload);
      return result;
    } catch (error) {
      console.error('Get component types error:', error);
      throw error;
    }
  }, []);

  const getIndexMetadata = useCallback(async (connectionId: string = 'default'): Promise<IndexMetadata> => {
    try {
      const payload = JSON.stringify({ connectionId });
      const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'getIndexMetadata', payload);
      return result as IndexMetadata;
    } catch (error) {
      console.error('Get index metadata error:', error);
      throw error;
    }
  }, []);

  return {
    indexSolutions,
    queryComponents,
    queryComponentsSync,
    getComponentDetails,
    diffComponentLayers,
    clearIndex,
    saveIndexConfig,
    loadIndexConfigs,
    saveFilterConfig,
    loadFilterConfigs,
    fetchSolutions,
    getComponentTypes,
    getIndexMetadata,
    indexCompletion,
    queryState,
    loading: {
      indexing,
      querying: queryState.isQuerying,
      diffing,
    },
    getAnalytics: async (connectionId: string = 'default'): Promise<AnalyticsData> => {
      try {
        const payload = JSON.stringify({ connectionId });
        const result = await hostBridge.invokePluginCommand(PLUGIN_ID, 'getAnalytics', payload);
        console.log('Analytics data received:', result);
        return result;
      } catch (error) {
        console.error('Failed to get analytics:', error);
        throw error;
      }
    },
  };
};
