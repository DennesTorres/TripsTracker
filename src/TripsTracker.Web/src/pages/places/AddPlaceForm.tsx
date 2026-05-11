import { useState } from 'react';
import axios from 'axios';
import { useCreatePlace, useCountries } from '@/api/hooks';
import type { CitySuggestion } from '@/types';
import Modal from '@/components/ui/Modal';
import CountryCombobox from '@/components/ui/CountryCombobox';
import CityAutocomplete from '@/components/ui/CityAutocomplete';
import FormCheckbox from '@/components/ui/FormCheckbox';
import styles from './AddPlaceForm.module.scss';

interface Props {
  onClose: () => void;
  initialCity?: string;
  onExplore?: (city: string) => void;
}

interface MismatchSuggestion {
  suggestedCity: string;
  countryIsoAlpha2: string;
  isHome: boolean;
}

export default function AddPlaceForm({ onClose, initialCity = '', onExplore }: Props) {
  const { data: countries = [] } = useCountries();
  const create = useCreatePlace();
  const [countryIsoAlpha2, setCountryIsoAlpha2] = useState('');
  const [cityName, setCityName] = useState(initialCity);
  const [selectedStateName, setSelectedStateName] = useState<string | undefined>();
  const [isHome, setIsHome] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [suggestion, setSuggestion] = useState<MismatchSuggestion | null>(null);

  function handleCitySelect(s: CitySuggestion) {
    setCityName(s.city);
    setCountryIsoAlpha2(s.countryIsoAlpha2);
    setSelectedStateName(s.stateName ?? undefined);
    setError(null);
  }

  function handleCityChange(value: string) {
    setCityName(value);
    if (selectedStateName) setSelectedStateName(undefined);
  }

  function handleCountryChange(isoAlpha2: string) {
    setCountryIsoAlpha2(isoAlpha2);
    setSelectedStateName(undefined);
  }

  function submitCity(city: string, country: string, home: boolean) {
    setError(null);
    setSuggestion(null);
    create.mutate(
      { cityName: city, countryIsoAlpha2: country, isHome: home },
      {
        onSuccess: onClose,
        onError: (err: unknown) => {
          if (axios.isAxiosError(err) && err.response?.data && typeof err.response.data === 'object') {
            const data = err.response.data as Record<string, unknown>;
            if (data['errorCode'] === 'GEOCODING_MISMATCH' && typeof data['suggestedCity'] === 'string') {
              setSuggestion({ suggestedCity: data['suggestedCity'] as string, countryIsoAlpha2: country, isHome: home });
              return;
            }
            setError(typeof data['message'] === 'string' ? data['message'] : 'Could not add place.');
          } else {
            const msg = axios.isAxiosError(err)
              ? (typeof err.response?.data === 'string' ? err.response.data : 'Could not add place.')
              : err instanceof Error ? err.message : 'Could not add place.';
            setError(msg);
          }
        },
      }
    );
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    submitCity(cityName.trim(), countryIsoAlpha2, isHome);
  };

  return (
    <Modal title="Add place" onClose={onClose}>
      <form onSubmit={handleSubmit} className={styles.form}>
        <label className={styles.fieldLabel}>
          Country
          <CountryCombobox
            countries={countries}
            value={countryIsoAlpha2}
            onChange={handleCountryChange}
            required
            allowClear
          />
        </label>

        <label className={styles.fieldLabel}>
          City
          <CityAutocomplete
            value={cityName}
            onChange={handleCityChange}
            countryCode={countryIsoAlpha2}
            onSelect={handleCitySelect}
            selectedStateName={selectedStateName}
            required
          />
        </label>

        {onExplore && cityName.trim().length >= 2 && (
          <button
            type="button"
            className={styles.exploreLink}
            onClick={() => onExplore(cityName.trim())}
          >
            Explore photos &amp; comments for this place first →
          </button>
        )}

        <FormCheckbox
          label="Home location"
          checked={isHome}
          onChange={setIsHome}
        />

        {error && <p className={styles.error}>{error}</p>}

        {suggestion && (
          <div className={styles.suggestion}>
            <p>
              <strong>'{cityName}'</strong> was not found, but <strong>'{suggestion.suggestedCity}'</strong> was.
              Use this name instead?
            </p>
            <div className={styles.suggestionActions}>
              <button type="button" onClick={() => setSuggestion(null)}>Cancel</button>
              <button
                type="button"
                className={styles.saveBtn}
                onClick={() => submitCity(suggestion.suggestedCity, suggestion.countryIsoAlpha2, suggestion.isHome)}
                disabled={create.isPending}
              >
                {create.isPending ? 'Adding…' : `Add '${suggestion.suggestedCity}'`}
              </button>
            </div>
          </div>
        )}

        {!suggestion && (
          <div className={styles.actions}>
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="submit" className={styles.saveBtn} disabled={create.isPending}>
              {create.isPending ? 'Geocoding…' : 'Add place'}
            </button>
          </div>
        )}
      </form>
    </Modal>
  );
}
