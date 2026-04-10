import { useState, useEffect, useRef } from 'react';
import { useCitySuggestions } from '@/api/hooks';
import type { CitySuggestion } from '@/types';
import styles from './CityAutocomplete.module.scss';

interface Props {
  value: string;
  onChange: (value: string) => void;
  countryCode: string;
  onSelect: (suggestion: CitySuggestion) => void;
  selectedStateName?: string;
  required?: boolean;
}

export default function CityAutocomplete({ value, onChange, countryCode, onSelect, selectedStateName, required }: Props) {
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  const { data: suggestions = [] } = useCitySuggestions(debouncedQuery, countryCode);

  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(value), 300);
    return () => clearTimeout(t);
  }, [value]);

  useEffect(() => {
    function onOutsideClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setShowDropdown(false);
    }
    document.addEventListener('mousedown', onOutsideClick);
    return () => document.removeEventListener('mousedown', onOutsideClick);
  }, []);

  function handleSelect(s: CitySuggestion) {
    onSelect(s);
    setShowDropdown(false);
  }

  return (
    <div className={styles.wrap} ref={wrapRef}>
      <input
        type="text"
        className={styles.input}
        value={value}
        onChange={e => { onChange(e.target.value); setShowDropdown(true); }}
        onFocus={() => setShowDropdown(true)}
        placeholder="e.g. São Paulo"
        required={required}
        autoComplete="off"
      />
      {selectedStateName && (
        <span className={styles.stateHint}>{selectedStateName}</span>
      )}
      {showDropdown && suggestions.length > 0 && (
        <ul className={styles.dropdown}>
          {suggestions.map((s, i) => (
            <li key={i} onMouseDown={() => handleSelect(s)}>
              <span className={styles.city}>{s.city}</span>
              <span className={styles.meta}>
                {s.stateName ? `${s.stateName}, ` : ''}{s.countryName}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
