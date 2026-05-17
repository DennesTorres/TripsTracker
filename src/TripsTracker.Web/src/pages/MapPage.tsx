import { useCallback, useEffect, useRef, useState } from 'react';
import WorldMap from '@/components/map/WorldMap';
import StatsBar from '@/components/map/StatsBar';
import AddPlaceForm from '@/pages/places/AddPlaceForm';
import PlacePopup from '@/pages/places/PlacePopup';
import PlaceDetailPanel from '@/pages/places/PlaceDetailPanel';
import ExplorePanel from '@/pages/places/ExplorePanel';
import ShareModal from '@/components/share/ShareModal';
import DiscoverMapsModal from '@/components/share/DiscoverMapsModal';
import { usePlaces, useCountries, useVisitedStates, useSetStateBorders, useCreatePlace, useExploreSearch } from '@/api/hooks';
import type { ExploreLocation, Place } from '@/types';
import { Share2, Compass } from 'lucide-react';
import styles from './MapPage.module.scss';

interface DetailInfo {
  city: string;
  stateName?: string | null;
  countryName: string;
  countryId: number;
  placeId?: number;
}

interface Props {
  exploreCity?: string | null;
  onExploreCityConsumed?: () => void;
}

export default function MapPage({ exploreCity, onExploreCityConsumed }: Props) {
  const { data: places = [], isLoading: placesLoading, isError: placesError, refetch: refetchPlaces } = usePlaces();
  const { data: countries = [], isLoading: countriesLoading, isError: countriesError, refetch: refetchCountries } = useCountries();
  const { data: visitedStates = [], isLoading: statesLoading, isError: statesError, refetch: refetchStates } = useVisitedStates();
  const setStateBorders = useSetStateBorders();
  const createPlace = useCreatePlace();

  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [usGeo, setUsGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [brGeo, setBrGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [arGeo, setArGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [gbGeo, setGbGeo] = useState<GeoJSON.FeatureCollection | null>(null);
  const [adding, setAdding] = useState(false);
  const [sharing, setSharing] = useState(false);
  const [discovering, setDiscovering] = useState(false);
  const [exploreQuery, setExploreQuery] = useState('');
  const [explorePin, setExplorePin] = useState<ExploreLocation | null>(null);
  const [popup, setPopup] = useState<{ places: Place[]; x: number; y: number } | null>(null);
  const [activeDetail, setActiveDetail] = useState<DetailInfo | null>(null);
  const autoSelectRef = useRef(false);
  const pendingExploreCityRef = useRef<string | null>(null);

  const { data: exploreResults = [] } = useExploreSearch(exploreQuery);

  useEffect(() => {
    if (!exploreCity) return;
    pendingExploreCityRef.current = exploreCity;
    autoSelectRef.current = true;
    setExploreQuery(exploreCity);
    onExploreCityConsumed?.();
    // If React Query already has cached results for this query, exploreResults won't change
    // reference → the [exploreResults] effect won't re-fire → auto-select would be blocked.
    // Check immediately: if matching results are already present, select now.
    if (exploreResults.length > 0) {
      const cityLC = exploreCity.toLowerCase();
      const first = exploreResults[0].city.toLowerCase();
      if (first.includes(cityLC) || cityLC.includes(first)) {
        autoSelectRef.current = false;
        pendingExploreCityRef.current = null;
        handleExploreSelectCity(exploreResults[0]);
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [exploreCity]);

  useEffect(() => {
    if (exploreQuery) {
      setExplorePin(null);
      setActiveDetail(null);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [exploreQuery]);

  useEffect(() => {
    if (autoSelectRef.current && exploreResults.length > 0) {
      const expected = pendingExploreCityRef.current?.toLowerCase() ?? '';
      const first = exploreResults[0].city.toLowerCase();
      // Only auto-select if results match the city we're looking for (prevents stale-result races)
      if (!expected || first.includes(expected) || expected.includes(first)) {
        autoSelectRef.current = false;
        pendingExploreCityRef.current = null;
        handleExploreSelectCity(exploreResults[0]);
        setExploreQuery('');
      }
    }
  }, [exploreResults]);

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

  function handleExploreSelectCity(loc: ExploreLocation) {
    setExplorePin(loc);
    setActiveDetail({ city: loc.city, stateName: loc.stateName, countryName: loc.countryName, countryId: loc.countryId });
  }

  function handleAddFromExplore() {
    if (!activeDetail) return;
    const existing = places.find(
      p => p.city.toLowerCase() === activeDetail.city.toLowerCase() && p.countryId === activeDetail.countryId
    );
    if (existing) {
      setActiveDetail(d => d ? { ...d, placeId: existing.id } : d);
      return;
    }
    const country = countries.find(c => c.id === activeDetail.countryId);
    if (!country) return;
    createPlace.mutate(
      { cityName: activeDetail.city, countryIsoAlpha2: country.isoAlpha2, isHome: false },
      { onSuccess: place => setActiveDetail(d => d ? { ...d, placeId: place.id } : d) }
    );
  }

  function handleCloseDetail() {
    if (activeDetail?.placeId === undefined) setExplorePin(null);
    setActiveDetail(null);
  }

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
            onPlaceClick={(places, screenX, screenY) => setPopup({ places, x: screenX, y: screenY })}
            temporaryPin={explorePin ? { lat: explorePin.lat, lon: explorePin.lon, label: explorePin.city } : undefined}
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
        <ExplorePanel
          query={exploreQuery}
          onQueryChange={setExploreQuery}
          onPinLocation={setExplorePin}
          onSelectCity={handleExploreSelectCity}
        />
        {popup && (
          <>
            <div style={{ position: 'absolute', inset: 0, zIndex: 29 }} onClick={() => setPopup(null)} />
            <PlacePopup
              places={popup.places}
              x={popup.x}
              y={popup.y}
              onClose={() => setPopup(null)}
              onSeeMore={place => {
                setPopup(null);
                setActiveDetail({ city: place.city, stateName: place.stateName, countryName: place.countryName, countryId: place.countryId, placeId: place.id });
              }}
            />
          </>
        )}
        {activeDetail && (
          <PlaceDetailPanel
            city={activeDetail.city}
            stateName={activeDetail.stateName}
            countryName={activeDetail.countryName}
            countryId={activeDetail.countryId}
            placeId={activeDetail.placeId}
            onAddToMyPlaces={activeDetail.placeId === undefined ? handleAddFromExplore : undefined}
            onClose={handleCloseDetail}
          />
        )}
      </div>
      {!isLoading && (
        <StatsBar countries={countries} places={places} />
      )}
      {adding && (
        <AddPlaceForm
          initialCity=""
          onClose={() => setAdding(false)}
          onExplore={city => {
            setAdding(false);
            pendingExploreCityRef.current = city;
            autoSelectRef.current = true;
            setExploreQuery(city);
          }}
        />
      )}
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
