import type { Place } from '@/types';
import styles from './DeleteConfirm.module.scss';

interface Props {
  place: Place;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function DeleteConfirm({ place, onConfirm, onCancel }: Props) {
  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <h3>Delete place</h3>
        <p>
          Delete <strong>{place.flag} {place.city}</strong> ({place.countryName})?
          This cannot be undone.
        </p>
        <div className={styles.actions}>
          <button onClick={onCancel}>Cancel</button>
          <button className={styles.deleteBtn} onClick={onConfirm}>Delete</button>
        </div>
      </div>
    </div>
  );
}
