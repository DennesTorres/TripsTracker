import { useState, useMemo, useRef, useEffect } from 'react';
import { usePlaces, useDeletePlace, useUpdatePlace } from '@/api/hooks';
import type { Place } from '@/types';
import AddPlaceForm from './AddPlaceForm';
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
  const [deleting, setDeleting] = useState<Place | null>(null);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('city');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [search, setSearch] = useState('');
  const [countryInput, setCountryInput] = useState('');
  const [showCountryDD, setShowCountryDD] = useState(false);
  const countryDDRef = useRef<HTMLDivElement>(null);
  const deletePlace = useDeletePlace();
  const updatePlace = useUpdatePlace();

  useEffect(() => {
    function onOutsideClick(e: MouseEvent) {
      if (countryDDRef.current && !countryDDRef.current.contains(e.target as Node))
        setShowCountryDD(false);
    }
    document.addEventListener('mousedown', onOutsideClick);
    return () => document.removeEventListener('mousedown', onOutsideClick);
  }, []);

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

  const countryOptions = useMemo(() =>
    [...new Set(places.map(p => p.countryName))].sort(),
    [places]);

  const filteredCountryOptions = useMemo(() =>
    countryOptions.filter(c => !countryInput || c.toLowerCase().includes(countryInput.toLowerCase())),
    [countryOptions, countryInput]);

  const filtered = places
    .filter(p => {
      if (!search) return true;
      const q = search.toLowerCase();
      return p.city.toLowerCase().includes(q) ||
             (p.stateAbbr ?? '').toLowerCase().includes(q) ||
             (p.stateName ?? '').toLowerCase().includes(q);
    })
    .filter(p => !countryInput || p.countryName.toLowerCase().includes(countryInput.toLowerCase()));

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
        <h2>Cities</h2>
        <div className={styles.filters}>
          <input
            className={styles.searchInput}
            type="text"
            placeholder="Search city…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          <div className={styles.countryCombo} ref={countryDDRef}>
            <input
              className={styles.countryInput}
              type="text"
              placeholder="All countries"
              value={countryInput}
              onChange={e => { setCountryInput(e.target.value); setShowCountryDD(true); }}
              onFocus={() => setShowCountryDD(true)}
            />
            {showCountryDD && (
              <ul className={styles.countryList}>
                <li className={styles.countryItem} onMouseDown={() => { setCountryInput(''); setShowCountryDD(false); }}>
                  All countries
                </li>
                {filteredCountryOptions.map(c => (
                  <li key={c} className={styles.countryItem} onMouseDown={() => { setCountryInput(c); setShowCountryDD(false); }}>
                    {c}
                  </li>
                ))}
              </ul>
            )}
          </div>
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
            {sorted.map(p => {
              const isDeleting = p.id === deletingId;
              return (
                <tr key={p.id} className={isDeleting ? styles.deletingRow : undefined}>
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
                    {!p.isHome && (
                      <button
                        className={styles.homeBtn}
                        onClick={() => updatePlace.mutate({ id: p.id, dto: { city: p.city, isHome: true } })}
                        disabled={updatePlace.isPending || isDeleting}
                      >
                        Set home
                      </button>
                    )}
                    <button
                      className={styles.deleteBtn}
                      onClick={() => setDeleting(p)}
                      disabled={isDeleting}
                    >
                      {isDeleting ? 'Deleting…' : 'Delete'}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {adding && <AddPlaceForm onClose={() => setAdding(false)} />}

      {deleting && (
        <DeleteConfirm
          place={deleting}
          onConfirm={() => {
            const place = deleting;
            setDeleting(null);
            setDeletingId(place.id);
            deletePlace.mutate(place.id, { onSettled: () => setDeletingId(null) });
          }}
          onCancel={() => setDeleting(null)}
        />
      )}

    </div>
  );
}
