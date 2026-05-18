import { useState, useRef } from 'react';
import {
  usePlacePhotos, useUploadPhoto, useDeletePhoto, useRatePhoto,
  usePlaceComments, useCreateComment, useDeleteComment, useVoteComment,
} from '@/api/hooks';
import { useEnsureUser } from '@/api/hooks';
import type { Place, PlacePhoto, PlaceComment } from '@/types';
import { Trash2, ThumbsUp, ThumbsDown, Star } from 'lucide-react';
import styles from './PlaceDetailPanel.module.scss';

interface Props {
  place: Place;
  onClose: () => void;
}

export default function PlaceDetailPanel({ place, onClose }: Props) {
  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <span className={styles.title}>
          {place.city}{place.stateName ? `, ${place.stateName}` : ''} — {place.countryName}
        </span>
        <button className={styles.closeBtn} onClick={onClose}>×</button>
      </div>
      <div className={styles.content}>
        <PhotosSection placeId={place.id} />
        <CommentsSection placeId={place.id} />
      </div>
    </div>
  );
}

function PhotosSection({ placeId }: { placeId: number }) {
  const { data: photos = [], isLoading } = usePlacePhotos(placeId);
  const upload = useUploadPhoto();
  const deletePhoto = useDeletePhoto();
  const ratePhoto = useRatePhoto();
  const fileRef = useRef<HTMLInputElement>(null);
  const [caption, setCaption] = useState('');

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    upload.mutate({ placeId, file, caption: caption || undefined });
    setCaption('');
    if (fileRef.current) fileRef.current.value = '';
  }

  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>Photos</h3>

      {isLoading && <p className={styles.empty}>Loading…</p>}
      {!isLoading && photos.length === 0 && (
        <p className={styles.empty}>No photos yet.</p>
      )}

      {photos.map(photo => (
        <PhotoRow
          key={photo.id}
          photo={photo}
          onDelete={() => deletePhoto.mutate({ photoId: photo.id, placeId })}
          onRate={r => ratePhoto.mutate({ photoId: photo.id, rating: r, placeId })}
        />
      ))}

      <div className={styles.uploadRow}>
        <input
          className={styles.captionInput}
          type="text"
          placeholder="Caption (optional)"
          value={caption}
          onChange={e => setCaption(e.target.value)}
        />
        <button
          className={styles.uploadBtn}
          onClick={() => fileRef.current?.click()}
          disabled={upload.isPending}
        >
          {upload.isPending ? 'Uploading…' : '+ Add photo'}
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="image/*"
          style={{ display: 'none' }}
          onChange={handleFileChange}
        />
      </div>
    </section>
  );
}

function PhotoRow({ photo, onDelete, onRate }: {
  photo: PlacePhoto;
  onDelete: () => void;
  onRate: (r: number) => void;
}) {
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
        <span className={styles.photoInfo}>
          {photo.ratingCount > 0
            ? `${photo.averageRating.toFixed(1)} ★ (${photo.ratingCount})`
            : 'Not rated'}
        </span>
      </div>
      <div className={styles.photoActions}>
        {[1, 2, 3, 4, 5].map(r => (
          <button key={r} className={styles.starBtn} onClick={() => onRate(r)} title={`Rate ${r}`}>
            <Star size={13} />
          </button>
        ))}
        <button className={`${styles.iconBtn} ${styles.deleteBtn}`} onClick={onDelete} title="Delete photo">
          <Trash2 size={14} />
        </button>
      </div>
    </div>
  );
}

function CommentsSection({ placeId }: { placeId: number }) {
  const { data: comments = [], isLoading } = usePlaceComments(placeId);
  const { data: me } = useEnsureUser();
  const createComment = useCreateComment();
  const deleteComment = useDeleteComment();
  const voteComment = useVoteComment();
  const [text, setText] = useState('');

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = text.trim();
    if (!trimmed) return;
    createComment.mutate({ placeId, text: trimmed });
    setText('');
  }

  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>Comments</h3>

      {isLoading && <p className={styles.empty}>Loading…</p>}
      {!isLoading && comments.length === 0 && (
        <p className={styles.empty}>No comments yet.</p>
      )}

      {comments.map(c => (
        <CommentRow
          key={c.id}
          comment={c}
          isOwn={me?.id === c.userId}
          onDelete={() => deleteComment.mutate({ commentId: c.id, placeId })}
          onVote={up => voteComment.mutate({ commentId: c.id, isUpvote: up, placeId })}
        />
      ))}

      <form className={styles.addCommentForm} onSubmit={handleSubmit}>
        <textarea
          className={styles.commentInput}
          placeholder="Add a comment…"
          value={text}
          onChange={e => setText(e.target.value)}
          rows={2}
        />
        <button
          type="submit"
          className={styles.submitBtn}
          disabled={!text.trim() || createComment.isPending}
        >
          {createComment.isPending ? 'Posting…' : 'Post'}
        </button>
      </form>
    </section>
  );
}

function CommentRow({ comment, isOwn, onDelete, onVote }: {
  comment: PlaceComment;
  isOwn: boolean;
  onDelete: () => void;
  onVote: (up: boolean) => void;
}) {
  return (
    <div className={styles.commentRow}>
      <div className={styles.commentHeader}>
        <span className={styles.commentAuthor}>{comment.userDisplayName || 'Anonymous'}</span>
        <span className={styles.commentDate}>
          {new Date(comment.createdAt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
        </span>
      </div>
      <p className={styles.commentText}>{comment.text}</p>
      <div className={styles.commentFooter}>
        <button className={styles.voteBtn} onClick={() => onVote(true)} title="Upvote">
          <ThumbsUp size={13} /> <span>{comment.upvoteCount}</span>
        </button>
        <button className={styles.voteBtn} onClick={() => onVote(false)} title="Downvote">
          <ThumbsDown size={13} /> <span>{comment.downvoteCount}</span>
        </button>
        {isOwn && (
          <button className={`${styles.iconBtn} ${styles.deleteBtn}`} onClick={onDelete} title="Delete comment">
            <Trash2 size={13} />
          </button>
        )}
      </div>
    </div>
  );
}
