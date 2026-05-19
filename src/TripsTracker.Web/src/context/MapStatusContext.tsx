import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

interface StatusEntry { id: string | number; message: string; promoted: boolean; }

interface MapStatusContextValue {
  loadingMessage: string | null;
  pushStatus:    (id: string | number, message: string) => void;
  promoteStatus: (id: string | number) => void;
  dismissStatus: (id: string | number) => void;
}

const MapStatusContext = createContext<MapStatusContextValue>({
  loadingMessage: null,
  pushStatus:    () => {},
  promoteStatus: () => {},
  dismissStatus: () => {},
});

export function MapStatusProvider({ children }: { children: ReactNode }) {
  const [queue, setQueue] = useState<StatusEntry[]>([]);

  const pushStatus = useCallback((id: string | number, message: string) => {
    setQueue(q => q.some(e => e.id === id) ? q : [...q, { id, message, promoted: false }]);
  }, []);

  const promoteStatus = useCallback((id: string | number) => {
    setQueue(q => {
      const entry = q.find(e => e.id === id);
      if (!entry) return q;
      const without = q.filter(e => e.id !== id);
      const lastPromotedIdx = without.reduce((acc, e, i) => e.promoted ? i : acc, -1);
      const insertAt = lastPromotedIdx + 1;
      return [
        ...without.slice(0, insertAt),
        { ...entry, promoted: true },
        ...without.slice(insertAt),
      ];
    });
  }, []);

  const dismissStatus = useCallback((id: string | number) => {
    setQueue(q => q.filter(e => e.id !== id));
  }, []);

  const loadingMessage = queue.length > 0 ? queue[0].message : null;

  return (
    <MapStatusContext.Provider value={{ loadingMessage, pushStatus, promoteStatus, dismissStatus }}>
      {children}
    </MapStatusContext.Provider>
  );
}

export function useMapStatus() {
  return useContext(MapStatusContext);
}
