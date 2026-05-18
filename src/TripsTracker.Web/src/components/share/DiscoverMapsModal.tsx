import { useState } from 'react';
import { useDiscoverMaps } from '@/api/hooks';
import Modal from '@/components/ui/Modal';
import styles from './DiscoverMapsModal.module.scss';

interface Props {
  onOpen: (token: string) => void;
  onClose: () => void;
}

export default function DiscoverMapsModal({ onOpen, onClose }: Props) {
  const [query, setQuery] = useState('');
  const { data: maps = [], isFetching } = useDiscoverMaps(query);

  return (
    <Modal title="Discover maps" onClose={onClose} width={480}>
      <div className={styles.content}>
        <p className={styles.description}>
          Browse public travel maps shared by other users.
        </p>
        <input
          className={styles.search}
          type="text"
          placeholder="Search by display name…"
          value={query}
          onChange={e => setQuery(e.target.value)}
          autoFocus
        />
        {isFetching && <p className={styles.loading}>Searching…</p>}
        {!isFetching && maps.length === 0 && (
          <p className={styles.empty}>No public maps found.</p>
        )}
        <div className={styles.list}>
          {maps.map(m => (
            <div key={m.token} className={styles.row}>
              <div className={styles.info}>
                <div className={styles.stats}>
                  {m.continentsVisited} continents · {m.countriesVisited} countries · {m.placesCount} places
                </div>
                <div className={styles.name}>{m.displayName}</div>
              </div>
              <button
                className={styles.viewBtn}
                onClick={() => { onOpen(m.token); onClose(); }}
              >
                View map
              </button>
            </div>
          ))}
        </div>
      </div>
    </Modal>
  );
}
