import { useState, useEffect } from 'react';
import AppShell from '@/components/layout/AppShell';
import MapPage from '@/pages/MapPage';
import PlacesPage from '@/pages/places/PlacesPage';
import CountriesPage from '@/pages/countries/CountriesPage';
import ProfilePage from '@/pages/profile/ProfilePage';
import SharedMapPage from '@/pages/shared/SharedMapPage';
import { RequireAuth } from './auth/RequireAuth';
import { useEnsureUser } from './api/hooks';

function AuthenticatedApp() {
  useEnsureUser();

  return (
    <AppShell>
      {(view, navigate) => {
        if (view === 'map') return <MapPage />;
        if (view === 'places') return <PlacesPage />;
        if (view === 'profile') return <ProfilePage onClose={() => navigate('map')} />;
        return <CountriesPage />;
      }}
    </AppShell>
  );
}

function useHashRoute() {
  const [hash, setHash] = useState(window.location.hash);
  useEffect(() => {
    const handler = () => setHash(window.location.hash);
    window.addEventListener('hashchange', handler);
    return () => window.removeEventListener('hashchange', handler);
  }, []);
  return hash;
}

export default function App() {
  const hash = useHashRoute();

  // Public shared map route: /#/shared/{token}
  const sharedMatch = hash.match(/^#\/shared\/(.+)$/);
  if (sharedMatch) {
    return <SharedMapPage token={sharedMatch[1]} />;
  }

  return (
    <RequireAuth>
      <AuthenticatedApp />
    </RequireAuth>
  );
}
