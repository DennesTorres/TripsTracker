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

const VISITED_COLOR = '#4ade80';
const HOME_COLOR = '#60a5fa';
const DEFAULT_COLOR = '#1e293b';
const BORDER_COLOR = '#334155';
const PIN_COLOR = '#f97316';
const HOME_PIN_COLOR = '#60a5fa';

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

  useEffect(() => {
    if (!svgRef.current || !geoJson) return;

    const width = svgRef.current.clientWidth || 960;
    const height = svgRef.current.clientHeight || 500;

    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    const projection = d3.geoNaturalEarth1()
      .scale(width / 6.3)
      .translate([width / 2, height / 2]);

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
        return DEFAULT_COLOR;
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

    // US states layer (all visited — draw borders for detail)
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

    // Place pins
    const tooltip = d3.select(tooltipRef.current);

    g.selectAll<SVGCircleElement, Place>('.pin')
      .data(places)
      .join('circle')
      .attr('class', 'pin')
      .attr('cx', p => {
        const coords = projection([p.lon, p.lat]);
        return coords ? coords[0] : 0;
      })
      .attr('cy', p => {
        const coords = projection([p.lon, p.lat]);
        return coords ? coords[1] : 0;
      })
      .attr('r', 4)
      .attr('fill', p => p.isHome ? HOME_PIN_COLOR : PIN_COLOR)
      .attr('stroke', '#fff')
      .attr('stroke-width', 1)
      .style('cursor', 'pointer')
      .on('mouseover', (event: MouseEvent, p: Place) => {
        tooltip
          .style('display', 'block')
          .html(`<strong>${p.flag} ${p.city}</strong><br/>${p.countryName}`);
        tooltip
          .style('left', `${event.offsetX + 12}px`)
          .style('top', `${event.offsetY - 28}px`);
      })
      .on('mousemove', (event: MouseEvent) => {
        tooltip
          .style('left', `${event.offsetX + 12}px`)
          .style('top', `${event.offsetY - 28}px`);
      })
      .on('mouseout', () => {
        tooltip.style('display', 'none');
      });

    // Zoom
    const zoom = d3.zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 8])
      .on('zoom', (event: d3.D3ZoomEvent<SVGSVGElement, unknown>) => {
        g.attr('transform', event.transform.toString());
      });

    svg.call(zoom);
  }, [countries, places, visitedStates, geoJson, usStatesGeoJson, brazilStatesGeoJson]);

  return (
    <div className={styles.container}>
      <svg ref={svgRef} className={styles.svg} />
      <div ref={tooltipRef} className={styles.tooltip} />
    </div>
  );
}
