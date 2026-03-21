import {
  useCountries,
  useSetCountryVisited, useSetCountryHome,
} from '@/api/hooks';
import type { Country } from '@/types';
import styles from './CountriesPage.module.scss';

export default function CountriesPage() {
  const { data: countries = [], isLoading } = useCountries();
  const setVisited = useSetCountryVisited();
  const setHome = useSetCountryHome();

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

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
    </div>
  );
}
