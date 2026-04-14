import { useState } from 'react';
import { useCountries, useVisitedStates, useSetStateBorders } from '@/api/hooks';
import type { Country } from '@/types';
import styles from './CountriesPage.module.scss';

export default function CountriesPage() {
  const { data: countries = [], isLoading } = useCountries();
  const { data: visitedStates = [] } = useVisitedStates();
  const setStateBorders = useSetStateBorders();
  const [visitedOnly, setVisitedOnly] = useState(false);
  const [regionFilter, setRegionFilter] = useState('');

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

  const statesByCountry = visitedStates.reduce<Record<number, { abbr: string; name?: string }[]>>((acc, vs) => {
    (acc[vs.countryId] ??= []).push({ abbr: vs.stateAbbr, name: vs.stateName });
    return acc;
  }, {});

  const regions = [...new Set(countries.map(c => c.region))].sort();
  const visitedCount = countries.filter(c => c.isVisited).length;
  const stateCount = visitedStates.length;

  const filtered = countries
    .filter(c => !visitedOnly || c.isVisited)
    .filter(c => !regionFilter || c.region === regionFilter)
    .sort((a, b) => a.name.localeCompare(b.name));

  return (
    <div className={styles.page}>
      <div className={styles.section}>
        <div className={styles.toolbar}>
          <h2>Countries</h2>
          <div className={styles.stats}>
            <span>{visitedCount} / {countries.length} countries visited</span>
            {stateCount > 0 && <span>·</span>}
            {stateCount > 0 && <span>{stateCount} states</span>}
          </div>
          <div className={styles.filters}>
            <label className={styles.checkLabel}>
              <input
                type="checkbox"
                checked={visitedOnly}
                onChange={e => setVisitedOnly(e.target.checked)}
              />
              Visited only
            </label>
            <select
              className={styles.regionSelect}
              value={regionFilter}
              onChange={e => setRegionFilter(e.target.value)}
            >
              <option value="">All continents</option>
              {regions.map(r => <option key={r} value={r}>{r}</option>)}
            </select>
          </div>
        </div>
        <table className={styles.table}>
          <thead>
            <tr>
              <th></th>
              <th>Country</th>
              <th>Continent</th>
              <th>States</th>
              <th>Visited</th>
              <th>Home</th>
              <th>Borders</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((c: Country) => (
              <tr key={c.id}>
                <td className={styles.flag}>{c.flag}</td>
                <td>{c.name}</td>
                <td className={styles.region}>{c.region}</td>
                <td className={styles.states}>
                  {(statesByCountry[c.id] ?? [])
                    .sort((a, b) => a.abbr.localeCompare(b.abbr))
                    .map(s => {
                      const isNumeric = /^\d+$/.test(s.abbr);
                      const label = isNumeric && s.name ? s.name.split(' ')[0] : s.abbr;
                      return (
                        <span key={s.abbr} className={styles.stateTag} title={s.name ?? s.abbr}>
                          {label}
                        </span>
                      );
                    })}
                </td>
                <td className={styles.visited}>{c.isVisited ? '✓' : ''}</td>
                <td>{c.isHome ? '✓' : ''}</td>
                <td>
                  <input
                    type="checkbox"
                    checked={c.showStateBorders}
                    onChange={() => setStateBorders.mutate({ id: c.id, show: !c.showStateBorders })}
                    title="Show state/region borders on map"
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
