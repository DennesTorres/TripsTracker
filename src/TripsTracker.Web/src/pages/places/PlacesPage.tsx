import { useState, useMemo } from 'react';
import { usePlaces, useDeletePlace } from '@/api/hooks';
import type { Place } from '@/types';
import AddPlaceForm from './AddPlaceForm';
import PlaceForm from './PlaceForm';
import DeleteConfirm from './DeleteConfirm';
import styles from './PlacesPage.module.scss';

type SortKey = 'city' | 'countryName' | 'stateAbbr' | 'lon' | 'lat';
type SortDir = 'asc' | 'desc';

function sortPlaces(places: Place[], key: SortKey, dir: SortDir): Place[] {
  return [...places].sort((a, b) => {
    const av = a[key] ?? '';
    const bv = b[key] ?? '';
    const cmp = typeof av === 'number' && typeof bv === 'number'
      ? av - bv
      : String(av).localeCompare(String(bv));
    return dir === 'asc' ? cmp : -cmp;
  });
}

export default function PlacesPage() {
  const { data: places = [], isLoading } = usePlaces();
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<Place | null>(null);
  const [deleting, setDeleting] = useState<Place | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('city');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [search, setSearch] = useState('');
  const [countryFilter, setCountryFilter] = useState('');
  const deletePlace = useDeletePlace();

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

  const countryOptions = useMemo(() =>
    [...new Set(places.map(p => p.countryName))].sort(),
    [places]);

  const filtered = places
    .filter(p => !search || p.city.toLowerCase().includes(search.toLowerCase()))
    .filter(p => !countryFilter || p.countryName === countryFilter);

  const sorted = sortPlaces(filtered, sortKey, sortDir);

  function handleSort(key: SortKey) {
    if (key === sortKey) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortKey(key); setSortDir('asc'); }
  }

  function sortIndicator(key: SortKey) {
    if (key !== sortKey) return null;
    return <span className={styles.sortArrow}>{sortDir === 'asc' ? ' ▲' : ' ▼'}</span>;
  }

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h2>Places</h2>
        <div className={styles.filters}>
          <input
            className={styles.searchInput}
            type="text"
            placeholder="Search city…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          <select
            className={styles.countrySelect}
            value={countryFilter}
            onChange={e => setCountryFilter(e.target.value)}
          >
            <option value="">All countries</option>
            {countryOptions.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <button className={styles.addBtn} onClick={() => setAdding(true)}>+ Add place</button>
      </div>

      <div className={styles.tableWrapper}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th></th>
              <th className={styles.sortable} onClick={() => handleSort('city')}>City{sortIndicator('city')}</th>
              <th className={styles.sortable} onClick={() => handleSort('countryName')}>Country{sortIndicator('countryName')}</th>
              <th className={styles.sortable} onClick={() => handleSort('stateAbbr')}>State{sortIndicator('stateAbbr')}</th>
              <th className={styles.sortable} onClick={() => handleSort('lon')}>Lon{sortIndicator('lon')}</th>
              <th className={styles.sortable} onClick={() => handleSort('lat')}>Lat{sortIndicator('lat')}</th>
              <th>Home</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {sorted.map(p => (
              <tr key={p.id}>
                <td className={styles.flag}>{p.countryFlag}</td>
                <td>{p.city}</td>
                <td>{p.countryName}</td>
                <td className={styles.stateCell} title={p.stateName ?? p.stateAbbr ?? ''}>
                  {p.stateName ?? p.stateAbbr ?? ''}
                </td>
                <td className={styles.coord}>{p.lon.toFixed(4)}</td>
                <td className={styles.coord}>{p.lat.toFixed(4)}</td>
                <td>{p.isHome ? '✓' : ''}</td>
                <td className={styles.actions}>
                  <button onClick={() => setEditing(p)}>Edit</button>
                  <button className={styles.deleteBtn} onClick={() => setDeleting(p)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {adding && <AddPlaceForm onClose={() => setAdding(false)} />}

      {editing !== null && (
        <PlaceForm
          place={editing}
          onClose={() => setEditing(null)}
        />
      )}

      {deleting && (
        <DeleteConfirm
          place={deleting}
          onConfirm={() => {
            const place = deleting;
            setDeleting(null);
            deletePlace.mutate(place.id);
          }}
          onCancel={() => setDeleting(null)}
        />
      )}

    </div>
  );
}
