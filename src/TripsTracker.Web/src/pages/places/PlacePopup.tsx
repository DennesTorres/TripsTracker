import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useExploreContent, useRatePhoto, useUploadPhoto, useVoteComment } from '@/api/hooks';
import type { Place } from '@/types';
import { Star, ThumbsDown, ThumbsUp } from 'lucide-react';
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
  const qc = useQueryClient();
  const { data: content } = useExploreContent(place.city, place.countryId);
  const photos = content?.photos ?? [];
  const topLevelComments = (content?.comments ?? []).filter(c => !c.parentCommentId);
  const [photoIndex, setPhotoIndex] = useState(0);
  const [commentIndex, setCommentIndex] = useState(0);
  const fileRef = useRef<HTMLInputElement>(null);
  const justUploadedRef = useRef(false);

  const ratePhoto = useRatePhoto();
  const uploadPhoto = useUploadPhoto();
  const voteComment = useVoteComment();

  const photo = photos.length > 0 ? photos[Math.min(photoIndex, photos.length - 1)] : undefined;
  const comment = topLevelComments.length > 0 ? topLevelComments[Math.min(commentIndex, topLevelComments.length - 1)] : undefined;

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['explore-content', place.city, place.countryId] });
  };

  useEffect(() => {
    if (justUploadedRef.current && photos.length > 0) {
      setPhotoIndex(photos.length - 1);
      justUploadedRef.current = false;
    }
  }, [photos.length]);

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    uploadPhoto.mutate(
      { placeId: place.id, file },
      { onSuccess: () => { invalidate(); justUploadedRef.current = true; } }
    );
    if (fileRef.current) fileRef.current.value = '';
  }

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
          <div className={styles.photoStars}>
            {[1, 2, 3, 4, 5].map(r => (
              <button
                key={r}
                className={`${styles.starBtn} ${r <= (photo.currentUserRating ?? 0) ? styles.starBtnActive : ''}`}
                onClick={() => ratePhoto.mutate({ photoId: photo.id, rating: r, placeId: place.id }, { onSuccess: invalidate })}
                disabled={ratePhoto.isPending}
                title={`Rate ${r}`}
              >
                <Star size={11} />
              </button>
            ))}
          </div>
          {photos.length > 1 && (
            <div className={styles.photoNav}>
              <button
                className={styles.navBtn}
                onClick={() => setPhotoIndex(i => Math.max(0, i - 1))}
                disabled={photoIndex === 0}
              >‹</button>
              <span className={styles.navCount}>{photoIndex + 1}/{photos.length}</span>
              <button
                className={styles.navBtn}
                onClick={() => setPhotoIndex(i => Math.min(photos.length - 1, i + 1))}
                disabled={photoIndex >= photos.length - 1}
              >›</button>
            </div>
          )}
        </div>
      )}
      {comment && (
        <div className={styles.commentPreview}>
          <div className={styles.commentHeader}>
            <span className={styles.commentAuthor}>{comment.userDisplayName || 'Anonymous'}</span>
            {topLevelComments.length > 1 && (
              <div className={styles.commentNav}>
                <button
                  className={styles.navBtn}
                  onClick={() => setCommentIndex(i => Math.max(0, i - 1))}
                  disabled={commentIndex === 0}
                >‹</button>
                <span className={styles.navCount}>{commentIndex + 1}/{topLevelComments.length}</span>
                <button
                  className={styles.navBtn}
                  onClick={() => setCommentIndex(i => Math.min(topLevelComments.length - 1, i + 1))}
                  disabled={commentIndex >= topLevelComments.length - 1}
                >›</button>
              </div>
            )}
          </div>
          <span className={styles.commentText}>
            {comment.text.length > 80 ? `${comment.text.slice(0, 80)}…` : comment.text}
          </span>
          <div className={styles.commentVoteRow}>
            <button
              className={`${styles.voteBtn} ${comment.currentUserVote === true ? styles.voteBtnActive : ''}`}
              onClick={() => voteComment.mutate({ commentId: comment.id, isUpvote: true, placeId: place.id }, { onSuccess: invalidate })}
              disabled={voteComment.isPending}
              title="Upvote"
            >
              <ThumbsUp size={11} /> <span>{comment.upvoteCount}</span>
            </button>
            <button
              className={`${styles.voteBtn} ${comment.currentUserVote === false ? styles.voteBtnActive : ''}`}
              onClick={() => voteComment.mutate({ commentId: comment.id, isUpvote: false, placeId: place.id }, { onSuccess: invalidate })}
              disabled={voteComment.isPending}
              title="Downvote"
            >
              <ThumbsDown size={11} /> <span>{comment.downvoteCount}</span>
            </button>
          </div>
        </div>
      )}
      <div className={styles.actions}>
        <button
          className={styles.actionBtn}
          onClick={() => fileRef.current?.click()}
          disabled={uploadPhoto.isPending}
        >
          {uploadPhoto.isPending ? 'Uploading…' : '+ Add photo'}
        </button>
        <input ref={fileRef} type="file" accept="image/*" style={{ display: 'none' }} onChange={handleFileChange} />
        <button className={styles.actionBtn} onClick={onSeeMore}>+ Add comment</button>
        <button className={styles.seeMoreBtn} onClick={onSeeMore}>See more →</button>
      </div>
    </div>
  );
}
