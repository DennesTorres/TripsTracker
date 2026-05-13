import { Star, X } from 'lucide-react';
import { usePointsSummary } from '@/api/hooks';
import styles from './PointsSummaryPopup.module.scss';

const EVENT_LABELS: Record<string, string> = {
  city_added: 'City added',
  city_pioneer: 'Pioneer city',
  country_first: 'First country visit',
  country_pioneer: 'Pioneer country',
  continent_first: 'First continent visit',
  continent_pioneer: 'Pioneer continent',
  photo_uploaded: 'Photo uploaded',
  comment_added: 'Comment added',
};

interface Props {
  onClose: () => void;
  onViewFull: () => void;
}

export default function PointsSummaryPopup({ onClose, onViewFull }: Props) {
  const { data } = usePointsSummary();

  return (
    <div className={styles.popup}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Star size={13} className={styles.star} />
          <span className={styles.total}>{data?.totalPoints.toLocaleString() ?? '—'} pts</span>
        </div>
        <button className={styles.closeBtn} onClick={onClose}><X size={14} /></button>
      </div>

      <div className={styles.list}>
        {!data?.recentEvents.length && (
          <p className={styles.empty}>No points yet. Start exploring!</p>
        )}
        {data?.recentEvents.slice(0, 5).map(e => (
          <div key={e.id} className={styles.row}>
            <span className={styles.label}>{EVENT_LABELS[e.eventType] ?? e.eventType}</span>
            <span className={styles.points}>+{e.points}</span>
          </div>
        ))}
      </div>

      <button className={styles.fullBtn} onClick={onViewFull}>
        View full statement →
      </button>
    </div>
  );
}
