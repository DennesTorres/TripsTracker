import { useState } from 'react';
import { usePlaces, useDeletePlace, useSetCountryHome } from '@/api/hooks';
import type { Place, DeletePlaceResult } from '@/types';
import AddPlaceForm from './AddPlaceForm';
import PlaceForm from './PlaceForm';
import DeleteConfirm from './DeleteConfirm';
import styles from './PlacesPage.module.scss';

export default function PlacesPage() {
  const { data: places = [], isLoading } = usePlaces();
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<Place | null>(null);
  const [deleting, setDeleting] = useState<Place | null>(null);
  const [homePrompt, setHomePrompt] = useState<DeletePlaceResult | null>(null);
  const deletePlace = useDeletePlace();
  const setHome = useSetCountryHome();

  if (isLoading) return <div className={styles.loading}>Loading…</div>;

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h2>Places</h2>
        <button className={styles.addBtn} onClick={() => setAdding(true)}>+ Add place</button>
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
            deletePlace.mutate(place.id, {
              onSuccess: result => {
                if (result?.promptHomeCountry) setHomePrompt(result);
              },
            });
          }}
          onCancel={() => setDeleting(null)}
        />
      )}

      {homePrompt && (
        <div className={styles.overlay}>
          <div className={styles.modal}>
            <div className={styles.header}>
              <h3>Home country</h3>
            </div>
            <p>You removed your home city. Is <strong>{homePrompt.countryName}</strong> still your home country?</p>
            <div className={styles.actions}>
              <button
                onClick={() => {
                  if (homePrompt.countryId != null)
                    setHome.mutate({ id: homePrompt.countryId, isHome: false });
                  setHomePrompt(null);
                }}
              >
                No
              </button>
              <button className={styles.saveBtn} onClick={() => setHomePrompt(null)}>
                Yes
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
