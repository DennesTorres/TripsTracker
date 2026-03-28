import { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import { useCreatePlace, useCountries, useCitySuggestions } from '@/api/hooks';
import type { CitySuggestion } from '@/types';
import styles from './AddPlaceForm.module.scss';

interface Props {
  onClose: () => void;
}

interface MismatchSuggestion {
  suggestedCity: string;
  countryIsoAlpha2: string;
  isHome: boolean;
}

interface ResolvedCoords {
  lat: number;
  lon: number;
  stateAbbr?: string;
  stateName?: string;
}

export default function AddPlaceForm({ onClose }: Props) {
  const { data: countries = [] } = useCountries();
  const create = useCreatePlace();
  const [countryIsoAlpha2, setCountryIsoAlpha2] = useState('');
  const [cityName, setCityName] = useState('');
  const [isHome, setIsHome] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [suggestion, setSuggestion] = useState<MismatchSuggestion | null>(null);
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  // Coordinates pre-resolved from Photon when user selects from autocomplete.
  // Cleared when user manually edits the city field after selecting a suggestion.
  const [resolvedCoords, setResolvedCoords] = useState<ResolvedCoords | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const { data: citySuggestions = [] } = useCitySuggestions(debouncedQuery, countryIsoAlpha2);

  // Debounce city input for suggestions
  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(cityName), 300);
    return () => clearTimeout(t);
  }, [cityName]);

  // Close dropdown on outside click
  useEffect(() => {
    function onOutsideClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node))
        setShowDropdown(false);
    }
    document.addEventListener('mousedown', onOutsideClick);
    return () => document.removeEventListener('mousedown', onOutsideClick);
  }, []);

  function selectCitySuggestion(s: CitySuggestion) {
    setCityName(s.city);
    setCountryIsoAlpha2(s.countryIsoAlpha2);
    setShowDropdown(false);
    setError(null);
    // Capture Photon coordinates so the backend can skip Nominatim re-geocoding
    setResolvedCoords(s.lat != null && s.lon != null
      ? { lat: s.lat, lon: s.lon, stateAbbr: s.stateAbbr, stateName: s.stateName }
      : null);
  }

  const sorted = [...countries].sort((a, b) => a.name.localeCompare(b.name));

  function submitCity(city: string, country: string, home: boolean, coords?: ResolvedCoords) {
    setError(null);
    setSuggestion(null);
    create.mutate(
      { cityName: city, countryIsoAlpha2: country, isHome: home, ...coords },
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
    submitCity(cityName.trim(), countryIsoAlpha2, isHome, resolvedCoords ?? undefined);
  };

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <h3>Add place</h3>
          <button className={styles.close} onClick={onClose}>×</button>
        </div>

        <form onSubmit={handleSubmit} className={styles.form}>
          <label>
            Country
            <select
              value={countryIsoAlpha2}
              onChange={e => setCountryIsoAlpha2(e.target.value)}
              required
            >
              <option value="">— select a country —</option>
              {sorted.map(c => (
                <option key={c.isoAlpha2} value={c.isoAlpha2}>
                  {c.flag} {c.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            City
            <div className={styles.cityInputWrap} ref={dropdownRef}>
              <input
                type="text"
                value={cityName}
                onChange={e => { setCityName(e.target.value); setShowDropdown(true); setResolvedCoords(null); }}
                onFocus={() => setShowDropdown(true)}
                placeholder="e.g. São Paulo"
                required
                autoComplete="off"
              />
              {showDropdown && citySuggestions.length > 0 && (
                <ul className={styles.suggestList}>
                  {citySuggestions.map((s, i) => (
                    <li key={i} onMouseDown={() => selectCitySuggestion(s)}>
                      <span className={styles.suggestCity}>{s.city}</span>
                      <span className={styles.suggestMeta}>
                        {s.stateName ? `${s.stateName}, ` : ''}{s.countryName}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </label>

          <label className={styles.checkLabel}>
            <input
              type="checkbox"
              checked={isHome}
              onChange={e => setIsHome(e.target.checked)}
            />
            Home location
          </label>

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
      </div>
    </div>
  );
}
