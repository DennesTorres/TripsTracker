import { useExploreContent } from '@/api/hooks';
import type { Place } from '@/types';
import styles from './PlacePopup.module.scss';

interface Props {
  places: Place[];
  x: number;
  y: number;
  onClose: () => void;
  onSeeMore: (place: Place) => void;
}

export default function PlacePopup({ places, x, y, onClose, onSeeMore }: Props) {
  return (
    <div className={styles.popup} style={{ left: x, top: y }} onClick={e => e.stopPropagation()}>
      <button className={styles.closeBtn} onClick={onClose}>×</button>
      {places.length === 1 ? (
        <SinglePreview place={places[0]} onSeeMore={() => onSeeMore(places[0])} />
      ) : (
        <div>
          <div className={styles.clusterTitle}>{places.length} places</div>
          {places.map(p => (
            <button key={p.id} className={styles.clusterItem} onClick={() => onSeeMore(p)}>
              {p.city}{p.stateName ? `, ${p.stateName}` : ''} — {p.countryName}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function SinglePreview({ place, onSeeMore }: { place: Place; onSeeMore: () => void }) {
  const { data: content } = useExploreContent(place.city, place.countryId);
  const photo = content?.photos[0];
  const comment = content?.comments[0];

  return (
    <div>
      <div className={styles.placeName}>
        <strong>{place.city}{place.stateName ? `, ${place.stateName}` : ''}</strong>
        <span className={styles.countryName}>{place.countryName}</span>
      </div>
      {photo && (
        <div className={styles.photoPreview}>
          <img
            src={`${import.meta.env.VITE_API_BASE_URL}/api/photos/${photo.id}/blob`}
            alt={photo.caption || photo.originalFileName || 'Photo'}
            className={styles.photoThumb}
            onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
          {photo.ratingCount > 0 && (
            <span className={styles.rating}>{photo.averageRating.toFixed(1)} ★</span>
          )}
        </div>
      )}
      {comment && (
        <div className={styles.commentPreview}>
          <span className={styles.commentAuthor}>{comment.userDisplayName || 'Anonymous'}</span>
          <span className={styles.commentText}>
            {comment.text.length > 80 ? `${comment.text.slice(0, 80)}…` : comment.text}
          </span>
          {comment.upvoteCount > 0 && (
            <span className={styles.commentVotes}>👍 {comment.upvoteCount}</span>
          )}
        </div>
      )}
      <div className={styles.actions}>
        <button className={styles.actionBtn} onClick={onSeeMore}>+ Add photo</button>
        <button className={styles.actionBtn} onClick={onSeeMore}>+ Add comment</button>
        <button className={styles.seeMoreBtn} onClick={onSeeMore}>See more →</button>
      </div>
    </div>
  );
}
