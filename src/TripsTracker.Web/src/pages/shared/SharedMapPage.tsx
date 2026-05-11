import { useState, useEffect } from 'react';
import { useIsAuthenticated } from '@azure/msal-react';
import { useSharedMap } from '@/api/hooks';
import WorldMap from '@/components/map/WorldMap';
import PlacePopup from '@/pages/places/PlacePopup';
import PlaceDetailPanel from '@/pages/places/PlaceDetailPanel';
import type { Place } from '@/types';
import styles from './SharedMapPage.module.scss';

interface Props {
  token: string;
}

export default function SharedMapPage({ token }: Props) {
  const { data, isLoading, error } = useSharedMap(token);
  const isAuthenticated = useIsAuthenticated();
  const [geoJson, setGeoJson] = useState<GeoJSON.FeatureCollection | null>(null);
  const [usStatesGeoJson, setUsStatesGeoJson] = useState<GeoJSON.FeatureCollection | null>(null);
  const [brazilStatesGeoJson, setBrazilStatesGeoJson] = useState<GeoJSON.FeatureCollection | null>(null);
  const [popup, setPopup] = useState<{ places: Place[]; x: number; y: number } | null>(null);
  const [selectedPlace, setSelectedPlace] = useState<Place | null>(null);

  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(setGeoJson);
    fetch('/geo/us-states.geojson').then(r => r.json()).then(setUsStatesGeoJson);
    fetch('/geo/brazil-states.geojson').then(r => r.json()).then(setBrazilStatesGeoJson);
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
          usStatesGeoJson={usStatesGeoJson!}
          brazilStatesGeoJson={brazilStatesGeoJson!}
          onPlaceClick={(places, screenX, screenY) => setPopup({ places, x: screenX, y: screenY })}
        />
        {popup && (
          <>
            <div style={{ position: 'absolute', inset: 0, zIndex: 29 }} onClick={() => setPopup(null)} />
            <PlacePopup
              places={popup.places}
              x={popup.x}
              y={popup.y}
              onClose={() => setPopup(null)}
              onSeeMore={place => { setPopup(null); setSelectedPlace(place); }}
            />
          </>
        )}
        {selectedPlace && (
          <PlaceDetailPanel place={selectedPlace} onClose={() => setSelectedPlace(null)} />
        )}
      </div>
    </div>
  );
}
