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

// Dark theme — matches reference travel-map design
// Colours are deliberately distinct in luminance so they separate on dark monitors
const OCEAN_COLOR      = '#0b1628';   // darkest — spec
const UNVISITED_COLOR  = '#1c2535';   // dark slate — spec
const HOME_COLOR       = '#16375c';   // medium navy (home country base)
const VISITED_COLOR    = '#1d6fba';   // clear steel blue (visited countries)
const BORDER_COLOR     = '#0d1e30';
const PIN_COLOR        = '#e8922a';   // orange — individual visited places
const HOME_PIN_COLOR   = '#d4a017';   // gold — places inside home country
const CLUSTER_COLOR    = '#c0392b';   // red — clusters
const CLUSTER_TEXT     = '#fff';

// Threshold in screen pixels for within-country clustering
const CLUSTER_THRESHOLD_PX = 28;

interface Cluster {
  cx: number;
  cy: number;
  places: Place[];
}

/**
 * Greedy O(n²) within-country clustering.
 * Two points cluster only when their SVG-space distance ≤ CLUSTER_THRESHOLD_PX / zoomK
 * AND they belong to the same country.  Points from different countries never merge.
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
      cx: members.reduce((s, m) => s + m.x, 0) / members.length,
      cy: members.reduce((s, m) => s + m.y, 0) / members.length,
      places: members.map(m => m.p),
    });
  }

  return clusters;
}

export default function WorldMap({
  countries,
  places,
  visitedStates,
  geoJson,
  usStatesGeoJson,
  brazilStatesGeoJson,
}: Props) {
  const svgRef    = useRef<SVGSVGElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const projectionRef  = useRef<d3.GeoProjection | null>(null);
  const pinsLayerRef   = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const rafIdRef       = useRef<number | null>(null);   // debounce handle

  // Strings are already decoded by the API hooks (cp1252 fix applied there).
  // No further decoding needed in the render layer.

  const renderPins = (
    clusters: Cluster[],
    pinsLayer: d3.Selection<SVGGElement, unknown, null, undefined>,
    k: number,
  ) => {
    const tooltip = d3.select(tooltipRef.current);

    // Full redraw of pins — acceptable for ≤200 places; debounced so it fires
    // only after the zoom gesture settles, not on every wheel tick.
    pinsLayer.selectAll('*').remove();

    clusters.forEach(cluster => {
      const isCluster = cluster.places.length > 1;
      const r    = isCluster ? 7 : 4;
      const fill = isCluster
        ? CLUSTER_COLOR
        : (cluster.places[0].isHome ? HOME_PIN_COLOR : PIN_COLOR);

      const circle = pinsLayer.append('circle')
        .attr('cx', cluster.cx)
        .attr('cy', cluster.cy)
        .attr('r', r / k)
        .attr('fill', fill)
        .attr('stroke', '#fff')
        .attr('stroke-width', 0.8 / k)
        .style('cursor', 'pointer');

      if (isCluster) {
        pinsLayer.append('text')
          .attr('x', cluster.cx)
          .attr('y', cluster.cy)
          .attr('text-anchor', 'middle')
          .attr('dominant-baseline', 'central')
          .attr('font-size', `${10 / k}px`)
          .attr('fill', CLUSTER_TEXT)
          .attr('pointer-events', 'none')
          .text(cluster.places.length);
      }

      circle
        .on('mouseover', (event: MouseEvent) => {
          let html: string;
          if (!isCluster) {
            const p = cluster.places[0];
            html = `<strong>${p.flag} ${p.city}</strong><br/>${p.countryName}`;
          } else {
            const country = cluster.places[0].countryName;
            const cities  = cluster.places.map(p => `${p.flag} ${p.city}`).join('<br/>');
            html = `<strong>${cluster.places.length} places in ${country}</strong><br/>${cities}`;
          }
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
    });
  };

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

    const pathGenerator = d3.geoPath().projection(projection);

    const visitedAlpha2Set = new Set(
      countries.filter(c => c.isVisited).map(c => c.isoAlpha2)
    );
    const homeAlpha2Set = new Set(
      countries.filter(c => c.isHome).map(c => c.isoAlpha2)
    );
    const visitedBrStates = new Set(
      visitedStates.filter(s => s.countryCode === 'BR').map(s => s.stateAbbr)
    );

    const g = svg.append('g');

    // Ocean background rectangle (inside zoom group so it fills on pan)
    g.append('rect')
      .attr('x', -width * 2)
      .attr('y', -height * 2)
      .attr('width', width * 5)
      .attr('height', height * 5)
      .attr('fill', OCEAN_COLOR);

    // World countries
    g.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .data(geoJson.features)
      .join('path')
      .attr('class', 'country')
      .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
      .attr('fill', f => {
        const alpha2: string = f.properties?.['ISO3166-1-Alpha-2'] ?? '';
        if (alpha2 && homeAlpha2Set.has(alpha2))    return HOME_COLOR;
        if (alpha2 && visitedAlpha2Set.has(alpha2)) return VISITED_COLOR;
        return UNVISITED_COLOR;
      })
      .attr('stroke', BORDER_COLOR)
      .attr('stroke-width', 0.5);

    // Brazil states layer
    if (brazilStatesGeoJson) {
      g.selectAll<SVGPathElement, GeoJSON.Feature>('.br-state')
        .data(brazilStatesGeoJson.features)
        .join('path')
        .attr('class', 'br-state')
        .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
        .attr('fill', f => {
          const abbr: string = (f.properties?.['sigla'] as string) ?? '';
          return visitedBrStates.has(abbr) ? VISITED_COLOR : HOME_COLOR;
        })
        .attr('stroke', BORDER_COLOR)
        .attr('stroke-width', 0.3)
        .attr('opacity', 0.9);
    }

    // US states layer
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

    const pinsLayer = g.append('g').attr('class', 'pins-layer');
    pinsLayerRef.current = pinsLayer;

    // Initial render
    renderPins(buildClusters(places, projection, 1), pinsLayer, 1);

    // Zoom — transform applies immediately; cluster recompute is debounced via RAF
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 32])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        g.attr('transform', event.transform.toString());
        const k = event.transform.k;

        // Cancel any pending recompute and schedule one for the next frame
        if (rafIdRef.current !== null) cancelAnimationFrame(rafIdRef.current);
        rafIdRef.current = requestAnimationFrame(() => {
          rafIdRef.current = null;
          if (projectionRef.current && pinsLayerRef.current) {
            renderPins(
              buildClusters(places, projectionRef.current, k),
              pinsLayerRef.current,
              k,
            );
          }
        });
      });

    svg.call(zoom);

    return () => {
      // Cancel pending RAF on cleanup
      if (rafIdRef.current !== null) {
        cancelAnimationFrame(rafIdRef.current);
        rafIdRef.current = null;
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [countries, places, visitedStates, geoJson, usStatesGeoJson, brazilStatesGeoJson]);

  return (
    <div className={styles.container}>
      <svg ref={svgRef} className={styles.svg} />
      <div ref={tooltipRef} className={styles.tooltip} />
    </div>
  );
}
