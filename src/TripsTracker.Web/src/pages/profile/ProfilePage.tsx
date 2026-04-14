import { useState, useEffect } from 'react';
import { useEnsureUser, useUpdateUser, useCountries } from '@/api/hooks';
import CountryCombobox from '@/components/ui/CountryCombobox';
import FormInput from '@/components/ui/FormInput';
import styles from './ProfilePage.module.scss';

export default function ProfilePage() {
  const { data: user } = useEnsureUser();
  const { data: countries = [] } = useCountries();
  const updateUser = useUpdateUser();

  const [displayName, setDisplayName] = useState('');
  const [homeCountry, setHomeCountry] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (user) {
      setDisplayName(user.displayName ?? '');
    }
  }, [user]);

  useEffect(() => {
    const home = countries.find(c => c.isHome);
    if (home) setHomeCountry(home.isoAlpha2);
  }, [countries]);

  const handleSave = () => {
    updateUser.mutate({ displayName: displayName || undefined }, {
      onSuccess: () => {
        setSaved(true);
        setTimeout(() => setSaved(false), 2000);
      },
    });
  };

  if (!user) return null;

  const joinedDate = new Date(user.createdAt).toLocaleDateString('en-GB', {
    day: 'numeric', month: 'long', year: 'numeric',
  });

  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <h2 className={styles.heading}>Profile</h2>

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
            countries={countries}
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

        <div className={styles.actions}>
          <button
            className={styles.saveBtn}
            onClick={handleSave}
            disabled={updateUser.isPending}
          >
            {updateUser.isPending ? 'Saving...' : saved ? 'Saved!' : 'Save changes'}
          </button>
        </div>
      </div>
    </div>
  );
}
