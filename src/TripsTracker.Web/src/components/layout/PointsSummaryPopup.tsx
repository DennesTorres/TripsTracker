import { useState } from 'react';
import { Star, X, HelpCircle } from 'lucide-react';
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

interface Props {
  onClose: () => void;
  onViewFull: () => void;
}

export default function PointsSummaryPopup({ onClose, onViewFull }: Props) {
  const { data } = usePointsSummary();
  const [openTooltip, setOpenTooltip] = useState<number | null>(null);

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
        {data?.recentEvents.slice(0, 5).map(e => {
          const loc = e.eventType.startsWith('continent') ? e.continentName
            : e.eventType.startsWith('country') ? e.countryName
            : e.cityName;
          const description = EVENT_DESCRIPTIONS[e.eventType];
          const isOpen = openTooltip === e.id;
          return (
            <div key={e.id} className={styles.row}>
              <div className={styles.labelWrap}>
                <span className={styles.label}>{EVENT_LABELS[e.eventType] ?? e.eventType}</span>
                {loc && <span className={styles.city}>{loc}</span>}
              </div>
              <div className={styles.rowRight}>
                {description && (
                  <div className={styles.tooltipWrap}>
                    <button
                      className={styles.helpBtn}
                      onClick={() => setOpenTooltip(isOpen ? null : e.id)}
                      title="What earns this?"
                    >
                      <HelpCircle size={11} />
                    </button>
                    {isOpen && <div className={styles.tooltip}>{description}</div>}
                  </div>
                )}
                <span className={styles.points}>+{e.points}</span>
              </div>
            </div>
          );
        })}
      </div>

      <button className={styles.fullBtn} onClick={onViewFull}>
        View full statement →
      </button>
    </div>
  );
}
