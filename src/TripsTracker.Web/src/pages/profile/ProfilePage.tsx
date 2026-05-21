import { useState, useEffect } from 'react';
import { useEnsureUser, useUpdateUser, usePlaces, useStorageUsage, useRefreshStorage } from '@/api/hooks';
import FormInput from '@/components/ui/FormInput';
import styles from './ProfilePage.module.scss';

function formatBytes(bytes: number): string {
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

interface Props {
  onClose?: () => void;
}

export default function ProfilePage({ onClose }: Props) {
  const { data: user } = useEnsureUser();
  const { data: places = [] } = usePlaces();
  const updateUser = useUpdateUser();
  const { data: storageUsage } = useStorageUsage();
  const refreshStorage = useRefreshStorage();

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
          setSavedMessage('Could not save changes');
          setTimeout(() => setSavedMessage(''), 5000);
        },
      }
    );
  };

  if (!user) return null;

  const homePlace = places.find(p => p.isHome);
  const homePlaceLabel = homePlace
    ? [homePlace.city, homePlace.stateName ?? homePlace.stateAbbr, homePlace.countryName].filter(Boolean).join(', ')
    : 'Not set — mark a place as Home in the Places tab';

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
          <p className={styles.readOnly}>{homePlaceLabel}</p>
        </div>

        <div className={styles.field}>
          <label className={styles.label}>Email</label>
          <p className={styles.readOnly}>{user.email}</p>
        </div>

        <div className={styles.field}>
          <label className={styles.label}>Member since</label>
          <p className={styles.readOnly}>{joinedDate}</p>
        </div>

        {storageUsage && (
          <div className={styles.field}>
            <div className={styles.storageHeader}>
              <label className={styles.label}>Photo storage</label>
              <button
                className={styles.refreshBtn}
                onClick={() => refreshStorage.mutate()}
                disabled={refreshStorage.isPending}
              >
                {refreshStorage.isPending ? 'Refreshing…' : '↻ Refresh'}
              </button>
            </div>
            <div className={styles.storageBar}>
              <div
                className={styles.storageBarFill}
                style={{ width: `${Math.min(100, (storageUsage.usedBytes / storageUsage.limitBytes) * 100)}%` }}
              />
            </div>
            <p className={styles.storageText}>
              {formatBytes(storageUsage.usedBytes)} of {formatBytes(storageUsage.limitBytes)} used
              {storageUsage.lastRefreshedAt && (
                <span className={styles.storageRefreshed}>
                  {' '}· Updated {new Date(storageUsage.lastRefreshedAt).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })}
                </span>
              )}
            </p>
          </div>
        )}

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
