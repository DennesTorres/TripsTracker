import type { Place } from '@/types';
import Modal from '@/components/ui/Modal';
import styles from './DeleteConfirm.module.scss';

interface Props {
  place: Place;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function DeleteConfirm({ place, onConfirm, onCancel }: Props) {
  return (
    <Modal title="Delete place" onClose={onCancel} width={380}>
      <div className={styles.content}>
        <p>
          Delete <strong>{place.countryFlag} {place.city}</strong> ({place.countryName})?
          This cannot be undone.
        </p>
        <div className={styles.actions}>
          <button onClick={onCancel}>Cancel</button>
          <button className={styles.deleteBtn} onClick={onConfirm}>Delete</button>
        </div>
      </div>
    </Modal>
  );
}
