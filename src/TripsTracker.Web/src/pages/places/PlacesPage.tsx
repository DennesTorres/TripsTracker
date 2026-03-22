import { useState } from 'react';
import { usePlaces, useDeletePlace } from '@/api/hooks';
import type { Place } from '@/types';
import PlaceForm from './PlaceForm';
import DeleteConfirm from './DeleteConfirm';
import styles from './PlacesPage.module.scss';

export default function PlacesPage() {
  const { data: places = [], isLoading } = usePlaces();
  const [editing, setEditing] = useState<Place | null>(null);
  const [deleting, setDeleting] = useState<Place | null>(null);
  const deletePlace = useDeletePlace();

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h2>Places</h2>
      </div>

      <div className={styles.tableWrapper}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th></th>
              <th>City</th>
              <th>Country</th>
              <th>State</th>
              <th>Lon</th>
              <th>Lat</th>
              <th>Home</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {places.map(p => (
              <tr key={p.id}>
                <td className={styles.flag}>{p.countryFlag}</td>
                <td>{p.city}</td>
                <td>{p.countryName}</td>
                <td>{p.stateAbbr ?? ''}</td>
                <td>{p.lon.toFixed(4)}</td>
                <td>{p.lat.toFixed(4)}</td>
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
            deletePlace.mutate(deleting.id);
            setDeleting(null);
          }}
          onCancel={() => setDeleting(null)}
        />
      )}
    </div>
  );
}
