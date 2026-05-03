import { useState } from 'react';
import { useShareLinks, useCreateShareLink, useDeactivateShareLink } from '@/api/hooks';
import Modal from '@/components/ui/Modal';
import { Copy, Trash2, Check } from 'lucide-react';
import styles from './ShareModal.module.scss';

type ShareMode = 'public' | 'private' | 'login';

interface Props {
  onClose: () => void;
}

export default function ShareModal({ onClose }: Props) {
  const { data: links = [] } = useShareLinks();
  const createLink = useCreateShareLink();
  const deactivate = useDeactivateShareLink();
  const [copiedId, setCopiedId] = useState<number | null>(null);
  const [mode, setMode] = useState<ShareMode>('private');

  const handleCreate = () => {
    createLink.mutate({
      requiresLogin: mode === 'login',
      isDiscoverable: mode === 'public',
    });
  };

  const copyLink = (token: string, id: number) => {
    const url = `${window.location.origin}/#/shared/${token}`;
    navigator.clipboard.writeText(url);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  const modeLabel = (l: { requiresLogin: boolean; isDiscoverable: boolean }) => {
    if (!l.isDiscoverable && !l.requiresLogin) return 'Private';
    if (l.isDiscoverable) return 'Public';
    return 'Login required';
  };

  return (
    <Modal title="Share your map" onClose={onClose} width={500}>
      <div className={styles.content}>
        <p className={styles.description}>
          Generate a link to share a read-only view of your travel map.
        </p>

        <div className={styles.modeGroup}>
          <label className={`${styles.modeOption} ${mode === 'private' ? styles.modeSelected : ''}`}>
            <input type="radio" name="shareMode" value="private" checked={mode === 'private'} onChange={() => setMode('private')} />
            <span>Private link</span>
            <span className={styles.modeHint}>Anyone with the link can view — not listed in Discover</span>
          </label>
          <label className={`${styles.modeOption} ${mode === 'public' ? styles.modeSelected : ''}`}>
            <input type="radio" name="shareMode" value="public" checked={mode === 'public'} onChange={() => setMode('public')} />
            <span>Public</span>
            <span className={styles.modeHint}>Anyone can view — appears in Discover</span>
          </label>
          <label className={`${styles.modeOption} ${mode === 'login' ? styles.modeSelected : ''}`}>
            <input type="radio" name="shareMode" value="login" checked={mode === 'login'} onChange={() => setMode('login')} />
            <span>Requires login</span>
            <span className={styles.modeHint}>Only signed-in users can view</span>
          </label>
        </div>

        <button className={styles.createBtn} onClick={handleCreate} disabled={createLink.isPending}>
          {createLink.isPending ? 'Creating...' : 'Generate new link'}
        </button>

        {links.length > 0 && (
          <div className={styles.linkList}>
            {links.map(l => (
              <div key={l.id} className={`${styles.linkRow} ${!l.isActive ? styles.inactive : ''}`}>
                <span className={l.isActive ? styles.statusActive : styles.statusDisabled}>
                  {!l.isActive ? 'Disabled' : modeLabel(l)}
                </span>
                <code className={styles.token}>{`.../${l.token.slice(0, 12)}...`}</code>
                <span className={styles.views}>{l.viewCount} views</span>
                {l.isActive ? (
                  <>
                    <button
                      className={styles.iconBtn}
                      onClick={() => copyLink(l.token, l.id)}
                      title="Copy link"
                    >
                      {copiedId === l.id ? <Check size={14} /> : <Copy size={14} />}
                    </button>
                    <button
                      className={`${styles.iconBtn} ${styles.disableBtn}`}
                      onClick={() => deactivate.mutate(l.id)}
                      title="Disable link — URL will stop working"
                    >
                      <Trash2 size={14} />
                    </button>
                  </>
                ) : null}
              </div>
            ))}
          </div>
        )}
      </div>
    </Modal>
  );
}
