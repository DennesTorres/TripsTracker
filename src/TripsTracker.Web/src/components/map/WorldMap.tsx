import { useEffect, useRef, useState, useCallback } from 'react';
import * as d3 from 'd3';
import type { GeoPermissibleObjects } from 'd3';
import type { Country, Place, VisitedState } from '@/types';
import styles from './WorldMap.module.scss';
import { useMapStatus } from '@/context/MapStatusContext';

interface Props {
  countries: Country[];
  places: Place[];
  visitedStates: VisitedState[];
  geoJson: GeoJSON.FeatureCollection;
  /** Dynamic border GeoJSON keyed by country ID. Loaded on demand by MapPage. */
  borderGeoCache: Record<number, GeoJSON.FeatureCollection>;
  onToggleStateBorders?: (countryId: number, show: boolean) => void;
  onPlaceClick?: (placeIds: number[]) => void;
  /** Country name currently loading borders — shown in status bar while operation is in progress. */
  loadingCountryName?: string | null;
}

interface ContextMenuState {
  x: number;
  y: number;
  countryId: number;
  countryName: string;
  currentShow: boolean;
}

// ─── Colours — matched exactly to reference travel-map.html CSS variables ─────
const OCEAN_COLOR  = '#0b1628';  // --ocean
const LAND_COLOR   = '#1c2535';  // --land   (unvisited)
const VIS_COLOR    = '#7a5c1e';  // --vis    (visited countries, dark amber)
const HOME_COLOR   = '#1a4028';  // --home   (home country, dark green)
const C1_COLOR     = '#f0b84a';  // --c1     (1 place pin, gold)
const C2_COLOR     = '#e07830';  // --c2     (2–5 places, orange)
const C3_COLOR     = '#e8354a';  // --c3     (6+ places, red)
const CH_COLOR     = '#2a9058';  // --ch     (home-country places, green)

// Brazil state border colours — from reference .br-state CSS
const BR_STATE_BASE_STROKE  = 'rgba(255,255,255,0.35)';
const BR_STATE_VIS_STROKE   = 'rgba(255,255,255,0.55)';
const BR_STATE_VIS_FILL     = 'rgba(200,151,58,0.14)';
const BR_STATE_BASE_FILL    = 'rgba(0,0,0,0.06)';

const CLUSTER_PX    = 28;   // merge threshold in screen pixels
const BORDERS_ZOOM_MIN = 1.0;  // minimum zoom to show any state borders

// ─── Clustering — mirrors reference cluster() function exactly ────────────────
interface ClusterData {
  x: number;          // SVG x of geographic centroid
  y: number;          // SVG y of geographic centroid
  places: Place[];
}

function buildClusters(
  places: Place[],
  projection: d3.GeoProjection,
  currentK: number,
): ClusterData[] {
  // Project every place to screen space (SVG coords × zoom scale)
  const pts = places.map(p => {
    const c = projection([p.lon, p.lat]);
    return {
      p,
      lon: p.lon,
      lat: p.lat,
      sx: (c?.[0] ?? 0) * currentK,
      sy: (c?.[1] ?? 0) * currentK,
      merged: false,
    };
  });

  const clusters: ClusterData[] = [];

  for (let i = 0; i < pts.length; i++) {
    if (pts[i].merged) continue;

    const c = {
      places: [pts[i]],
      sx: pts[i].sx,
      sy: pts[i].sy,
      country: pts[i].p.countryName,
    };

    for (let j = i + 1; j < pts.length; j++) {
      if (pts[j].merged) continue;
      const dx = pts[j].sx - c.sx;
      const dy = pts[j].sy - c.sy;
      if (
        pts[j].p.countryName === c.country &&
        Math.sqrt(dx * dx + dy * dy) < CLUSTER_PX
      ) {
        c.places.push(pts[j]);
        pts[j].merged = true;
        // Update centroid in screen space (matching reference)
        c.sx = c.places.reduce((s, m) => s + m.sx, 0) / c.places.length;
        c.sy = c.places.reduce((s, m) => s + m.sy, 0) / c.places.length;
      }
    }
    pts[i].merged = true;

    // Pin position: project the geographic centroid (matching reference)
    // proj([avgLon, avgLat]) ≠ avg(proj(lon, lat)) for non-linear projections
    const n = c.places.length;
    const avgLon = c.places.reduce((s, m) => s + m.lon, 0) / n;
    const avgLat = c.places.reduce((s, m) => s + m.lat, 0) / n;
    const projected = projection([avgLon, avgLat]);

    clusters.push({
      x: projected?.[0] ?? 0,
      y: projected?.[1] ?? 0,
      places: c.places.map(m => m.p),
    });
  }

  return clusters;
}

// ─── Separate collocated clusters from different countries ────────────────────
// When two clusters land within OVERLAP_PX screen pixels of each other they are
// pushed apart so both dots remain visible.
function separateCollocatedClusters(clusters: ClusterData[], k: number): ClusterData[] {
  const OVERLAP_PX = 8;
  const PUSH_PX    = 5;
  const positions  = clusters.map(c => ({ x: c.x, y: c.y }));

  for (let i = 0; i < positions.length; i++) {
    for (let j = i + 1; j < positions.length; j++) {
      const dx = positions[j].x - positions[i].x;
      const dy = positions[j].y - positions[i].y;
      const screenDist = Math.sqrt(dx * dx + dy * dy) * k;
      if (screenDist < OVERLAP_PX) {
        const svgPush = PUSH_PX / k;
        const angle   = screenDist < 0.01 ? Math.PI / 2 : Math.atan2(dy, dx);
        positions[i].x -= Math.cos(angle) * svgPush;
        positions[i].y -= Math.sin(angle) * svgPush;
        positions[j].x += Math.cos(angle) * svgPush;
        positions[j].y += Math.sin(angle) * svgPush;
      }
    }
  }

  return clusters.map((c, i) => ({ ...c, x: positions[i].x, y: positions[i].y }));
}

// ─── Component ────────────────────────────────────────────────────────────────
export default function WorldMap({
  countries,
  places,
  visitedStates,
  geoJson,
  borderGeoCache,
  onToggleStateBorders,
  onPlaceClick,
}: Props) {
  const svgRef      = useRef<SVGSVGElement>(null);
  const tooltipRef  = useRef<HTMLDivElement>(null);

  const [contextMenu, setContextMenu] = useState<ContextMenuState | null>(null);
  const [statusLabel, setStatusLabel] = useState<{ country: string; state: string | null }>({ country: '', state: null });
  const { loadingMessage, dismissStatus } = useMapStatus();

  // ── D3 selection refs — set by Effect 1, read by all other effects ───────────
  const projRef          = useRef<d3.GeoProjection | null>(null);
  const pathGeneratorRef = useRef<d3.GeoPath | null>(null);
  const currentKRef      = useRef<number>(1);
  const pinsGRef         = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const statesGRef       = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const countriesGRef    = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);

  // ── Prop refs — always current, safe to read inside D3 event handlers ────────
  // Updated unconditionally on every render, before any effect runs.
  const countriesRef             = useRef<Country[]>(countries);
  countriesRef.current           = countries;
  const placesRef                = useRef<Place[]>(places);
  placesRef.current              = places;
  const visitedStatesRef         = useRef<VisitedState[]>(visitedStates);
  visitedStatesRef.current       = visitedStates;
  const onToggleStateBordersRef  = useRef(onToggleStateBorders);
  onToggleStateBordersRef.current = onToggleStateBorders;
  const onPlaceClickRef          = useRef(onPlaceClick);
  onPlaceClickRef.current        = onPlaceClick;

  const closeContextMenu = useCallback(() => setContextMenu(null), []);

  // ── recluster — reads entirely from refs; safe to call from stale closures ───
  const recluster = () => {
    if (!projRef.current || !pinsGRef.current) return;
    const proj = projRef.current;
    const pinsG = pinsGRef.current;
    const k = currentKRef.current;
    const tooltip = d3.select(tooltipRef.current);
    const currentPlaces = placesRef.current;

    pinsG.selectAll('.mg').remove();

    const countryTotals = currentPlaces.reduce<Record<number, number>>((acc, p) => {
      acc[p.countryId] = (acc[p.countryId] ?? 0) + 1;
      return acc;
    }, {});

    const clusters = separateCollocatedClusters(buildClusters(currentPlaces, proj, k), k);
    const s = 1 / k; // counter-scale so dots stay 4px on screen

    clusters.forEach(c => {
      const n = c.places.length;
      const hasHome = c.places.some(p => p.isHome);

      const dotColor = hasHome ? CH_COLOR : n >= 6 ? C3_COLOR : n >= 2 ? C2_COLOR : C1_COLOR;

      const mg = pinsG.append('g')
        .attr('class', 'mg')
        .attr('transform', `translate(${c.x},${c.y}) scale(${s})`);

      mg.append('circle')
        .attr('r', 4)
        .attr('fill', dotColor)
        .attr('stroke', 'rgba(255,255,255,0.3)')
        .attr('stroke-width', 0.4);

      if (n > 1) {
        mg.append('text')
          .attr('text-anchor', 'middle')
          .attr('dominant-baseline', 'central')
          .attr('font-size', '5px')
          .attr('fill', '#fff')
          .attr('pointer-events', 'none')
          .text(n);
      }

      mg.append('circle')
        .attr('r', 12)
        .attr('fill', 'transparent')
        .style('cursor', 'pointer')
        .on('mouseover', (event: MouseEvent) => {
          const p0 = c.places[0];
          const cityLabel = (p: Place) => {
            const abbr = p.stateAbbr && !/^\d+$/.test(p.stateAbbr) ? p.stateAbbr : null;
            const state = abbr ?? (p.stateName ? p.stateName.split(' ')[0] : null);
            return state ? `${p.city} (${state})` : p.city;
          };
          const total = countryTotals[p0.countryId] ?? n;
          const countLabel = n < total ? `${n}/${total}` : `${n}`;
          const html = n === 1
            ? `<strong>${cityLabel(p0)}</strong><br/>${p0.countryName}`
            : `<strong>${countLabel} places in ${p0.countryName}</strong><br/>${
                c.places.map(p => cityLabel(p)).join('<br/>')
              }`;
          tooltip.style('display', 'block').html(html);
          const rect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - rect.left + 14}px`)
            .style('top', `${event.clientY - rect.top - 32}px`);
        })
        .on('mousemove', (event: MouseEvent) => {
          const rect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - rect.left + 14}px`)
            .style('top', `${event.clientY - rect.top - 32}px`);
        })
        .on('mouseout', () => tooltip.style('display', 'none'))
        .on('click', () => {
          if (onPlaceClickRef.current) onPlaceClickRef.current(c.places.map(p => p.id));
        });
    });
  };

  // ── updateStateBorderVisibility — show/hide all state borders based on zoom ──
  const updateStateBorderVisibility = () => {
    if (!statesGRef.current) return;
    const show = currentKRef.current >= BORDERS_ZOOM_MIN;
    const paths = statesGRef.current.selectAll<SVGPathElement, GeoJSON.Feature>('.brs');
    if (show) paths.style('display', null); else paths.style('display', 'none');
  };

  // ── Effect 1: setup — ocean, country paths (no fill), borders layer, pins, zoom
  // Runs only when geoJson changes (once on mount in practice).
  // svg.selectAll('*').remove() lives ONLY here — country fills never disappear.
  useEffect(() => {
    if (!svgRef.current || !geoJson) return;

    const width  = svgRef.current.clientWidth  || 960;
    const height = svgRef.current.clientHeight || 500;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const projection = d3.geoNaturalEarth1()
      .scale(Math.min(width / 6.1, height / 3.2))
      .translate([width / 2, height / 2]);

    projRef.current = projection;
    const savedTransform = svgRef.current ? d3.zoomTransform(svgRef.current) : d3.zoomIdentity;
    currentKRef.current = savedTransform.k;

    const pathGenerator = d3.geoPath().projection(projection);
    pathGeneratorRef.current = pathGenerator;

    const g = svg.append('g').attr('transform', savedTransform.toString());

    // Ocean sphere
    const sphereD = pathGenerator({ type: 'Sphere' as const }) ?? '';
    g.append('path').attr('fill', OCEAN_COLOR).attr('d', sphereD);

    // Countries layer — paths created here, fills applied by Effect 2
    const countriesG = g.append('g');
    countriesGRef.current = countriesG;
    countriesG.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .data(geoJson.features)
      .join('path')
      .attr('class', 'country')
      .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
      .attr('fill', LAND_COLOR)  // Effect 2 updates to VIS/HOME colors
      .attr('stroke', 'rgba(0,0,0,0.4)')
      .attr('stroke-width', 0.35)
      .attr('vector-effect', 'non-scaling-stroke')
      .on('mouseover', (_event: MouseEvent, f: GeoJSON.Feature) => {
        const a2: string = f.properties?.['ISO_A2'] ?? '';
        const country = countriesRef.current.find(c => c.isoAlpha2 === a2);
        if (country) setStatusLabel({ country: country.name, state: null });
      })
      .on('mouseout', () => setStatusLabel({ country: '', state: null }))
      .on('contextmenu', (event: MouseEvent, f: GeoJSON.Feature) => {
        event.preventDefault();
        if (!onToggleStateBordersRef.current) return;
        const a2: string = f.properties?.['ISO_A2'] ?? '';
        const country = countriesRef.current.find(c => c.isoAlpha2 === a2);
        if (!country) return;
        const rect = svgRef.current!.getBoundingClientRect();
        setContextMenu({
          x: event.clientX - rect.left,
          y: event.clientY - rect.top,
          countryId: country.id,
          countryName: country.name,
          currentShow: country.showStateBorders,
        });
      });

    // State borders layer — populated by Effect 4
    const statesG = g.append('g');
    statesGRef.current = statesG;

    // Sphere outline (on top of countries and borders)
    g.append('path')
      .attr('fill', 'none')
      .attr('stroke', 'rgba(255,255,255,0.1)')
      .attr('stroke-width', 0.7)
      .attr('vector-effect', 'non-scaling-stroke')
      .attr('d', sphereD);

    // Pins layer (topmost)
    const pinsG = g.append('g');
    pinsGRef.current = pinsG;
    recluster();

    // ── zoom ──
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.8, 500])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        const kChanged = event.transform.k !== currentKRef.current;
        currentKRef.current = event.transform.k;
        g.attr('transform', event.transform.toString());
        if (kChanged) {
          recluster();
          updateStateBorderVisibility();
        }
      });

    svg.call(zoom);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [geoJson]);

  // ── Effect 2: color update — in-place fill update, never rebuilds SVG ────────
  useEffect(() => {
    const countriesG = countriesGRef.current;
    if (!countriesG) return;

    const visitedAlpha2Set = new Set(countries.filter(c => c.isVisited).map(c => c.isoAlpha2));
    const homeAlpha2Set    = new Set(countries.filter(c => c.isHome).map(c => c.isoAlpha2));

    countriesG.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .attr('fill', f => {
        const a2: string = f.properties?.['ISO_A2'] ?? '';
        if (a2 && homeAlpha2Set.has(a2))    return HOME_COLOR;
        if (a2 && visitedAlpha2Set.has(a2)) return VIS_COLOR;
        return LAND_COLOR;
      });
  }, [countries]);

  // ── Effect 3: pins update — recluster whenever places changes ────────────────
  useEffect(() => {
    recluster();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [places]);

  // ── Effect 4: border effect — updates statesG only, never clears main SVG ───
  useEffect(() => {
    const statesG = statesGRef.current;
    const pathGenerator = pathGeneratorRef.current;
    if (!statesG || !pathGenerator) return;

    statesG.selectAll('.brs').remove();

    countries
      .filter(c => c.showStateBorders)
      .forEach(country => {
        const geo = borderGeoCache[country.id];
        if (!geo) return;

        // Build visited set: GADM ISO_1 format is "{A2}-{stateAbbr}"
        const visitedSet = new Set(
          visitedStates
            .filter(s => s.countryId === country.id)
            .map(s => `${country.isoAlpha2}-${s.stateAbbr}`)
        );
        const cssClass = `brs-${country.isoAlpha2.toLowerCase()}`;

        statesG.selectAll<SVGPathElement, GeoJSON.Feature>(`.${cssClass}`)
          .data(geo.features)
          .join('path')
          .attr('class', `brs ${cssClass}`)
          .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
          .attr('fill', f => {
            const iso: string = (f.properties?.['ISO_1'] as string) ?? '';
            return visitedSet.has(iso) ? BR_STATE_VIS_FILL : BR_STATE_BASE_FILL;
          })
          .attr('stroke', f => {
            const iso: string = (f.properties?.['ISO_1'] as string) ?? '';
            return visitedSet.has(iso) ? BR_STATE_VIS_STROKE : BR_STATE_BASE_STROKE;
          })
          .attr('stroke-width', 0.5)
          .attr('vector-effect', 'non-scaling-stroke')
          .style('display', 'none')
          .on('mouseover', (_event: MouseEvent, f: GeoJSON.Feature) => {
            const stateName: string = (f.properties?.['NAME_1'] as string) ?? '';
            setStatusLabel({ country: country.name, state: stateName || null });
          })
          .on('mouseout', () => {
            setStatusLabel({ country: '', state: null });
          })
          .on('contextmenu', (event: MouseEvent) => {
            event.preventDefault();
            if (!onToggleStateBordersRef.current) return;
            const rect = svgRef.current!.getBoundingClientRect();
            setContextMenu({
              x: event.clientX - rect.left,
              y: event.clientY - rect.top,
              countryId: country.id,
              countryName: country.name,
              currentShow: country.showStateBorders,
            });
          });

        dismissStatus(country.id);
      });

    updateStateBorderVisibility();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [borderGeoCache, countries, visitedStates, dismissStatus]);

  return (
    <div className={styles.container} onClick={closeContextMenu}>
      <svg ref={svgRef} className={styles.svg} />
      <div ref={tooltipRef} className={styles.tooltip} />
      {contextMenu && (
        <div
          className={styles.contextMenu}
          style={{ left: contextMenu.x, top: contextMenu.y }}
          onClick={e => e.stopPropagation()}
        >
          <button
            onClick={() => {
              onToggleStateBordersRef.current?.(contextMenu.countryId, !contextMenu.currentShow);
              setContextMenu(null);
            }}
          >
            {contextMenu.currentShow ? 'Disable state borders' : 'Enable state borders'} — {contextMenu.countryName}
          </button>
        </div>
      )}
      <div className={styles.statusBar}>
        {loadingMessage ? (
          <span style={{ fontStyle: 'italic' }}>{loadingMessage}</span>
        ) : statusLabel.country ? (
          <>
            <span>{statusLabel.country}</span>
            {statusLabel.state && (
              <>
                <span style={{ color: '#6a8aaa' }}>›</span>
                <span>{statusLabel.state}</span>
              </>
            )}
          </>
        ) : null}
      </div>
    </div>
  );
}
