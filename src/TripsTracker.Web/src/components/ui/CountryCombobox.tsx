import { useState, useRef, useEffect } from 'react';
import type { Country } from '@/types';
import styles from './CountryCombobox.module.scss';

interface Props {
  countries: Country[];
  value: string;
  onChange: (isoAlpha2: string) => void;
  required?: boolean;
}

export default function CountryCombobox({ countries, value, onChange, required }: Props) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  const selected = countries.find(c => c.isoAlpha2 === value);
  const sorted = [...countries].sort((a, b) => a.name.localeCompare(b.name));
  const filtered = sorted.filter(c =>
    !query || c.name.toLowerCase().includes(query.toLowerCase())
  );

  useEffect(() => {
    function onOutsideClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false);
        setQuery('');
      }
    }
    document.addEventListener('mousedown', onOutsideClick);
    return () => document.removeEventListener('mousedown', onOutsideClick);
  }, []);

  function handleFocus() {
    setQuery('');
    setOpen(true);
  }

  function handleSelect(c: Country) {
    onChange(c.isoAlpha2);
    setQuery('');
    setOpen(false);
  }

  const displayValue = open ? query : (selected ? `${selected.flag} ${selected.name}` : '');

  return (
    <div className={styles.wrap} ref={wrapRef}>
      <input
        type="text"
        className={styles.input}
        value={displayValue}
        onChange={e => { setQuery(e.target.value); setOpen(true); }}
        onFocus={handleFocus}
        placeholder="— select a country —"
        required={required && !value}
        autoComplete="off"
      />
      {open && filtered.length > 0 && (
        <ul className={styles.dropdown}>
          {filtered.map(c => (
            <li key={c.isoAlpha2} onMouseDown={() => handleSelect(c)}>
              <span className={styles.flag}>{c.flag}</span>
              <span className={styles.name}>{c.name}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
