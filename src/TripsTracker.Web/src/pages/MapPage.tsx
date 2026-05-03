import { useCallback, useEffect, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import PlaceDetailModal from '@/pages/places/PlaceDetailModal';
import ShareModal from '@/components/share/ShareModal';
import DiscoverMapsModal from '@/components/share/DiscoverMapsModal';
import { usePlaces, useCountries, useVisitedStates, useSetStateBorders } from '@/api/hooks';
import type { Place } from '@/types';
import { Share2, Compass } from 'lucide-react';
import styles from './MapPage.module.scss';

export default function MapPage() {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();
  const setStateBorders = useSetStateBorders();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [usGeo, setUsGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [brGeo, setBrGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [arGeo, setArGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [gbGeo, setGbGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [adding, setAdding] = useState(false);
  const [sharing, setSharing] = useState(false);
  const [discovering, setDiscovering] = useState(false);
  const [selectedPlace, setSelectedPlace] = useState<Place | null>(null);

  useEffect(() => {
    Promise.all([
      fetch('/geo/world-110m.geojson').then(r => r.json()),
      fetch('/geo/us-states.geojson').then(r => r.json()),
      fetch('/geo/brazil-states.geojson').then(r => r.json()),
      fetch('/geo/ar-provinces.geojson').then(r => r.json()),
      fetch('/geo/gb-counties.geojson').then(r => r.json()),
    ]).then(([world, us, br, ar, gb]) => {
      setWorldGeo(world as GeoJSON.FeatureCollection);
      setUsGeo(us as GeoJSON.FeatureCollection);
      setBrGeo(br as GeoJSON.FeatureCollection);
      setArGeo(ar as GeoJSON.FeatureCollection);
      setGbGeo(gb as GeoJSON.FeatureCollection);
    });
  }, []);

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
            usStatesGeoJson={usGeo!}
            brazilStatesGeoJson={brGeo!}
            arGeoJson={arGeo ?? undefined}
            gbGeoJson={gbGeo ?? undefined}
            onToggleStateBorders={handleToggleStateBorders}
            onPlaceClick={place => setSelectedPlace(place)}
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
      {selectedPlace && <PlaceDetailModal place={selectedPlace} onClose={() => setSelectedPlace(null)} />}
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
