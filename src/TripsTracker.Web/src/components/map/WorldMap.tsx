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
const OCEAN_COLOR      = '#0b1628';
const VISITED_COLOR    = '#2a5f8f';
const HOME_COLOR       = '#1a3a5c';
const UNVISITED_COLOR  = '#1c2535';
const BORDER_COLOR     = '#0d1e30';
const PIN_COLOR        = '#e8922a';      // orange
const HOME_PIN_COLOR   = '#d4a017';     // gold
const CLUSTER_COLOR    = '#c0392b';     // red for clusters
const CLUSTER_TEXT     = '#fff';

// Threshold in screen pixels for within-country clustering
const CLUSTER_THRESHOLD_PX = 28;

/**
 * Decode flag emoji that may be stored as UTF-8 mojibake.
 * When flag chars are all ≤ 0xFF and UTF-8 decoding produces fewer characters,
 * the original string is raw UTF-8 bytes mistakenly stored as Latin-1 characters.
 */
function decodeFlag(flag: string): string {
  if (!flag) return flag;
  // If any char is > 0xFF it's already proper Unicode — no fix needed
  if ([...flag].some(c => c.charCodeAt(0) > 0xff)) return flag;
  try {
    const bytes = new Uint8Array([...flag].map(c => c.charCodeAt(0)));
    const decoded = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    // Accept decoded form only when it's shorter (multi-byte sequences collapsed)
    return decoded.length < flag.length ? decoded : flag;
  } catch {
    return flag;
  }
}

interface Cluster {
  cx: number;
  cy: number;
  places: Place[];
}

/**
 * Greedy O(n²) within-country clustering.
 * zoomK = current zoom scale factor (1 = no zoom).
 * Two points cluster if their SVG-space distance ≤ CLUSTER_THRESHOLD_PX / zoomK.
 * Points from different countries are never merged.
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
  const svgRef = useRef<SVGSVGElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);
  // Keep projection and pins layer in refs so zoom handler can access them
  const projectionRef = useRef<d3.GeoProjection | null>(null);
  const pinsLayerRef = useRef<d3.Selection<SVGGElement, unknown, null, undefined> | null>(null);
  const zoomKRef = useRef<number>(1);

  const renderPins = (clusters: Cluster[], pinsLayer: d3.Selection<SVGGElement, unknown, null, undefined>, k: number) => {
    const tooltip = d3.select(tooltipRef.current);

    // Clear previous pins/clusters
    pinsLayer.selectAll('*').remove();

    clusters.forEach(cluster => {
      const r = cluster.places.length === 1 ? 4 : 6;
      const fill = cluster.places.length === 1
        ? (cluster.places[0].isHome ? HOME_PIN_COLOR : PIN_COLOR)
        : CLUSTER_COLOR;

      const circle = pinsLayer.append('circle')
        .attr('cx', cluster.cx)
        .attr('cy', cluster.cy)
        .attr('r', r / k)           // counter-scale so pins stay constant screen size
        .attr('fill', fill)
        .attr('stroke', '#fff')
        .attr('stroke-width', 0.8 / k)
        .style('cursor', 'pointer');

      if (cluster.places.length > 1) {
        // Cluster label (counter-scaled)
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

      // Tooltip on hover
      circle
        .on('mouseover', (event: MouseEvent) => {
          let html: string;
          if (cluster.places.length === 1) {
            const p = cluster.places[0];
            const flag = decodeFlag(p.flag);
            html = `<strong>${flag} ${p.city}</strong><br/>${p.countryName}`;
          } else {
            const country = cluster.places[0].countryName;
            const cities = cluster.places.map(p => decodeFlag(p.flag) + ' ' + p.city).join('<br/>');
            html = `<strong>${cluster.places.length} places in ${country}</strong><br/>${cities}`;
          }
          tooltip
            .style('display', 'block')
            .html(html);
          const svgRect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - svgRect.left + 12}px`)
            .style('top', `${event.clientY - svgRect.top - 28}px`);
        })
        .on('mousemove', (event: MouseEvent) => {
          const svgRect = svgRef.current!.getBoundingClientRect();
          tooltip
            .style('left', `${event.clientX - svgRect.left + 12}px`)
            .style('top', `${event.clientY - svgRect.top - 28}px`);
        })
        .on('mouseout', () => {
          tooltip.style('display', 'none');
        });
    });
  };

  useEffect(() => {
    if (!svgRef.current || !geoJson) return;

    const width = svgRef.current.clientWidth || 960;
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

    // Base group for zoom (countries only)
    const g = svg.append('g');

    // Ocean rectangle as background
    g.append('rect')
      .attr('x', -width)
      .attr('y', -height)
      .attr('width', width * 3)
      .attr('height', height * 3)
      .attr('fill', OCEAN_COLOR);

    // World countries
    g.selectAll<SVGPathElement, GeoJSON.Feature>('.country')
      .data(geoJson.features)
      .join('path')
      .attr('class', 'country')
      .attr('d', f => pathGenerator(f as GeoPermissibleObjects) ?? '')
      .attr('fill', f => {
        const alpha2: string = f.properties?.['ISO3166-1-Alpha-2'] ?? '';
        if (alpha2 && homeAlpha2Set.has(alpha2)) return HOME_COLOR;
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

    // Separate group for pins — also inside g so zoom translates them,
    // but we counter-scale pin size and recompute clusters on zoom
    const pinsLayer = g.append('g').attr('class', 'pins-layer');
    pinsLayerRef.current = pinsLayer;

    // Initial render at zoom k=1
    const initialClusters = buildClusters(places, projection, 1);
    renderPins(initialClusters, pinsLayer, 1);

    // Zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 12])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        g.attr('transform', event.transform.toString());
        const k = event.transform.k;
        zoomKRef.current = k;
        // Recompute clusters for new zoom level
        if (projectionRef.current && pinsLayerRef.current) {
          const clusters = buildClusters(places, projectionRef.current, k);
          renderPins(clusters, pinsLayerRef.current, k);
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
