import { create } from 'zustand';
import type { ConnectionInfo } from '@ddk/host-sdk';

interface ConnectionState {
  connections: ConnectionInfo[];
  activeConnectionId: string | null;
  setConnections: (connections: ConnectionInfo[]) => void;
  addConnection: (connection: ConnectionInfo) => void;
  removeConnection: (id: string) => void;
  setActiveConnection: (id: string | null) => void;
}

export const useConnectionStore = create<ConnectionState>((set) => ({
  connections: [],
  activeConnectionId: null,
  setConnections: (connections) => set({ connections }),
  addConnection: (connection) =>
    set((state) => ({ connections: [...state.connections, connection] })),
  removeConnection: (id) =>
    set((state) => ({
      connections: state.connections.filter((c) => c.id !== id),
      activeConnectionId: state.activeConnectionId === id ? null : state.activeConnectionId,
    })),
  setActiveConnection: (id) => set({ activeConnectionId: id }),
}));
