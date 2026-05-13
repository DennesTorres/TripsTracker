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

  function handleSelect(loc: ExploreLocation) {
    onPinLocation(loc);
    onSelectCity(loc);
  }

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <span className={styles.title}>Explore places</span>
      </div>

      <div className={styles.searchView}>
        <input
          className={styles.searchInput}
          type="text"
          placeholder="Search city…"
          value={query}
          onChange={e => onQueryChange(e.target.value)}
          autoFocus
        />
        {isFetching && <p className={styles.hint}>Searching…</p>}
        {!isFetching && query.length >= 2 && locations.length === 0 && (
          <p className={styles.hint}>No places found.</p>
        )}
        <div className={styles.results}>
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
      </div>
    </div>
  );
}
