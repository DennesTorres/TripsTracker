import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

interface StatusEntry { id: string | number; message: string; }

interface MapStatusContextValue {
  loadingMessage: string | null;
  pushStatus: (id: string | number, message: string) => void;
  popStatus:  (id: string | number) => void;
}

const MapStatusContext = createContext<MapStatusContextValue>({
  loadingMessage: null,
  pushStatus: () => {},
  popStatus:  () => {},
});

export function MapStatusProvider({ children }: { children: ReactNode }) {
  const [queue, setQueue] = useState<StatusEntry[]>([]);

  const pushStatus = useCallback((id: string | number, message: string) => {
    setQueue(q => q.some(e => e.id === id) ? q : [...q, { id, message }]);
  }, []);

  const popStatus = useCallback((id: string | number) => {
    setQueue(q => q.filter(e => e.id !== id));
  }, []);

  const loadingMessage = queue.length > 0 ? queue[0].message : null;

  return (
    <MapStatusContext.Provider value={{ loadingMessage, pushStatus, popStatus }}>
      {children}
    </MapStatusContext.Provider>
  );
}

export function useMapStatus() {
  return useContext(MapStatusContext);
}
