import { useState, useEffect } from 'react';
import { useEnsureUser, useUpdateUser, usePlaces, useCountries, useCreatePlace, useUpdatePlace } from '@/api/hooks';
import FormInput from '@/components/ui/FormInput';
import CityAutocomplete from '@/components/ui/CityAutocomplete';
import type { CitySuggestion } from '@/types';
import styles from './ProfilePage.module.scss';

interface Props {
  onClose?: () => void;
}

export default function ProfilePage({ onClose }: Props) {
  const { data: user } = useEnsureUser();
  const { data: places = [] } = usePlaces();
  const { data: countries = [] } = useCountries();
  const updateUser = useUpdateUser();
  const createPlace = useCreatePlace();
  const updatePlace = useUpdatePlace();

  const [displayName, setDisplayName] = useState('');
  const [savedMessage, setSavedMessage] = useState('');
  const [homeQuery, setHomeQuery] = useState('');
  const [homeStateName, setHomeStateName] = useState('');
  const [homeMessage, setHomeMessage] = useState('');

  useEffect(() => {
    if (user) setDisplayName(user.displayName ?? '');
  }, [user]);

  const handleSave = () => {
    updateUser.mutate(
      { displayName: displayName || undefined },
      {
        onSuccess: () => {
          setSavedMessage('Changes saved');
          setTimeout(() => setSavedMessage(''), 3000);
        },
        onError: () => {
          setSavedMessage('Could not save');
          setTimeout(() => setSavedMessage(''), 5000);
        },
      }
    );
  };

  const handleHomeSelect = (suggestion: CitySuggestion) => {
    setHomeQuery(suggestion.city);
    setHomeStateName(suggestion.stateName ?? '');
    const country = countries.find(c => c.isoAlpha2 === suggestion.countryIsoAlpha2);
    const countryId = country?.id;
    const existing = countryId !== undefined
      ? places.find(p => p.city.toLowerCase() === suggestion.city.toLowerCase() && p.countryId === countryId)
      : undefined;

    const onSuccess = () => {
      setHomeQuery('');
      setHomeStateName('');
      setHomeMessage('Home place updated');
      setTimeout(() => setHomeMessage(''), 3000);
    };
    const onError = () => {
      setHomeMessage('Could not update home place');
      setTimeout(() => setHomeMessage(''), 5000);
    };

    if (existing) {
      // Case 1: already visited — set IsHome on the existing UserPlaces row
      updatePlace.mutate({ id: existing.id, dto: { isHome: true } }, { onSuccess, onError });
    } else {
      // Case 2: not yet visited — create UserPlaces row with IsHome=true
      createPlace.mutate({ cityName: suggestion.city, countryIsoAlpha2: suggestion.countryIsoAlpha2, isHome: true }, { onSuccess, onError });
    }
  };

  if (!user) return null;

  const homePlace = places.find(p => p.isHome);
  const joinedDate = new Date(user.createdAt).toLocaleDateString('en-GB', {
    day: 'numeric', month: 'long', year: 'numeric',
  });

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h2 className={styles.heading}>Edit Profile</h2>
          {onClose && (
            <button className={styles.closeBtn} onClick={onClose} title="Back to map">✕</button>
          )}
        </div>

        <div className={styles.field}>
          <FormInput
            label="Display name"
            value={displayName}
            onChange={setDisplayName}
            placeholder="How you appear to others"
          />
        </div>

        <div className={styles.field}>
          <label className={styles.label}>Home place</label>
          {homePlace && (
            <p className={styles.readOnly}>
              {homePlace.city}{homePlace.stateName ? `, ${homePlace.stateName}` : ''}, {homePlace.countryName}
            </p>
          )}
          <CityAutocomplete
            value={homeQuery}
            onChange={val => { setHomeQuery(val); setHomeStateName(''); }}
            countryCode=""
            onSelect={handleHomeSelect}
            selectedStateName={homeStateName}
          />
          {homeMessage && (
            <p className={`${styles.savedMsg} ${homeMessage.startsWith('Could') ? styles.savedMsgError : styles.savedMsgOk}`}>
              {homeMessage}
            </p>
          )}
        </div>

        <div className={styles.field}>
          <label className={styles.label}>Email</label>
          <p className={styles.readOnly}>{user.email}</p>
        </div>

        <div className={styles.field}>
          <label className={styles.label}>Member since</label>
          <p className={styles.readOnly}>{joinedDate}</p>
        </div>

        {savedMessage && (
          <p className={`${styles.savedMsg} ${savedMessage.startsWith('Could') ? styles.savedMsgError : styles.savedMsgOk}`}>
            {savedMessage}
          </p>
        )}

        <div className={styles.actions}>
          <button
            className={styles.saveBtn}
            onClick={handleSave}
            disabled={updateUser.isPending}
          >
            {updateUser.isPending ? 'Saving...' : 'Save changes'}
          </button>
        </div>
      </div>
    </div>
  );
}
