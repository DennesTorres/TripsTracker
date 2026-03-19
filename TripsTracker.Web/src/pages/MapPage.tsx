import { useEffect, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import { usePlaces, useCountries, useVisitedStates } from '@/api/hooks';
import styles from './MapPage.module.scss';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading } = useVisitedStates();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [usGeo, setUsGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [brGeo, setBrGeo] = useState<GeoJSON.FeatureCollection | null>(null);

  useEffect(() => {
    Promise.all([
      fetch('/geo/world-110m.geojson').then(r => r.json()),
      fetch('/geo/us-states.geojson').then(r => r.json()),
      fetch('/geo/brazil-states.geojson').then(r => r.json()),
    ]).then(([world, us, br]) => {
      setWorldGeo(world as GeoJSON.FeatureCollection);
      setUsGeo(us as GeoJSON.FeatureCollection);
      setBrGeo(br as GeoJSON.FeatureCollection);
    });
  }, []);

  const isLoading = placesLoading || countriesLoading || statesLoading || !worldGeo;

  return (
    <div className={styles.page}>
      <div className={styles.mapArea}>
        {isLoading ? (
          <div className={styles.loading}>Loading map…</div>
        ) : (
          <WorldMap
            countries={countries}
            places={places}
            visitedStates={visitedStates}
            geoJson={worldGeo!}
            usStatesGeoJson={usGeo!}
            brazilStatesGeoJson={brGeo!}
          />
        )}
      </div>
      {!isLoading && (
        <StatsBar countries={countries} places={places} />
      )}
    </div>
  );
}
