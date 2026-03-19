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
}

// ─── Colour palette ───────────────────────────────────────────────────────────
// Contrast is achieved by:
//   • OCEAN vs UNVISITED  : different value (~5× luminance ratio)
//   • UNVISITED vs VISITED: different value AND saturation
//   • HOME country        : warm amber — completely different hue from the blue ocean
const OCEAN_COLOR         = '#0b1628';   // very dark navy   (spec)
const UNVISITED_COLOR     = '#243447';   // medium dark slate (~5× lum vs ocean)
const VISITED_COLOR       = '#1a6090';   // clear steel-blue (visited countries)
const HOME_COLOR          = '#6b4113';   // warm amber        (home country base)
const HOME_VISITED_STATE  = '#1a6090';   // same as VISITED for visited home states
const HOME_UNVISITED_STATE = '#7d5020';  // lighter amber for unvisited home states
const BORDER_COLOR        = '#14243a';
const PIN_COLOR           = '#e8922a';   // orange  — individual places
const HOME_PIN_COLOR      = '#d4a017';   // gold    — places inside home country
const CLUSTER_COLOR       = '#c0392b';   // red     — clusters
const CLUSTER_TEXT        = '#ffffff';

// ─── Clustering ───────────────────────────────────────────────────────────────
const CLUSTER_THRESHOLD_PX = 28;

// Zoom scale breakpoints at which clusters are recomputed.
// Between breakpoints the g-transform moves/scales pins; they stay at the last
// computed radius which may be ≤20% off — imperceptible in practice.
const ZOOM_BREAKPOINTS = [0.5, 0.75, 1, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32];

function nearestBreakpoint(k: number): number {
  return ZOOM_BREAKPOINTS.reduce((best, bp) =>
    Math.abs(bp - k) < Math.abs(best - k) ? bp : best
  );
}

interface Cluster {
  key: string;   // stable identity key for D3 join
  cx: number;
  cy: number;
  places: Place[];
}

/**
 * Greedy O(n²) within-country clustering.
 * Two points merge only when screen-pixel distance ≤ CLUSTER_THRESHOLD_PX
 * AND they belong to the same country.
 */
function buildClusters(
  places: Place[],
  projection: d3.GeoProjection,
  zoomK: number,
): Cluster[] {
  const threshold = CLUSTER_THRESHOLD_PX / zoomK;

  const pts = places.map(p => {
    const c = projection([p.lon, p.lat]);
    return { p, x: c?.[0] ?? 0, y: c?.[1] ?? 0, used: false };
  });

  const clusters: Cluster[] = [];

  for (let i = 0; i < pts.length; i++) {
    if (pts[i].used) continue;
    pts[i].used = true;
    const members = [pts[i]];

    for (let j = i + 1; j < pts.length; j++) {
      if (pts[j].used) continue;
      if (pts[j].p.countryName !== pts[i].p.countryName) continue;
      const dx = pts[j].x - pts[i].x;
      const dy = pts[j].y - pts[i].y;
      if (Math.sqrt(dx * dx + dy * dy) <= threshold) {
        pts[j].used = true;
        members.push(pts[j]);
      }
    }

    clusters.push({
      key: members.map(m => m.p.id).sort((a, b) => a - b).join(','),
      cx: members.reduce((s, m) => s + m.x, 0) / members.length,
      cy: members.reduce((s, m) => s + m.y, 0) / members.length,
      places: members.map(m => m.p),
    });
  }

  return clusters;
}

// ─── Component ────────────────────────────────────────────────────────────────
export default function WorldMap({
  countries,
  places,
  visitedStates,
  geoJson,
  usStatesGeoJson,
  brazilStatesGeoJson,
}: Props) {
  const svgRef       = useRef<SVGSVGElement>(null);
  const tooltipRef   = useRef<HTMLDivElement>(null);
  const projectionRef = useRef<d3.GeoProjection | null>(null);
  const pinsLayerRef  = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const lastBpRef     = useRef<number>(1);  // last breakpoint at which pins were drawn

  // ── renderPins ──────────────────────────────────────────────────────────────
  // Uses a D3 data-join keyed by cluster identity so elements are UPDATED
  // in-place rather than removed and recreated.  This eliminates the 1-frame
  // flicker that occurred when pinsLayer.selectAll('*').remove() fired before
  // the replacement elements were ready.
  const renderPins = (
    clusters: Cluster[],
    pinsLayer: d3.Selection<SVGGElement, unknown, null, undefined>,
    k: number,
  ) => {
    const tooltip = d3.select(tooltipRef.current);

    // ── group-level join (one <g> per cluster) ──────────────────────────────
    const groups = pinsLayer
      .selectAll<SVGGElement, Cluster>('.pin-group')
      .data(clusters, (d: Cluster) => d.key);

    groups.exit().remove();

    const entered = groups.enter()
      .append('g')
      .attr('class', 'pin-group');

    entered.append('circle').attr('class', 'pin-circle');
    entered.append('text').attr('class', 'pin-label');

    const merged = entered.merge(groups);

    // ── update each group ───────────────────────────────────────────────────
    merged.each(function (cluster) {
      const grp      = d3.select(this);
      const isCluster = cluster.places.length > 1;
      const r         = (isCluster ? 7 : 4) / k;
      const fill      = isCluster
        ? CLUSTER_COLOR
        : (cluster.places[0].isHome ? HOME_PIN_COLOR : PIN_COLOR);

      // Circle
      grp.select<SVGCircleElement>('.pin-circle')
        .attr('cx', cluster.cx)
        .attr('cy', cluster.cy)
        .attr('r', r)
        .attr('fill', fill)
        .attr('stroke', '#fff')
        .attr('stroke-width', 0.8 / k)
        .style('cursor', 'pointer')
        .on('mouseover', (event: MouseEvent) => {
          const html = isCluster
            ? `<strong>${cluster.places.length} places in ${cluster.places[0].countryName}</strong><br/>${
                cluster.places.map(p => `${p.flag} ${p.city}`).join('<br/>')
              }`
            : `<strong>${cluster.places[0].flag} ${cluster.places[0].city}</strong><br/>${cluster.places[0].countryName}`;

          tooltip.style('display', 'block').html(html);
          const rect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - rect.left + 12}px`)
            .style('top',  `${event.clientY - rect.top  - 28}px`);
        })
        .on('mousemove', (event: MouseEvent) => {
          const rect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - rect.left + 12}px`)
            .style('top',  `${event.clientY - rect.top  - 28}px`);
        })
        .on('mouseout', () => tooltip.style('display', 'none'));

      // Label (only for clusters)
      grp.select<SVGTextElement>('.pin-label')
        .attr('x', cluster.cx)
        .attr('y', cluster.cy)
        .attr('text-anchor', 'middle')
        .attr('dominant-baseline', 'central')
        .attr('font-size', `${10 / k}px`)
        .attr('fill', CLUSTER_TEXT)
        .attr('pointer-events', 'none')
        .text(isCluster ? cluster.places.length : '');
    });
  };

  // ── main effect ─────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!svgRef.current || !geoJson) return;

    const width  = svgRef.current.clientWidth  || 960;
    const height = svgRef.current.clientHeight || 500;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const projection = d3.geoNaturalEarth1()
      .scale(width / 6.3)
      .translate([width / 2, height / 2]);

    projectionRef.current = projection;
    lastBpRef.current     = 1;

    const pathGenerator = d3.geoPath().projection(projection);

    const visitedAlpha2Set = new Set(countries.filter(c => c.isVisited).map(c => c.isoAlpha2));
    const homeAlpha2Set    = new Set(countries.filter(c => c.isHome).map(c => c.isoAlpha2));
    const visitedBrStates  = new Set(visitedStates.filter(s => s.countryCode === 'BR').map(s => s.stateAbbr));

    const g = svg.append('g');

    // Ocean fill — oversized rect inside the zoom group so panning never
    // reveals the SVG background behind it
    g.append('rect')
      .attr('x', -width * 3)
      .attr('y', -height * 3)
      .attr('width', width * 7)
      .attr('height', height * 7)
      .attr('fill', OCEAN_COLOR);

    // World countries
    g.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .data(geoJson.features)
      .join('path')
      .attr('class', 'country')
      .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
      .attr('fill', f => {
        const a2: string = f.properties?.['ISO3166-1-Alpha-2'] ?? '';
        if (a2 && homeAlpha2Set.has(a2))    return HOME_COLOR;
        if (a2 && visitedAlpha2Set.has(a2)) return VISITED_COLOR;
        return UNVISITED_COLOR;
      })
      .attr('stroke', BORDER_COLOR)
      .attr('stroke-width', 0.5);

    // Brazil states
    if (brazilStatesGeoJson) {
      g.selectAll<SVGPathElement, GeoJSON.Feature>('.br-state')
        .data(brazilStatesGeoJson.features)
        .join('path')
        .attr('class', 'br-state')
        .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
        .attr('fill', f => {
          const abbr: string = (f.properties?.['sigla'] as string) ?? '';
          return visitedBrStates.has(abbr) ? HOME_VISITED_STATE : HOME_UNVISITED_STATE;
        })
        .attr('stroke', BORDER_COLOR)
        .attr('stroke-width', 0.3)
        .attr('opacity', 0.9);
    }

    // US states
    if (usStatesGeoJson && visitedAlpha2Set.has('US')) {
      g.selectAll<SVGPathElement, GeoJSON.Feature>('.us-state')
        .data(usStatesGeoJson.features)
        .join('path')
        .attr('class', 'us-state')
        .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
        .attr('fill', VISITED_COLOR)
        .attr('stroke', BORDER_COLOR)
        .attr('stroke-width', 0.3)
        .attr('opacity', 0.9);
    }

    // Pins layer — inside the zoom group so panning moves pins for free
    const pinsLayer = g.append('g').attr('class', 'pins-layer');
    pinsLayerRef.current = pinsLayer;

    renderPins(buildClusters(places, projection, 1), pinsLayer, 1);

    // ── zoom ────────────────────────────────────────────────────────────────
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 32])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        // Always apply the transform immediately — this is what makes pan/zoom
        // feel instantaneous (no computation on the critical path).
        g.attr('transform', event.transform.toString());

        const k  = event.transform.k;
        const bp = nearestBreakpoint(k);

        // Only recompute clusters when zoom scale crosses a breakpoint.
        // Pure panning (same k, different x/y) is handled entirely by the
        // g-transform above — no pin update needed.
        if (bp !== lastBpRef.current && projectionRef.current && pinsLayerRef.current) {
          lastBpRef.current = bp;
          renderPins(
            buildClusters(places, projectionRef.current, k),
            pinsLayerRef.current,
            k,
          );
        }
      });

    svg.call(zoom);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [countries, places, visitedStates, geoJson, usStatesGeoJson, brazilStatesGeoJson]);

  return (
    <div className={styles.container}>
      <svg ref={svgRef} className={styles.svg} />
      <div ref={tooltipRef} className={styles.tooltip} />
    </div>
  );
}
