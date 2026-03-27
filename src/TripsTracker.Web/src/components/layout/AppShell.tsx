import { useState } from 'react';
import styles from './AppShell.module.scss';

export type View = 'map' | 'places' | 'countries';

interface Props {
  children: (view: View) => React.ReactNode;
}

const TABS: { id: View; label: string }[] = [
  { id: 'map', label: 'Map' },
  { id: 'places', label: 'Cities' },
  { id: 'countries', label: 'Countries' },
];

export default function AppShell({ children }: Props) {
  const [view, setView] = useState<View>('map');

  return (
    <div className={styles.shell}>
      <nav className={styles.nav}>
        <span className={styles.brand}>TripsTracker</span>
        <div className={styles.tabs}>
          {TABS.map(t => (
            <button
              key={t.id}
              className={`${styles.tab} ${view === t.id ? styles.active : ''}`}
              onClick={() => setView(t.id)}
            >
              {t.label}
            </button>
          ))}
        </div>
      </nav>
      <main className={styles.main}>{children(view)}</main>
    </div>
  );
}
