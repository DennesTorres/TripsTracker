import { ArrowLeft, X } from 'lucide-react';
import styles from './HowPointsWorkPanel.module.scss';

interface Props {
  onBack: () => void;
  onClose: () => void;
}

const EARNING_METHODS = [
  {
    category: 'Places',
    items: [
      { label: 'Add a city', points: 50, description: 'Earn points every time you add a new visited city to your map.' },
      { label: 'Pioneer city', points: 200, description: 'Be the first person globally to add a city. Rare and rewarding!' },
    ],
  },
  {
    category: 'Countries',
    items: [
      { label: 'First visit to a country', points: 500, description: 'Earn bonus points the first time you add a city in a country.' },
      { label: 'Pioneer country', points: 2000, description: 'Be the first person globally to visit any city in a country.' },
    ],
  },
  {
    category: 'Continents',
    items: [
      { label: 'First visit to a continent', points: 5000, description: 'Earn a large bonus the first time you visit any country on a continent.' },
      { label: 'Pioneer continent', points: 20000, description: 'Be the first person globally to visit any country on a continent. Legendary!' },
    ],
  },
  {
    category: 'Photos & Comments',
    items: [
      { label: 'Upload a photo', points: 10, description: 'Share your travel photos for the community.' },
      { label: 'Add a comment', points: 5, description: 'Share your experience and tips about a visited place.' },
    ],
  },
];

export default function HowPointsWorkPanel({ onBack, onClose }: Props) {
  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <button className={styles.backBtn} onClick={onBack} title="Back">
          <ArrowLeft size={16} />
          <span>Back</span>
        </button>
        <span className={styles.title}>How points work</span>
        <button className={styles.closeBtn} onClick={onClose} title="Close">
          <X size={16} />
        </button>
      </div>

      <div className={styles.body}>
        <p className={styles.intro}>
          Earn points by exploring the world. Pioneer bonuses are awarded when you are the first
          person to visit a location — they can be lost if the location is deleted.
        </p>
        {EARNING_METHODS.map(section => (
          <div key={section.category} className={styles.section}>
            <h3 className={styles.sectionTitle}>{section.category}</h3>
            {section.items.map(item => (
              <div key={item.label} className={styles.row}>
                <div className={styles.rowLeft}>
                  <span className={styles.rowLabel}>{item.label}</span>
                  <span className={styles.rowDesc}>{item.description}</span>
                </div>
                <span className={styles.rowPoints}>+{item.points.toLocaleString()} pts</span>
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
