import { useCallback, useEffect, useRef, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import { usePlaces, useCountries, useVisitedStates, useSetStateBorders } from '@/api/hooks';
import apiClient from '@/api/client';
import styles from './MapPage.module.scss';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();
  const setStateBorders = useSetStateBorders();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [borderGeoCache, setBorderGeoCache] = useState<Record<number, GeoJSON.FeatureCollection>>({});
  const fetchingRef = useRef<Set<number>>(new Set());
  const borderGeoCacheRef = useRef<Record<number, GeoJSON.FeatureCollection>>({});
  borderGeoCacheRef.current = borderGeoCache;
  const [adding, setAdding] = useState(false);
  const [loadingCountryId, setLoadingCountryId] = useState<number | null>(null);

  // Load world base map once
  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(data => {
      setWorldGeo(data as GeoJSON.FeatureCollection);
    });
  }, []);

  // Fetch state border GeoJSON via backend for countries with showStateBorders=true
  useEffect(() => {
    const toFetch = countries.filter(
      c => c.showStateBorders && !borderGeoCache[c.id] && !fetchingRef.current.has(c.id)
    );
    if (toFetch.length === 0) return;

    toFetch.forEach(async country => {
      fetchingRef.current.add(country.id);
      try {
        const response = await apiClient.get<GeoJSON.FeatureCollection>(
          `/api/countries/${country.id}/borders`
        );
        if (response.data) {
          setBorderGeoCache(prev => ({ ...prev, [country.id]: response.data }));
        }
      } catch {
        // Silently ignore — borders won't render for this country
      } finally {
        fetchingRef.current.delete(country.id);
        // Clear loading indicator if this fetch was triggered by a toggle
        setLoadingCountryId(prev => prev === country.id ? null : prev);
      }
    });
  }, [countries, borderGeoCache]);

  const handleToggleStateBorders = useCallback((countryId: number, show: boolean) => {
    setLoadingCountryId(countryId);
    setStateBorders.mutate(
      { id: countryId, show },
      {
        onSuccess: () => {
          // Disabling or GeoJSON already cached: no fetch will happen, clear loading now
          if (!show || borderGeoCacheRef.current[countryId]) {
            setLoadingCountryId(null);
          }
          // Enabling and not cached: the GeoJSON fetch finally-block will clear loading
        },
        onError: () => setLoadingCountryId(null),
      }
    );
  }, [setStateBorders]);

  const isLoading = placesLoading || countriesLoading || statesLoading || !worldGeo;
  const hasError = !isLoading && (placesError || countriesError || statesError);

  function retryAll() {
    refetchPlaces();
    refetchCountries();
    refetchStates();
  }

  return (
    <div className={styles.page}>
      <div className={styles.mapArea}>
        {isLoading ? (
          <div className={styles.loading}>Loading map…</div>
        ) : hasError ? (
          <div className={styles.loading}>
            Failed to load data — is the API running?&nbsp;
            <button onClick={retryAll} style={{ marginLeft: 8, cursor: 'pointer' }}>Retry</button>
          </div>
        ) : (
          <WorldMap
            countries={countries}
            places={places}
            visitedStates={visitedStates}
            geoJson={worldGeo!}
            borderGeoCache={borderGeoCache}
            onToggleStateBorders={handleToggleStateBorders}
            loadingCountryName={loadingCountryId
              ? (countries.find(c => c.id === loadingCountryId)?.name ?? null)
              : null}
          />
        )}
        {!isLoading && !hasError && (
          <button className={styles.addBtn} onClick={() => setAdding(true)}>
            + Add place
          </button>
        )}
      </div>
      {!isLoading && (
        <StatsBar countries={countries} places={places} />
      )}
      {adding && <AddPlaceForm onClose={() => setAdding(false)} />}
    </div>
  );
}
