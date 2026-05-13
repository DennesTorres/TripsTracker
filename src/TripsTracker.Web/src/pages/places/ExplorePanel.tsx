import { useExploreSearch } from '@/api/hooks';
import type { ExploreLocation } from '@/types';
import styles from './ExplorePanel.module.scss';

interface Props {
  query: string;
  onQueryChange: (q: string) => void;
  onPinLocation: (location: ExploreLocation | null) => void;
  onSelectCity: (loc: ExploreLocation) => void;
}

export default function ExplorePanel({ query, onQueryChange, onPinLocation, onSelectCity }: Props) {
  const { data: locations = [], isFetching } = useExploreSearch(query);
  const showDropdown = query.length >= 2;

  function handleSelect(loc: ExploreLocation) {
    onPinLocation(loc);
    onSelectCity(loc);
  }

  return (
    <div className={styles.container}>
      <input
        className={styles.searchInput}
        type="text"
        placeholder="Search city to explore…"
        value={query}
        onChange={e => onQueryChange(e.target.value)}
      />
      {showDropdown && isFetching && (
        <div className={styles.dropdown}>
          <p className={styles.hint}>Searching…</p>
        </div>
      )}
      {showDropdown && !isFetching && locations.length === 0 && (
        <div className={styles.dropdown}>
          <p className={styles.hint}>No places found.</p>
        </div>
      )}
      {showDropdown && !isFetching && locations.length > 0 && (
        <div className={styles.dropdown}>
          {locations.map((loc, i) => (
            <button key={i} className={styles.resultRow} onClick={() => handleSelect(loc)}>
              <span className={styles.resultCity}>
                {loc.city}{loc.stateName ? `, ${loc.stateName}` : ''} — {loc.countryName}
              </span>
              <span className={styles.resultMeta}>
                {loc.userCount} {loc.userCount === 1 ? 'visitor' : 'visitors'}
                {loc.photoCount > 0 ? ` · ${loc.photoCount} photos` : ''}
                {loc.commentCount > 0 ? ` · ${loc.commentCount} comments` : ''}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
