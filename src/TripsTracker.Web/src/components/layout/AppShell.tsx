import { useState, useRef, useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { useEnsureUser } from '@/api/hooks';
import { User, LogOut } from 'lucide-react';
import styles from './AppShell.module.scss';
import { MapStatusProvider } from '@/context/MapStatusContext';
import { BorderGeoCacheProvider } from '@/context/BorderGeoCacheContext';

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
  const menuRef = useRef<HTMLDivElement>(null);
  const { instance } = useMsal();
  const { data: user } = useEnsureUser();

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
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
      <main className={styles.main}>
        <BorderGeoCacheProvider>
          <MapStatusProvider>{children(view, setView)}</MapStatusProvider>
        </BorderGeoCacheProvider>
      </main>
    </div>
  );
}
