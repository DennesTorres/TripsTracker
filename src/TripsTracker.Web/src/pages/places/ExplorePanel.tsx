import { useState } from 'react';
import { useExploreSearch, useExploreContent } from '@/api/hooks';
import type { ExploreLocation, PlacePhoto, PlaceComment } from '@/types';
import { Star, ThumbsUp, ThumbsDown } from 'lucide-react';
import styles from './ExplorePanel.module.scss';

interface Props {
  initialQuery?: string;
  onClose: () => void;
  onPinLocation: (location: ExploreLocation | null) => void;
  onAddPlace: (city: string, countryIsoAlpha2?: string) => void;
}

export default function ExplorePanel({ initialQuery = '', onClose, onPinLocation, onAddPlace }: Props) {
  const [query, setQuery] = useState(initialQuery);
  const [selected, setSelected] = useState<ExploreLocation | null>(null);

  const { data: locations = [], isFetching } = useExploreSearch(query);
  const { data: content, isLoading: contentLoading } = useExploreContent(
    selected?.city ?? '',
    selected?.countryId ?? null,
  );

  function handleSelect(loc: ExploreLocation) {
    setSelected(loc);
    onPinLocation(loc);
  }

  function handleBack() {
    setSelected(null);
    onPinLocation(null);
  }

  function handleClose() {
    onPinLocation(null);
    onClose();
  }

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        {selected ? (
          <button className={styles.backBtn} onClick={handleBack}>← Back</button>
        ) : (
          <span className={styles.title}>Explore places</span>
        )}
        <button className={styles.closeBtn} onClick={handleClose}>×</button>
      </div>

      {!selected ? (
        <div className={styles.searchView}>
          <input
            className={styles.searchInput}
            type="text"
            placeholder="Search city…"
            value={query}
            onChange={e => setQuery(e.target.value)}
            autoFocus
          />
          {isFetching && <p className={styles.hint}>Searching…</p>}
          {!isFetching && query.length >= 2 && locations.length === 0 && (
            <p className={styles.hint}>No places found.</p>
          )}
          <div className={styles.results}>
            {locations.map((loc, i) => (
              <button key={i} className={styles.resultRow} onClick={() => handleSelect(loc)}>
                <span className={styles.resultCity}>
                  {loc.city}{loc.stateName ? `, ${loc.stateName}` : ''} — {loc.countryName}
                </span>
                <span className={styles.resultMeta}>
                  {loc.userCount} {loc.userCount === 1 ? 'visitor' : 'visitors'}
                  {loc.photoCount > 0 ? ` · ${loc.photoCount} photos` : ''}
                  {loc.commentCount > 0 ? ` · ${loc.commentCount} comments` : ''}
                </span>
              </button>
            ))}
          </div>
        </div>
      ) : (
        <div className={styles.contentView}>
          <div className={styles.locationHeader}>
            <strong>{selected.city}{selected.stateName ? `, ${selected.stateName}` : ''}</strong>
            <span className={styles.countryName}>{selected.countryName}</span>
            <button className={styles.addBtn} onClick={() => onAddPlace(selected.city)}>
              + Add to my places
            </button>
          </div>

          {contentLoading && <p className={styles.hint}>Loading…</p>}

          {content && (
            <>
              <section className={styles.section}>
                <h3 className={styles.sectionTitle}>Photos ({content.photos.length})</h3>
                {content.photos.length === 0
                  ? <p className={styles.hint}>No photos yet.</p>
                  : content.photos.map(p => <ExplorePhotoRow key={p.id} photo={p} />)
                }
              </section>
              <section className={styles.section}>
                <h3 className={styles.sectionTitle}>Comments ({content.comments.length})</h3>
                {content.comments.length === 0
                  ? <p className={styles.hint}>No comments yet.</p>
                  : content.comments.map(c => <ExploreCommentRow key={c.id} comment={c} />)
                }
              </section>
            </>
          )}
        </div>
      )}
    </div>
  );
}

function ExplorePhotoRow({ photo }: { photo: PlacePhoto }) {
  return (
    <div className={styles.photoRow}>
      <div className={styles.photoThumb}>
        <img
          src={`${import.meta.env.VITE_API_BASE_URL}/api/photos/${photo.id}/blob`}
          alt={photo.caption || photo.originalFileName || 'Photo'}
          className={styles.thumbImg}
          onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
        />
      </div>
      <div className={styles.photoMeta}>
        <span className={styles.photoName}>{photo.caption || photo.originalFileName || 'Untitled'}</span>
        {photo.ratingCount > 0 && (
          <span className={styles.photoRating}>
            <Star size={11} /> {photo.averageRating.toFixed(1)} ({photo.ratingCount})
          </span>
        )}
      </div>
    </div>
  );
}

function ExploreCommentRow({ comment }: { comment: PlaceComment }) {
  return (
    <div className={styles.commentRow}>
      <div className={styles.commentHeader}>
        <span className={styles.commentAuthor}>{comment.userDisplayName || 'Anonymous'}</span>
        <span className={styles.commentDate}>
          {new Date(comment.createdAt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
        </span>
      </div>
      <p className={styles.commentText}>{comment.text}</p>
      {(comment.upvoteCount > 0 || comment.downvoteCount > 0) && (
        <div className={styles.commentVotes}>
          <span><ThumbsUp size={11} /> {comment.upvoteCount}</span>
          <span><ThumbsDown size={11} /> {comment.downvoteCount}</span>
        </div>
      )}
    </div>
  );
}
