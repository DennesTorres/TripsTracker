import { useState } from 'react';
import { useUpdatePlace } from '@/api/hooks';
import type { Place } from '@/types';
import styles from './PlaceForm.module.scss';

interface Props {
  place: Place;
  onClose: () => void;
}

export default function PlaceForm({ place, onClose }: Props) {
  const update = useUpdatePlace();
  const [city, setCity] = useState(place.city);
  const [isHome, setIsHome] = useState(place.isHome);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    update.mutate({ id: place.id, dto: { city, isHome } }, { onSuccess: onClose });
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <h3>Edit place</h3>
          <button className={styles.close} onClick={onClose}>×</button>
        </div>

        <form onSubmit={handleSubmit} className={styles.form}>
          <div className={styles.readOnly}>
            <span>{place.countryFlag} {place.countryName}</span>
            {place.stateAbbr && <span className={styles.state}>{place.stateAbbr}</span>}
            <span className={styles.coords}>{place.lat.toFixed(4)}, {place.lon.toFixed(4)}</span>
          </div>

          <label>
            City
            <input
              type="text"
              value={city}
              onChange={e => setCity(e.target.value)}
              required
            />
          </label>

          <label className={styles.checkLabel}>
            <input
              type="checkbox"
              checked={isHome}
              onChange={e => setIsHome(e.target.checked)}
            />
            Home location
          </label>

          <div className={styles.actions}>
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className={styles.saveBtn} disabled={update.isPending}>
              {update.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
