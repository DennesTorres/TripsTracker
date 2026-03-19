import { useState } from 'react';
import styles from './AppShell.module.scss';

type View = 'map' | 'places';

interface Props {
  children: (view: View) => React.ReactNode;
}

export default function AppShell({ children }: Props) {
  const [view, setView] = useState<View>('map');

  return (
    <div className={styles.shell}>
      <nav className={styles.nav}>
        <span className={styles.brand}>TripsTracker</span>
        <div className={styles.tabs}>
          <button
            className={`${styles.tab} ${view === 'map' ? styles.active : ''}`}
            onClick={() => setView('map')}
          >
            Map
          </button>
          <button
            className={`${styles.tab} ${view === 'places' ? styles.active : ''}`}
            onClick={() => setView('places')}
          >
            Places
          </button>
        </div>
      </nav>
      <main className={styles.main}>{children(view)}</main>
    </div>
  );
}
