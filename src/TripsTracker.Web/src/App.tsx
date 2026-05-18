import AppShell from '@/components/layout/AppShell';
import MapPage from '@/pages/MapPage';
import PlacesPage from '@/pages/places/PlacesPage';
import CountriesPage from '@/pages/countries/CountriesPage';
import { RequireAuth } from './auth/RequireAuth';
import { useEnsureUser } from './api/hooks';

function AuthenticatedApp() {
  // Ensure user record exists on first login (adopts legacy places and country flags)
  useEnsureUser();

  return (
    <AppShell>
      {view => {
        if (view === 'map') return <MapPage />;
        if (view === 'places') return <PlacesPage />;
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
