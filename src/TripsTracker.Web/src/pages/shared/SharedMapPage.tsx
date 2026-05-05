import { useState, useEffect } from 'react';
import { useIsAuthenticated } from '@azure/msal-react';
import { useSharedMap } from '@/api/hooks';
import WorldMap from '@/components/map/WorldMap';
import styles from './SharedMapPage.module.scss';

interface Props {
  token: string;
}

export default function SharedMapPage({ token }: Props) {
  const { data, isLoading, error } = useSharedMap(token);
  const isAuthenticated = useIsAuthenticated();
  const [geoJson, setGeoJson] = useState<GeoJSON.FeatureCollection | null>(null);

  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(setGeoJson);
  }, []);

  if (isLoading) return <div className={styles.loading}>Loading shared map...</div>;
  if (error || !data) return <div className={styles.error}>This shared map is not available.</div>;
  if (!geoJson) return <div className={styles.loading}>Loading map data...</div>;

  const visitedCount = data.countries.filter(c => c.isVisited).length;
  const continents = new Set(data.countries.filter(c => c.isVisited).map(c => c.region)).size;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <span className={styles.brand}>TripsTracker</span>
        <span className={styles.owner}>{data.ownerDisplayName}'s travel map</span>
        <span className={styles.stats}>
          {data.places.length} places · {visitedCount} countries · {continents} continents
        </span>
        <div className={styles.headerActions}>
          {isAuthenticated
            ? <a className={styles.myMapLink} href="#">My map</a>
            : <a className={styles.myMapLink} href="#">Sign in</a>
          }
        </div>
      </div>
      <div className={styles.mapContainer}>
        <WorldMap
          countries={data.countries}
          places={data.places}
          visitedStates={data.visitedStates}
          geoJson={geoJson}
          borderGeoCache={{}}
        />
      </div>
    </div>
  );
}
