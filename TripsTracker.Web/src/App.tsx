import AppShell from '@/components/layout/AppShell';
import MapPage from '@/pages/MapPage';
import PlacesPage from '@/pages/places/PlacesPage';
import CountriesPage from '@/pages/countries/CountriesPage';

export default function App() {
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
