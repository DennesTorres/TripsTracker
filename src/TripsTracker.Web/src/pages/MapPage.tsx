import { useCallback, useEffect, useRef, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import { usePlaces, useCountries, useVisitedStates, useSetStateBorders } from '@/api/hooks';
import styles from './MapPage.module.scss';

const GEO_BOUNDARIES_API = 'https://www.geoboundaries.org/api/current/gbOpen';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();
  const setStateBorders = useSetStateBorders();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [borderGeoCache, setBorderGeoCache] = useState<Record<string, GeoJSON.FeatureCollection>>({});
  const fetchingRef = useRef<Set<string>>(new Set());
  const [adding, setAdding] = useState(false);

  // Load world base map once
  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(data => {
      setWorldGeo(data as GeoJSON.FeatureCollection);
    });
  }, []);

  // Fetch geoBoundaries GeoJSON for any country with showStateBorders=true and isoAlpha3
  useEffect(() => {
    const toFetch = countries.filter(
      c => c.showStateBorders && c.isoAlpha3 && !borderGeoCache[c.isoAlpha3] && !fetchingRef.current.has(c.isoAlpha3!)
    );
    if (toFetch.length === 0) return;

    toFetch.forEach(async country => {
      const iso3 = country.isoAlpha3!;
      fetchingRef.current.add(iso3);
      try {
        const meta = await fetch(`${GEO_BOUNDARIES_API}/${iso3}/ADM1/`).then(r => r.json());
        const dlUrl: string = meta?.gjDownloadURL;
        if (!dlUrl) return;
        const geo = await fetch(dlUrl).then(r => r.json()) as GeoJSON.FeatureCollection;
        setBorderGeoCache(prev => ({ ...prev, [iso3]: geo }));
      } catch {
        // Silently ignore fetch failures — borders simply won't render for this country
      } finally {
        fetchingRef.current.delete(iso3);
      }
    });
  }, [countries, borderGeoCache]);

  const handleToggleStateBorders = useCallback((countryId: number, show: boolean) => {
    setStateBorders.mutate({ id: countryId, show });
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
