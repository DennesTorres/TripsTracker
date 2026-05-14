import { createContext, useContext, useState, type ReactNode } from 'react';

interface MapStatusContextValue {
  loadingMessage: string | null;
  setLoadingMessage: (msg: string | null) => void;
}

const MapStatusContext = createContext<MapStatusContextValue>({
  loadingMessage: null,
  setLoadingMessage: () => {},
});

export function MapStatusProvider({ children }: { children: ReactNode }) {
  const [loadingMessage, setLoadingMessage] = useState<string | null>(null);
  return (
    <MapStatusContext.Provider value={{ loadingMessage, setLoadingMessage }}>
      {children}
    </MapStatusContext.Provider>
  );
}

export function useMapStatus() {
  return useContext(MapStatusContext);
}
