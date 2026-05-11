import { useState } from 'react';
import { X, HelpCircle } from 'lucide-react';
import { useUserStatement } from '@/api/hooks';
import HowPointsWorkPanel from './HowPointsWorkPanel';
import styles from './PointsStatementPanel.module.scss';

interface Props {
  userId: number;
  onClose: () => void;
}

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

const EVENT_DESCRIPTIONS: Record<string, string> = {
  city_added: 'You added a new city to your trip history',
  city_pioneer: 'You were the first person globally to visit this city',
  country_first: 'First city you visited in this country',
  country_pioneer: 'You were the first person globally to visit any city in this country',
  continent_first: 'First country you visited in this continent',
  continent_pioneer: 'You were the first person globally to visit any country in this continent',
  photo_uploaded: 'You uploaded a photo of a place',
  comment_added: 'You added a comment to a place',
};

function formatEventType(eventType: string): string {
  return EVENT_LABELS[eventType] ?? eventType;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

export default function PointsStatementPanel({ userId, onClose }: Props) {
  const { data, isLoading } = useUserStatement(userId);
  const [helpOpen, setHelpOpen] = useState(false);
  const [openTooltip, setOpenTooltip] = useState<number | null>(null);

  if (helpOpen) {
    return <HowPointsWorkPanel onBack={() => setHelpOpen(false)} onClose={onClose} />;
  }

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <span className={styles.title}>
            {data ? `${data.displayName}'s points` : 'Points statement'}
          </span>
          {data && (
            <span className={styles.total}>{data.totalPoints.toLocaleString()} pts</span>
          )}
        </div>
        <button className={styles.closeBtn} onClick={onClose} title="Close">
          <X size={16} />
        </button>
      </div>

      <div className={styles.body}>
        {isLoading && <p className={styles.loading}>Loading…</p>}
        {!isLoading && data && data.events.length === 0 && (
          <p className={styles.empty}>No points earned yet.</p>
        )}
        {data && data.events.map(e => {
          const description = EVENT_DESCRIPTIONS[e.eventType];
          const isOpen = openTooltip === e.id;
          return (
            <div key={e.id} className={styles.row}>
              <span className={styles.label}>{formatEventType(e.eventType)}</span>
              {description && (
                <div className={styles.tooltipWrap}>
                  <button
                    className={styles.helpBtn}
                    onClick={() => setOpenTooltip(isOpen ? null : e.id)}
                    title="What earns this?"
                  >
                    <HelpCircle size={12} />
                  </button>
                  {isOpen && (
                    <div className={styles.tooltip}>{description}</div>
                  )}
                </div>
              )}
              <span className={styles.date}>{formatDate(e.createdAt)}</span>
              <span className={styles.points}>+{e.points}</span>
            </div>
          );
        })}
      </div>

      <div className={styles.footer}>
        <button className={styles.howBtn} onClick={() => setHelpOpen(true)}>
          How points work →
        </button>
      </div>
    </div>
  );
}
