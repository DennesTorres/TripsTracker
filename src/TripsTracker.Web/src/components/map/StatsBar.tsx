import type { Country, Place } from '@/types';
import styles from './StatsBar.module.scss';

interface Props {
  countries: Country[];
  places: Place[];
}

export default function StatsBar({ countries, places }: Props) {
  const visitedCountries = countries.filter(c => c.isVisited && !c.isHome).length;
  const totalPlaces = places.length;
  const regions = new Set(
    countries.filter(c => c.isVisited).map(c => c.region)
  ).size;

  return (
    <div className={styles.bar}>
      <Stat value={visitedCountries} label="countries visited" />
      <Stat value={regions} label="continents" />
      <Stat value={totalPlaces} label="places" />
    </div>
  );
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <div className={styles.stat}>
      <span className={styles.value}>{value}</span>
      <span className={styles.label}>{label}</span>
    </div>
  );
}
