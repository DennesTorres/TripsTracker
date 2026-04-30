import { useState, useRef, useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { useEnsureUser, usePointsSummary } from '@/api/hooks';
import { User, LogOut, Star } from 'lucide-react';
import styles from './AppShell.module.scss';

export type View = 'map' | 'places' | 'countries' | 'profile';

interface Props {
  children: (view: View, navigate: (v: View) => void) => React.ReactNode;
}

const TABS: { id: View; label: string }[] = [
  { id: 'map', label: 'Map' },
  { id: 'places', label: 'Places' },
  { id: 'countries', label: 'Countries' },
];

export default function AppShell({ children }: Props) {
  const [view, setView] = useState<View>('map');
  const [menuOpen, setMenuOpen] = useState(false);
  const [pointsOpen, setPointsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const pointsRef = useRef<HTMLDivElement>(null);
  const { instance } = useMsal();
  const { data: user } = useEnsureUser();
  const { data: pointsData } = usePointsSummary();

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
      if (pointsRef.current && !pointsRef.current.contains(e.target as Node)) {
        setPointsOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const handleSignOut = () => {
    instance.logoutRedirect({ postLogoutRedirectUri: window.location.origin });
  };

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
        <div className={styles.spacer} />

        {pointsData !== undefined && (
          <div className={styles.pointsMenu} ref={pointsRef}>
            <button
              className={styles.pointsBtn}
              onClick={() => setPointsOpen(o => !o)}
              title="Your points"
            >
              <Star size={13} />
              <span>{pointsData.totalPoints.toLocaleString()}</span>
            </button>
            {pointsOpen && (
              <div className={styles.pointsDropdown}>
                <div className={styles.dropdownHeader}>
                  <span className={styles.dropdownName}>{pointsData.totalPoints.toLocaleString()} pts total</span>
                </div>
                {pointsData.recentEvents.length === 0 ? (
                  <div className={styles.pointsEmpty}>No activity yet</div>
                ) : (
                  pointsData.recentEvents.map(e => (
                    <div key={e.id} className={styles.pointsRow}>
                      <span className={styles.pointsEventType}>{formatEventType(e.eventType)}</span>
                      <span className={styles.pointsValue}>+{e.points}</span>
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        )}

        <div className={styles.userMenu} ref={menuRef}>
          <button
            className={styles.avatarBtn}
            onClick={() => setMenuOpen(o => !o)}
            title={user?.displayName ?? user?.email ?? 'Profile'}
          >
            <User size={18} />
          </button>
          {menuOpen && (
            <div className={styles.dropdown}>
              <div className={styles.dropdownHeader}>
                {user?.displayName && <div className={styles.dropdownName}>{user.displayName}</div>}
                <div className={styles.dropdownEmail}>{user?.email}</div>
              </div>
              <button className={styles.dropdownItem} onClick={() => { setView('profile'); setMenuOpen(false); }}>
                <User size={14} /> Profile
              </button>
              <button className={styles.dropdownItem} onClick={handleSignOut}>
                <LogOut size={14} /> Sign out
              </button>
            </div>
          )}
        </div>
      </nav>
      <main className={styles.main}>{children(view, setView)}</main>
    </div>
  );
}

function formatEventType(eventType: string): string {
  const map: Record<string, string> = {
    place_added: 'Place added',
    photo_uploaded: 'Photo uploaded',
    comment_added: 'Comment added',
  };
  return map[eventType] ?? eventType;
}
