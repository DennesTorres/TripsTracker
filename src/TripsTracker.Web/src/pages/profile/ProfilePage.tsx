import { useState, useEffect } from 'react';
import { useEnsureUser, useUpdateUser, useCountries } from '@/api/hooks';
import CountryCombobox from '@/components/ui/CountryCombobox';
import FormInput from '@/components/ui/FormInput';
import styles from './ProfilePage.module.scss';

interface Props {
  onClose?: () => void;
}

export default function ProfilePage({ onClose }: Props) {
  const { data: user } = useEnsureUser();
  const { data: countries = [] } = useCountries();
  const updateUser = useUpdateUser();

  const [displayName, setDisplayName] = useState('');
  const [homeCountry, setHomeCountry] = useState('');
  const [savedMessage, setSavedMessage] = useState('');

  useEffect(() => {
    if (user) setDisplayName(user.displayName ?? '');
  }, [user]);

  useEffect(() => {
    const home = countries.find(c => c.isHome);
    if (home) setHomeCountry(home.isoAlpha2);
  }, [countries]);

  const handleSave = () => {
    const homeCountryId = countries.find(c => c.isoAlpha2 === homeCountry)?.id;
    updateUser.mutate(
      { displayName: displayName || undefined, homeCountryId },
      {
        onSuccess: () => {
          setSavedMessage('Changes saved');
          setTimeout(() => setSavedMessage(''), 3000);
        },
        onError: () => {
          setSavedMessage('Could not save — home country must be a visited country');
          setTimeout(() => setSavedMessage(''), 5000);
        },
      }
    );
  };

  if (!user) return null;

  const visitedCountries = countries.filter(c => c.isVisited);
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
          <label className={styles.label}>Home country</label>
          <CountryCombobox
            countries={visitedCountries}
            value={homeCountry}
            onChange={setHomeCountry}
          />
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
