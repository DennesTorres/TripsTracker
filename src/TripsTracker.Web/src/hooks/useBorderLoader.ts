import { useRef, useCallback } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import apiClient from '@/api/client';
import { useMapStatus } from '@/context/MapStatusContext';

export function useBorderLoader(
  borderGeoCache: Record<number, GeoJSON.FeatureCollection>,
  setBorderGeoCache: Dispatch<SetStateAction<Record<number, GeoJSON.FeatureCollection>>>
) {
  const { pushStatus, popStatus } = useMapStatus();
  const fetchingRef = useRef<Set<number>>(new Set());
  const cacheRef = useRef(borderGeoCache);
  cacheRef.current = borderGeoCache;

  const loadBorders = useCallback(async (countryId: number, countryName: string) => {
    if (cacheRef.current[countryId] || fetchingRef.current.has(countryId)) return;
    fetchingRef.current.add(countryId);
    pushStatus(countryId, `Loading borders for ${countryName}…`);
    try {
      const response = await apiClient.get<GeoJSON.FeatureCollection>(
        `/api/countries/${countryId}/borders`
      );
      if (response.data) {
        setBorderGeoCache(prev => ({ ...prev, [countryId]: response.data }));
        // Pop happens in Effect 4 after borders are drawn
      } else {
        popStatus(countryId);
      }
    } catch {
      popStatus(countryId);
    } finally {
      fetchingRef.current.delete(countryId);
    }
  }, [setBorderGeoCache, pushStatus, popStatus]);

  return { loadBorders };
}
