import { useState } from 'react';
import { useLeaderboard } from '@/api/hooks';
import Modal from '@/components/ui/Modal';
import PointsStatementPanel from './PointsStatementPanel';
import styles from './LeaderboardModal.module.scss';

interface Props {
  onClose: () => void;
}

export default function LeaderboardModal({ onClose }: Props) {
  const { data: entries = [], isLoading } = useLeaderboard();
  const [viewingUserId, setViewingUserId] = useState<number | null>(null);

  return (
    <>
      <Modal title="Leaderboard" onClose={onClose} width={400}>
        <div className={styles.content}>
          {isLoading && <p className={styles.loading}>Loading…</p>}
          {!isLoading && entries.length === 0 && (
            <p className={styles.empty}>No scores yet. Start exploring!</p>
          )}
          <div className={styles.list}>
            {entries.map(e => (
              <button
                key={e.rank}
                className={`${styles.row} ${e.rank <= 3 ? styles[`top${e.rank}`] : ''}`}
                onClick={() => setViewingUserId(e.userId)}
                title={`View ${e.displayName}'s points statement`}
              >
                <span className={styles.rank}>#{e.rank}</span>
                <span className={styles.name}>{e.displayName}</span>
                <span className={styles.points}>{e.totalPoints.toLocaleString()} pts</span>
              </button>
            ))}
          </div>
        </div>
      </Modal>
      {viewingUserId !== null && (
        <PointsStatementPanel
          userId={viewingUserId}
          onClose={() => setViewingUserId(null)}
        />
      )}
    </>
  );
}
