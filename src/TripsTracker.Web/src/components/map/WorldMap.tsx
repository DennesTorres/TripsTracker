import { useEffect, useRef } from 'react';
import * as d3 from 'd3';
import type { GeoPermissibleObjects } from 'd3';
import type { Country, Place, VisitedState } from '@/types';
import styles from './WorldMap.module.scss';

interface Props {
  countries: Country[];
  places: Place[];
  visitedStates: VisitedState[];
  geoJson: GeoJSON.FeatureCollection;
  usStatesGeoJson: GeoJSON.FeatureCollection;
  brazilStatesGeoJson: GeoJSON.FeatureCollection;
  arGeoJson?: GeoJSON.FeatureCollection;
  gbGeoJson?: GeoJSON.FeatureCollection;
  onToggleStateBorders?: (countryId: number, show: boolean) => void;
  onPlaceClick?: (place: Place) => void;
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
const BR_STATE_BASE_STROKE  = 'rgba(200,151,58,0.35)';
const BR_STATE_VIS_STROKE   = 'rgba(240,184,74,0.65)';
const BR_STATE_VIS_FILL     = 'rgba(200,151,58,0.14)';
const BR_STATE_BASE_FILL    = 'rgba(0,0,0,0.06)';

const CLUSTER_PX  = 28;   // merge threshold in screen pixels
const BR_ZOOM_MIN = 2.0;  // minimum zoom to show state borders

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
  usStatesGeoJson,
  brazilStatesGeoJson,
  arGeoJson,
  gbGeoJson,
  onToggleStateBorders,
  onPlaceClick,
}: Props) {
  const svgRef           = useRef<SVGSVGElement>(null);
  const tooltipRef       = useRef<HTMLDivElement>(null);
  const onPlaceClickRef  = useRef(onPlaceClick);
  onPlaceClickRef.current = onPlaceClick;

  // Mutable refs used inside zoom handler (never trigger re-render)
  const projRef     = useRef<d3.GeoProjection | null>(null);
  const currentKRef = useRef<number>(1);
  const pinsGRef    = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const brStatesGRef = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);

  // ── recluster — mirrors reference recluster() ──────────────────────────────
  const recluster = () => {
    if (!projRef.current || !pinsGRef.current) return;
    const proj = projRef.current;
    const pinsG = pinsGRef.current;
    const k = currentKRef.current;
    const tooltip = d3.select(tooltipRef.current);

    pinsG.selectAll('.mg').remove();

    const clusters = separateCollocatedClusters(buildClusters(places, proj, k), k);
    const s = 1 / k; // counter-scale so dots stay 4px on screen

    clusters.forEach(c => {
      const n = c.places.length;
      const hasHome = c.places.some(p => p.isHome);

      // Pin colour class — matching reference: home > 6+ > 2-5 > 1
      const dotColor = hasHome ? CH_COLOR : n >= 6 ? C3_COLOR : n >= 2 ? C2_COLOR : C1_COLOR;
      const strokeColor = hasHome ? CH_COLOR : n >= 6 ? C3_COLOR : n >= 2 ? C2_COLOR : C1_COLOR;

      // Group: translate to SVG position, counter-scale (reference pattern)
      const mg = pinsG.append('g')
        .attr('class', 'mg')
        .attr('transform', `translate(${c.x},${c.y}) scale(${s})`);

      // Dot — fixed 4px radius in screen space
      mg.append('circle')
        .attr('r', 4)
        .attr('fill', dotColor)
        .attr('stroke', 'rgba(255,255,255,0.3)')
        .attr('stroke-width', 0.4);

      // Cluster count label (when >1)
      if (n > 1) {
        mg.append('text')
          .attr('text-anchor', 'middle')
          .attr('dominant-baseline', 'central')
          .attr('font-size', '5px')
          .attr('fill', '#fff')
          .attr('pointer-events', 'none')
          .text(n);
      }

      // Hit area for hover (reference uses r=12)
      mg.append('circle')
        .attr('r', 12)
        .attr('fill', 'transparent')
        .style('cursor', 'pointer')
        .on('mouseover', (event: MouseEvent) => {
          const p0 = c.places[0];
          // Show stateAbbr only for letter-based codes (e.g. "SP", "RJ").
          // Numeric ISO codes like Poland's "22" are not meaningful to display.
          const cityLabel = (p: Place) => {
            const abbr = p.stateAbbr && !/^\d+$/.test(p.stateAbbr) ? p.stateAbbr : null;
            const state = abbr ?? (p.stateName ? p.stateName.split(' ')[0] : null);
            return state ? `${p.city} (${state})` : p.city;
          };
          const html = n === 1
            ? `<strong>${cityLabel(p0)}</strong><br/>${p0.countryName}`
            : `<strong>${n} places in ${p0.countryName}</strong><br/>${
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
          tooltip.style('display', 'none');
          onPlaceClickRef.current?.(c.places[0]);
        });

      // Store colour on stroke for rescale (unused here but matches reference shape)
      void strokeColor;
    });
  };

  // ── updateBrStates — show/hide state borders based on zoom ────────────────
  const updateBrStates = () => {
    if (!brStatesGRef.current) return;
    const show = currentKRef.current >= BR_ZOOM_MIN;
    const brPaths = brStatesGRef.current.selectAll<SVGPathElement, GeoJSON.Feature>('.brs');
    if (show) {
      brPaths.style('display', null);
    } else {
      brPaths.style('display', 'none');
    }
  };

  // ── main effect ─────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!svgRef.current || !geoJson) return;

    const width  = svgRef.current.clientWidth  || 960;
    const height = svgRef.current.clientHeight || 500;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    // Projection — reference formula: Math.min(w/6.1, h/3.2)
    const projection = d3.geoNaturalEarth1()
      .scale(Math.min(width / 6.1, height / 3.2))
      .translate([width / 2, height / 2]);

    projRef.current    = projection;
    currentKRef.current = 1;

    const pathGenerator = d3.geoPath().projection(projection);

    const visitedAlpha2Set = new Set(countries.filter(c => c.isVisited).map(c => c.isoAlpha2));
    const homeAlpha2Set    = new Set(countries.filter(c => c.isHome).map(c => c.isoAlpha2));
    // Build sets for countries with state borders enabled
    const showBordersCountries = new Set(countries.filter(c => c.showStateBorders).map(c => c.isoAlpha2));
    const brCountry = countries.find(c => c.isoAlpha2 === 'BR');
    const usCountry = countries.find(c => c.isoAlpha2 === 'US');
    const arCountry = countries.find(c => c.isoAlpha2 === 'AR');
    const gbCountry = countries.find(c => c.isoAlpha2 === 'GB');
    const visitedBrStates  = new Set(visitedStates.filter(s => s.countryId === brCountry?.id).map(s => s.stateAbbr));
    const visitedUsStates  = new Set(visitedStates.filter(s => s.countryId === usCountry?.id).map(s => s.stateAbbr));
    // GADM ISO_1 uses "AR-B" format; Nominatim returns "B" — add prefix
    const visitedArStates  = new Set(visitedStates.filter(s => s.countryId === arCountry?.id).map(s => `AR-${s.stateAbbr}`));
    // GADM admin-1 for GB is counties; Nominatim returns home nations — no visited highlighting
    const visitedGbStates  = new Set<string>();
    void gbCountry; // reference kept for future use

    const g = svg.append('g');

    // Ocean sphere — matches reference: draw actual sphere shape as ocean fill
    const sphereD = pathGenerator({ type: 'Sphere' as const }) ?? '';
    g.append('path')
      .attr('fill', OCEAN_COLOR)
      .attr('d', sphereD);

    // Countries
    const countriesG = g.append('g');
    countriesG.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .data(geoJson.features)
      .join('path')
      .attr('class', 'country')
      .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
      .attr('fill', f => {
        const a2: string = f.properties?.['ISO_A2'] ?? '';
        if (a2 && homeAlpha2Set.has(a2))    return HOME_COLOR;
        if (a2 && visitedAlpha2Set.has(a2)) return VIS_COLOR;
        return LAND_COLOR;
      })
      .attr('stroke', 'rgba(0,0,0,0.4)')
      .attr('stroke-width', 0.35)
      .attr('vector-effect', 'non-scaling-stroke')
      .on('contextmenu', (event: MouseEvent, f: GeoJSON.Feature) => {
        event.preventDefault();
        if (!onToggleStateBorders) return;
        const a2: string = f.properties?.['ISO_A2'] ?? '';
        const country = countries.find(c => c.isoAlpha2 === a2);
        if (country) onToggleStateBorders(country.id, !country.showStateBorders);
      });

    // State borders — rendered for countries with showStateBorders enabled
    // Hidden until zoom >= BR_ZOOM_MIN
    const statesG = g.append('g');
    brStatesGRef.current = statesG;

    const renderStateBorders = (
      geoData: GeoJSON.FeatureCollection,
      abbrProp: string,
      visitedSet: Set<string>,
      cssClass: string,
    ) => {
      statesG.selectAll<SVGPathElement, GeoJSON.Feature>(`.${cssClass}`)
        .data(geoData.features)
        .join('path')
        .attr('class', `brs ${cssClass}`)
        .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
        .attr('fill', f => {
          const abbr: string = (f.properties?.[abbrProp] as string) ?? '';
          return visitedSet.has(abbr) ? BR_STATE_VIS_FILL : BR_STATE_BASE_FILL;
        })
        .attr('stroke', f => {
          const abbr: string = (f.properties?.[abbrProp] as string) ?? '';
          return visitedSet.has(abbr) ? BR_STATE_VIS_STROKE : BR_STATE_BASE_STROKE;
        })
        .attr('stroke-width', 0.5)
        .attr('vector-effect', 'non-scaling-stroke')
        .attr('pointer-events', 'none')
        .style('display', 'none');
    };

    if (brazilStatesGeoJson && showBordersCountries.has('BR')) {
      renderStateBorders(brazilStatesGeoJson, 'sigla', visitedBrStates, 'brs-br');
    }
    if (usStatesGeoJson && showBordersCountries.has('US')) {
      renderStateBorders(usStatesGeoJson, 'STUSPS', visitedUsStates, 'brs-us');
    }
    if (arGeoJson && showBordersCountries.has('AR')) {
      renderStateBorders(arGeoJson, 'ISO_1', visitedArStates, 'brs-ar');
    }
    if (gbGeoJson && showBordersCountries.has('GB')) {
      renderStateBorders(gbGeoJson, 'ISO_1', visitedGbStates, 'brs-gb');
    }

    // Sphere outline (reference draws this on top of countries)
    g.append('path')
      .attr('fill', 'none')
      .attr('stroke', 'rgba(255,255,255,0.1)')
      .attr('stroke-width', 0.7)
      .attr('vector-effect', 'non-scaling-stroke')
      .attr('d', sphereD);

    // Pins layer (topmost)
    const pinsG = g.append('g');
    pinsGRef.current = pinsG;

    // Initial pin render
    recluster();

    // ── zoom — recluster only when k changes (pan-only events skip rebuild) ──
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.8, 500])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        const kChanged = event.transform.k !== currentKRef.current;
        currentKRef.current = event.transform.k;
        g.attr('transform', event.transform.toString());
        if (kChanged) {
          recluster();
          updateBrStates();
        }
      });

    svg.call(zoom);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [countries, places, visitedStates, geoJson, usStatesGeoJson, brazilStatesGeoJson, arGeoJson, gbGeoJson]);

  return (
    <div className={styles.container}>
      <svg ref={svgRef} className={styles.svg} />
      <div ref={tooltipRef} className={styles.tooltip} />
    </div>
  );
}
