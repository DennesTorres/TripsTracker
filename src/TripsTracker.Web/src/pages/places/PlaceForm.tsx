import { useState } from 'react';
import { useUpdatePlace } from '@/api/hooks';
import type { Place } from '@/types';
import Modal from '@/components/ui/Modal';
import FormInput from '@/components/ui/FormInput';
import FormCheckbox from '@/components/ui/FormCheckbox';
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
    <Modal title="Edit place" onClose={onClose} width={560}>
      <div className={styles.readOnly}>
        <span>{place.countryFlag} {place.countryName}</span>
        {place.stateAbbr && <span className={styles.state}>{place.stateAbbr}</span>}
        <span className={styles.coords}>{place.lat.toFixed(4)}, {place.lon.toFixed(4)}</span>
      </div>
      <form onSubmit={handleSubmit} className={styles.form}>
        <FormInput
          label="City"
          value={city}
          onChange={setCity}
          required
        />
        <FormCheckbox
          label="Home location"
          checked={isHome}
          onChange={setIsHome}
        />
        <div className={styles.actions}>
          <button type="button" onClick={onClose}>Cancel</button>
          <button type="submit" className={styles.saveBtn} disabled={update.isPending}>
            {update.isPending ? 'Saving…' : 'Save'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
