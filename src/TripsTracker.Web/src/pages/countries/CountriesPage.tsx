import {
  useCountries, useVisitedStates,
  useSetCountryVisited, useSetCountryHome,
  useSetVisitedState, useClearVisitedState,
} from '@/api/hooks';
import type { Country } from '@/types';
import styles from './CountriesPage.module.scss';

// Brazil state list
const BR_STATES = [
  'AC','AL','AM','AP','BA','CE','DF','ES','GO',
  'MA','MG','MS','MT','PA','PB','PE','PI','PR',
  'RJ','RN','RO','RR','RS','SC','SE','SP','TO',
];

export default function CountriesPage() {
  const { data: countries = [], isLoading: cLoading } = useCountries();
  const { data: visitedStates = [], isLoading: vsLoading } = useVisitedStates();
  const setVisited = useSetCountryVisited();
  const setHome = useSetCountryHome();
  const setStateMark = useSetVisitedState();
  const clearStateMark = useClearVisitedState();

  const visitedBrStates = new Set(
    visitedStates.filter(s => s.countryCode === 'BR').map(s => s.stateAbbr)
  );

  if (cLoading || vsLoading) return <div className={styles.loading}>Loading…</div>;

  const sorted = [...countries].sort((a, b) => a.name.localeCompare(b.name));

  return (
    <div className={styles.page}>
      <div className={styles.section}>
        <h2>Countries</h2>
        <table className={styles.table}>
          <thead>
            <tr>
              <th></th>
              <th>Country</th>
              <th>Region</th>
              <th>Visited</th>
              <th>Home</th>
            </tr>
          </thead>
          <tbody>
            {sorted.map((c: Country) => (
              <tr key={c.id}>
                <td className={styles.flag}>{c.flag}</td>
                <td>{c.name}</td>
                <td className={styles.region}>{c.region}</td>
                <td>
                  <input
                    type="checkbox"
                    checked={c.isVisited}
                    disabled={c.isHome}
                    onChange={e => setVisited.mutate({ id: c.id, isVisited: e.target.checked })}
                  />
                </td>
                <td>
                  <input
                    type="radio"
                    name="home"
                    checked={c.isHome}
                    onChange={() => setHome.mutate(c.id)}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className={styles.section}>
        <h2>Brazil — visited states</h2>
        <div className={styles.stateGrid}>
          {BR_STATES.map(abbr => {
            const visited = visitedBrStates.has(abbr);
            return (
              <button
                key={abbr}
                className={`${styles.stateBtn} ${visited ? styles.visited : ''}`}
                onClick={() => {
                  if (visited) {
                    clearStateMark.mutate({ countryCode: 'BR', stateAbbr: abbr });
                  } else {
                    setStateMark.mutate({ countryCode: 'BR', stateAbbr: abbr });
                  }
                }}
              >
                {abbr}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
