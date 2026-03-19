import AppShell from '@/components/layout/AppShell';
import MapPage from '@/pages/MapPage';
import PlacesPage from '@/pages/places/PlacesPage';

export default function App() {
  return (
    <AppShell>
      {view => view === 'map' ? <MapPage /> : <PlacesPage />}
    </AppShell>
  );
}
