import { useState } from 'react';
import axios from 'axios';
import { useCreatePlace, useCountries } from '@/api/hooks';
import styles from './AddPlaceForm.module.scss';

interface Props {
  onClose: () => void;
}

export default function AddPlaceForm({ onClose }: Props) {
  const { data: countries = [] } = useCountries();
  const create = useCreatePlace();
  const [countryIsoAlpha2, setCountryIsoAlpha2] = useState('');
  const [cityName, setCityName] = useState('');
  const [isHome, setIsHome] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sorted = [...countries].sort((a, b) => a.name.localeCompare(b.name));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    create.mutate(
      { cityName: cityName.trim(), countryIsoAlpha2, isHome },
      {
        onSuccess: onClose,
        onError: (err: unknown) => {
          const msg = axios.isAxiosError(err)
            ? (typeof err.response?.data === 'string' ? err.response.data : 'Could not add place.')
            : err instanceof Error ? err.message : 'Could not add place.';
          setError(msg);
        },
      }
    );
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <h3>Add place</h3>
          <button className={styles.close} onClick={onClose}>×</button>
        </div>

        <form onSubmit={handleSubmit} className={styles.form}>
          <label>
            Country
            <select
              value={countryIsoAlpha2}
              onChange={e => setCountryIsoAlpha2(e.target.value)}
              required
            >
              <option value="">— select a country —</option>
              {sorted.map(c => (
                <option key={c.isoAlpha2} value={c.isoAlpha2}>
                  {c.flag} {c.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            City
            <input
              type="text"
              value={cityName}
              onChange={e => setCityName(e.target.value)}
              placeholder="e.g. São Paulo"
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

          {error && <p className={styles.error}>{error}</p>}

          <div className={styles.actions}>
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className={styles.saveBtn} disabled={create.isPending}>
              {create.isPending ? 'Geocoding…' : 'Add place'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
