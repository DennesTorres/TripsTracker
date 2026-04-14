import AppShell from '@/components/layout/AppShell';
import MapPage from '@/pages/MapPage';
import PlacesPage from '@/pages/places/PlacesPage';
import CountriesPage from '@/pages/countries/CountriesPage';
import ProfilePage from '@/pages/profile/ProfilePage';
import { RequireAuth } from './auth/RequireAuth';
import { useEnsureUser } from './api/hooks';

function AuthenticatedApp() {
  useEnsureUser();

  return (
    <AppShell>
      {view => {
        if (view === 'map') return <MapPage />;
        if (view === 'places') return <PlacesPage />;
        if (view === 'profile') return <ProfilePage />;
        return <CountriesPage />;
      }}
    </AppShell>
  );
}

export default function App() {
  return (
    <RequireAuth>
      <AuthenticatedApp />
    </RequireAuth>
  );
}
