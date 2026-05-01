import { useEffect, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import ShareModal from '@/components/share/ShareModal';
import DiscoverMapsModal from '@/components/share/DiscoverMapsModal';
import { usePlaces, useCountries, useVisitedStates } from '@/api/hooks';
import { Share2, Compass } from 'lucide-react';
import styles from './MapPage.module.scss';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [usGeo, setUsGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [brGeo, setBrGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [adding, setAdding] = useState(false);
  const [sharing, setSharing] = useState(false);
  const [discovering, setDiscovering] = useState(false);

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
            usStatesGeoJson={usGeo!}
            brazilStatesGeoJson={brGeo!}
          />
        )}
        {!isLoading && !hasError && (
          <div className={styles.mapButtons}>
            <button className={styles.addBtn} onClick={() => setAdding(true)}>
              + Add place
            </button>
            <button className={styles.shareBtn} onClick={() => setSharing(true)}>
              <Share2 size={14} /> Share
            </button>
            <button className={styles.discoverBtn} onClick={() => setDiscovering(true)}>
              <Compass size={14} /> Discover
            </button>
          </div>
        )}
      </div>
      {!isLoading && (
        <StatsBar countries={countries} places={places} />
      )}
      {adding && <AddPlaceForm onClose={() => setAdding(false)} />}
      {sharing && <ShareModal onClose={() => setSharing(false)} />}
      {discovering && (
        <DiscoverMapsModal
          onOpen={token => { window.location.hash = `/shared/${token}`; }}
          onClose={() => setDiscovering(false)}
        />
      )}
    </div>
  );
}
