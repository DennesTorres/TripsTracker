import { useState, useEffect } from 'react';
import { useEnsureUser, useUpdateUser, usePlaces } from '@/api/hooks';
import FormInput from '@/components/ui/FormInput';
import styles from './ProfilePage.module.scss';

interface Props {
  onClose?: () => void;
  onNavigateToPlaces?: () => void;
}

export default function ProfilePage({ onClose, onNavigateToPlaces }: Props) {
  const { data: user } = useEnsureUser();
  const { data: places = [] } = usePlaces();
  const updateUser = useUpdateUser();

  const [displayName, setDisplayName] = useState('');
  const [savedMessage, setSavedMessage] = useState('');

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
          {homePlace ? (
            <p className={styles.readOnly}>
              {homePlace.city}{homePlace.stateName ? `, ${homePlace.stateName}` : ''}, {homePlace.countryName}
              {onNavigateToPlaces && (
                <> &mdash; <button className={styles.linkBtn} onClick={onNavigateToPlaces}>Change in Places tab</button></>
              )}
            </p>
          ) : (
            <p className={styles.readOnly}>
              No home place set.
              {onNavigateToPlaces && (
                <> <button className={styles.linkBtn} onClick={onNavigateToPlaces}>Set in Places tab</button></>
              )}
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
