import { useState, useRef, useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { useEnsureUser, usePointsSummary } from '@/api/hooks';
import { User, LogOut, Star, Trophy } from 'lucide-react';
import LeaderboardModal from './LeaderboardModal';
import PointsStatementPanel from './PointsStatementPanel';
import PointsSummaryPopup from './PointsSummaryPopup';
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
  const [summaryOpen, setSummaryOpen] = useState(false);
  const [statementOpen, setStatementOpen] = useState(false);
  const [statementUserId, setStatementUserId] = useState<number | null>(null);
  const [leaderboardOpen, setLeaderboardOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const { instance } = useMsal();
  const { data: user } = useEnsureUser();
  const { data: pointsData } = usePointsSummary();

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener('click', handler);
    return () => document.removeEventListener('click', handler);
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
          <button
            className={styles.pointsBtn}
            onClick={() => {
              if (!summaryOpen) {
                setStatementOpen(false);
                setStatementUserId(null);
                setLeaderboardOpen(false);
              }
              setSummaryOpen(o => !o);
            }}
            title="Your points"
          >
            <Star size={13} />
            <span>{pointsData.totalPoints.toLocaleString()}</span>
          </button>
        )}

        <button
          className={styles.leaderboardBtn}
          onClick={() => { setSummaryOpen(false); setLeaderboardOpen(true); }}
          title="Leaderboard"
        >
          <Trophy size={15} />
        </button>

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
      {summaryOpen && (
        <PointsSummaryPopup
          onClose={() => setSummaryOpen(false)}
          onViewFull={() => { setSummaryOpen(false); setStatementUserId(user?.id ?? null); setStatementOpen(true); }}
        />
      )}
      {leaderboardOpen && (
        <LeaderboardModal
          onClose={() => setLeaderboardOpen(false)}
          onViewUser={(id) => { setLeaderboardOpen(false); setStatementUserId(id); setStatementOpen(true); }}
        />
      )}
      {statementOpen && statementUserId !== null && (
        <PointsStatementPanel userId={statementUserId} onClose={() => { setStatementOpen(false); setStatementUserId(null); }} />
      )}
    </div>
  );
}
