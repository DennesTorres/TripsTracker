import { createContext, useContext, useState, type Dispatch, type SetStateAction, type ReactNode } from 'react';

interface BorderGeoCacheContextValue {
  borderGeoCache: Map<number, GeoJSON.FeatureCollection>;
  setBorderGeoCache: Dispatch<SetStateAction<Map<number, GeoJSON.FeatureCollection>>>;
}

const BorderGeoCacheContext = createContext<BorderGeoCacheContextValue>({
  borderGeoCache: new Map(),
  setBorderGeoCache: () => {},
});

export function BorderGeoCacheProvider({ children }: { children: ReactNode }) {
  const [borderGeoCache, setBorderGeoCache] = useState<Map<number, GeoJSON.FeatureCollection>>(new Map());
  return (
    <BorderGeoCacheContext.Provider value={{ borderGeoCache, setBorderGeoCache }}>
      {children}
    </BorderGeoCacheContext.Provider>
  );
}

export function useBorderGeoCache() {
  return useContext(BorderGeoCacheContext);
}
