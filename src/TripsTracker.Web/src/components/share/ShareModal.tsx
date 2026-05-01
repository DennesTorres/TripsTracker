import { useState } from 'react';
import { useShareLinks, useCreateShareLink, useDeactivateShareLink } from '@/api/hooks';
import Modal from '@/components/ui/Modal';
import { Copy, Trash2, Check } from 'lucide-react';
import styles from './ShareModal.module.scss';

interface Props {
  onClose: () => void;
}

export default function ShareModal({ onClose }: Props) {
  const { data: links = [] } = useShareLinks();
  const createLink = useCreateShareLink();
  const deactivate = useDeactivateShareLink();
  const [copiedId, setCopiedId] = useState<number | null>(null);
  const [requiresLogin, setRequiresLogin] = useState(false);

  const handleCreate = () => createLink.mutate({ requiresLogin });

  const copyLink = (token: string, id: number) => {
    const url = `${window.location.origin}/#/shared/${token}`;
    navigator.clipboard.writeText(url);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  return (
    <Modal title="Share your map" onClose={onClose} width={500}>
      <div className={styles.content}>
        <p className={styles.description}>
          Generate a link to share a read-only view of your travel map.
        </p>

        <label className={styles.loginCheck}>
          <input
            type="checkbox"
            checked={requiresLogin}
            onChange={e => setRequiresLogin(e.target.checked)}
          />
          Require login to view
        </label>

        <button className={styles.createBtn} onClick={handleCreate} disabled={createLink.isPending}>
          {createLink.isPending ? 'Creating...' : 'Generate new link'}
        </button>

        {links.length > 0 && (
          <div className={styles.linkList}>
            {links.map(l => (
              <div key={l.id} className={`${styles.linkRow} ${!l.isActive ? styles.inactive : ''}`}>
                <span className={l.isActive ? styles.statusActive : styles.statusDisabled}>
                  {!l.isActive ? 'Disabled' : l.requiresLogin ? 'Login required' : 'Public'}
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
