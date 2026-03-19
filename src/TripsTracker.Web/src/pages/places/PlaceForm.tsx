import { useEffect, useRef, useState } from 'react';
import * as d3 from 'd3';
import type { GeoPermissibleObjects } from 'd3';
import { useCreatePlace, useUpdatePlace } from '@/api/hooks';
import type { Place, SavePlace } from '@/types';
import styles from './PlaceForm.module.scss';

interface Props {
  place: Place | null;
  onClose: () => void;
}

const COUNTRY_FLAGS: Record<string, string> = {
  'United Kingdom': '🏴󠁧󠁢󠁳󠁣󠁴󠁿', 'Portugal': '🇵🇹', 'Spain': '🇪🇸', 'Italy': '🇮🇹',
  'Malta': '🇲🇹', 'Switzerland': '🇨🇭', 'Germany': '🇩🇪', 'Austria': '🇦🇹',
  'Hungary': '🇭🇺', 'Romania': '🇷🇴', 'Bulgaria': '🇧🇬', 'Croatia': '🇭🇷',
  'Sweden': '🇸🇪', 'Greece': '🇬🇷', 'Turkey': '🇹🇷', 'Israel': '🇮🇱',
  'Cape Verde': '🇨🇻', 'United States': '🇺🇸', 'Argentina': '🇦🇷', 'Brazil': '🇧🇷',
};

export default function PlaceForm({ place, onClose }: Props) {
  const create = useCreatePlace();
  const update = useUpdatePlace();

  const [lon, setLon] = useState(place?.lon ?? 0);
  const [lat, setLat] = useState(place?.lat ?? 0);
  const [city, setCity] = useState(place?.city ?? '');
  const [countryName, setCountryName] = useState(place?.countryName ?? '');
  const [flag, setFlag] = useState(place?.flag ?? '');
  const [isHome, setIsHome] = useState(place?.isHome ?? false);

  const mapRef = useRef<SVGSVGElement>(null);
  const [worldGeo, setWorldGeo] = useState<GeoJSON.FeatureCollection | null>(null);

  useEffect(() => {
    fetch('/geo/world-110m.geojson').then(r => r.json()).then(d => setWorldGeo(d as GeoJSON.FeatureCollection));
  }, []);

  useEffect(() => {
    if (!mapRef.current || !worldGeo) return;

    const width = mapRef.current.clientWidth || 500;
    const height = mapRef.current.clientHeight || 260;
    const svg = d3.select(mapRef.current);
    svg.selectAll('*').remove();

    const projection = d3.geoNaturalEarth1()
      .scale(width / 6.3)
      .translate([width / 2, height / 2]);
    const path = d3.geoPath().projection(projection);

    const g = svg.append('g');

    g.selectAll<SVGPathElement, GeoJSON.Feature>('path')
      .data(worldGeo.features)
      .join('path')
      .attr('d', f => path(f as GeoPermissibleObjects) ?? '')
      .attr('fill', '#1e293b')
      .attr('stroke', '#334155')
      .attr('stroke-width', 0.5);

    // Pin for current coords
    const pinCoords = projection([lon, lat]);
    if (pinCoords) {
      g.append('circle')
        .attr('cx', pinCoords[0])
        .attr('cy', pinCoords[1])
        .attr('r', 6)
        .attr('fill', '#f97316')
        .attr('stroke', '#fff')
        .attr('stroke-width', 1.5);
    }

    svg.style('cursor', 'crosshair').on('click', (event: MouseEvent) => {
      const [mx, my] = d3.pointer(event);
      const coords = projection.invert?.([mx, my]);
      if (coords) {
        setLon(parseFloat(coords[0].toFixed(4)));
        setLat(parseFloat(coords[1].toFixed(4)));
      }
    });
  }, [worldGeo, lon, lat]);

  // Auto-fill flag from country name
  useEffect(() => {
    const f = COUNTRY_FLAGS[countryName];
    if (f) setFlag(f);
  }, [countryName]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const dto: SavePlace = { lon, lat, flag, countryName, city, isHome };
    if (place) {
      update.mutate({ id: place.id, dto }, { onSuccess: onClose });
    } else {
      create.mutate(dto, { onSuccess: onClose });
    }
  };

  const isPending = create.isPending || update.isPending;

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <h3>{place ? 'Edit place' : 'Add place'}</h3>
          <button className={styles.close} onClick={onClose}>×</button>
        </div>

        <svg ref={mapRef} className={styles.miniMap} />

        <form onSubmit={handleSubmit} className={styles.form}>
          <div className={styles.coords}>
            <label>
              Lon
              <input type="number" step="0.0001" value={lon} onChange={e => setLon(parseFloat(e.target.value))} required />
            </label>
            <label>
              Lat
              <input type="number" step="0.0001" value={lat} onChange={e => setLat(parseFloat(e.target.value))} required />
            </label>
          </div>

          <label>
            City
            <input type="text" value={city} onChange={e => setCity(e.target.value)} required />
          </label>

          <label>
            Country
            <input
              type="text"
              value={countryName}
              onChange={e => setCountryName(e.target.value)}
              list="country-list"
              required
            />
            <datalist id="country-list">
              {Object.keys(COUNTRY_FLAGS).map(c => <option key={c} value={c} />)}
            </datalist>
          </label>

          <label>
            Flag
            <input type="text" value={flag} onChange={e => setFlag(e.target.value)} required />
          </label>

          <label className={styles.checkLabel}>
            <input type="checkbox" checked={isHome} onChange={e => setIsHome(e.target.checked)} />
            Home location
          </label>

          <div className={styles.actions}>
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className={styles.saveBtn} disabled={isPending}>
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
