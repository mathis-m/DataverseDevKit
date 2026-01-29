import { create } from 'zustand';
import type { PluginMetadata } from '@ddk/host-sdk';

export type TabType = 'plugin' | 'system';
export type SystemView = 'connections' | 'marketplace' | 'settings';

export interface TabInstance {
  instanceId: string;
  type: TabType;
  title: string;
  icon?: React.ReactNode;
  // For plugin tabs
  pluginId?: string;
  connectionId?: string | null;
  remoteEntry?: string;
  scope?: string;
  module?: string;
  // For system tabs
  systemView?: SystemView;
}

interface PluginState {
  availablePlugins: PluginMetadata[];
  tabs: TabInstance[];
  activeTabId: string | null;
  setAvailablePlugins: (plugins: PluginMetadata[]) => void;
  addTab: (tab: TabInstance) => void;
  removeTab: (tabId: string) => void;
  updateTab: (tabId: string, updates: Partial<TabInstance>) => void;
  setActiveTab: (tabId: string | null) => void;
  openSystemView: (view: SystemView, title: string, icon?: React.ReactNode) => void;
}

export const usePluginStore = create<PluginState>((set) => ({
  availablePlugins: [],
  tabs: [],
  activeTabId: null,
  setAvailablePlugins: (availablePlugins) => set({ availablePlugins }),
  addTab: (tab) =>
    set((state) => ({
      tabs: [...state.tabs, tab],
      activeTabId: tab.instanceId,
    })),
  removeTab: (tabId) =>
    set((state) => ({
      tabs: state.tabs.filter((t) => t.instanceId !== tabId),
      activeTabId: state.activeTabId === tabId ? null : state.activeTabId,
    })),
  updateTab: (tabId, updates) =>
    set((state) => ({
      tabs: state.tabs.map((t) =>
        t.instanceId === tabId ? { ...t, ...updates } : t
      ),
    })),
  setActiveTab: (activeTabId) => set({ activeTabId }),
  openSystemView: (view, title, icon) =>
    set((state) => {
      // Check if this system view is already open
      const existing = state.tabs.find(
        (t) => t.type === 'system' && t.systemView === view
      );
      if (existing) {
        return { activeTabId: existing.instanceId };
      }
      // Create new system tab
      const newTab: TabInstance = {
        instanceId: `system-${view}-${Date.now()}`,
        type: 'system',
        title,
        systemView: view,
        icon,
      };
      return {
        tabs: [...state.tabs, newTab],
        activeTabId: newTab.instanceId,
      };
    }),
}));
