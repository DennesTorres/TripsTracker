import { createContext, useContext, useState, type Dispatch, type SetStateAction, type ReactNode } from 'react';

interface BorderGeoCacheContextValue {
  borderGeoCache: Record<number, GeoJSON.FeatureCollection>;
  setBorderGeoCache: Dispatch<SetStateAction<Record<number, GeoJSON.FeatureCollection>>>;
}

const BorderGeoCacheContext = createContext<BorderGeoCacheContextValue>({
  borderGeoCache: {},
  setBorderGeoCache: () => {},
});

export function BorderGeoCacheProvider({ children }: { children: ReactNode }) {
  const [borderGeoCache, setBorderGeoCache] = useState<Record<number, GeoJSON.FeatureCollection>>({});
  return (
    <BorderGeoCacheContext.Provider value={{ borderGeoCache, setBorderGeoCache }}>
      {children}
    </BorderGeoCacheContext.Provider>
  );
}

export function useBorderGeoCache() {
  return useContext(BorderGeoCacheContext);
}
