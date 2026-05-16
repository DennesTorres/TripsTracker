import { useCallback, useEffect, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import { usePlaces, useCountries, useVisitedStates, useSetStateBorders } from '@/api/hooks';
import { useBorderLoader } from '@/hooks/useBorderLoader';
import { useBorderGeoCache } from '@/context/BorderGeoCacheContext';
import styles from './MapPage.module.scss';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();
  const setStateBorders = useSetStateBorders();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const { borderGeoCache, setBorderGeoCache } = useBorderGeoCache();
  const [adding, setAdding] = useState(false);

  const { loadBorders } = useBorderLoader(borderGeoCache, setBorderGeoCache);

  // Load world base map once
  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(data => {
      setWorldGeo(data as GeoJSON.FeatureCollection);
    });
  }, []);

  // Fetch state border GeoJSON for all countries with showStateBorders=true
  useEffect(() => {
    countries
      .filter(c => c.showStateBorders)
      .forEach(c => loadBorders(c.id, c.name));
  }, [countries, loadBorders]);

  const handleToggleStateBorders = useCallback((countryId: number, show: boolean) => {
    const country = countries.find(c => c.id === countryId);
    setStateBorders.mutate(
      { id: countryId, show },
      {
        onSuccess: () => {
          if (show && country) loadBorders(countryId, country.name);
        },
      }
    );
  }, [setStateBorders, countries, loadBorders]);

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
          <div className={styles.loading}>Loading map...</div>
        ) : hasError ? (
          <div className={styles.loading}>
            Failed to load data -- is the API running?&nbsp;
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
