import { useState, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  useExploreContent, useUploadPhoto, useDeletePhoto, useRatePhoto,
  useCreateComment, useDeleteComment, useVoteComment, useEnsureUser,
} from '@/api/hooks';
import type { PlacePhoto, PlaceComment } from '@/types';
import { Trash2, ThumbsUp, ThumbsDown, Star } from 'lucide-react';
import styles from './PlaceDetailPanel.module.scss';

interface Props {
  city: string;
  stateName?: string | null;
  countryName: string;
  countryId: number;
  placeId?: number;
  onAddToMyPlaces?: () => void;
  onClose: () => void;
}

export default function PlaceDetailPanel({ city, stateName, countryName, countryId, placeId, onAddToMyPlaces, onClose }: Props) {
  const { data: content, isLoading } = useExploreContent(city, countryId);
  const photos = content?.photos ?? [];
  const comments = content?.comments ?? [];

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <span className={styles.title}>
          {city}{stateName ? `, ${stateName}` : ''} — {countryName}
        </span>
        <button className={styles.closeBtn} onClick={onClose}>×</button>
      </div>
      {onAddToMyPlaces && (
        <div className={styles.addRow}>
          <button className={styles.addToMyPlacesBtn} onClick={onAddToMyPlaces}>
            + Add to my places
          </button>
        </div>
      )}
      <div className={styles.content}>
        <PhotosSection city={city} countryId={countryId} placeId={placeId} photos={photos} isLoading={isLoading} />
        <CommentsSection city={city} countryId={countryId} placeId={placeId} comments={comments} isLoading={isLoading} />
      </div>
    </div>
  );
}

interface SectionProps {
  city: string;
  countryId: number;
  placeId?: number;
  isLoading: boolean;
}

function PhotosSection({ city, countryId, placeId, photos, isLoading }: SectionProps & { photos: PlacePhoto[] }) {
  const qc = useQueryClient();
  const upload = useUploadPhoto();
  const deletePhoto = useDeletePhoto();
  const ratePhoto = useRatePhoto();
  const fileRef = useRef<HTMLInputElement>(null);
  const [caption, setCaption] = useState('');

  const invalidate = () => qc.invalidateQueries({ queryKey: ['explore-content', city, countryId] });

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file || placeId === undefined) return;
    upload.mutate({ placeId, file, caption: caption || undefined }, { onSuccess: invalidate });
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
          canEdit={placeId !== undefined}
          onDelete={() => deletePhoto.mutate({ photoId: photo.id, placeId: placeId! }, { onSuccess: invalidate })}
          onRate={r => ratePhoto.mutate({ photoId: photo.id, rating: r, placeId: placeId! }, { onSuccess: invalidate })}
        />
      ))}

      {placeId !== undefined && (
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
      )}
    </section>
  );
}

function PhotoRow({ photo, canEdit, onDelete, onRate }: {
  photo: PlacePhoto;
  canEdit: boolean;
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
      {canEdit && (
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
      )}
    </div>
  );
}

function CommentsSection({ city, countryId, placeId, comments, isLoading }: SectionProps & { comments: PlaceComment[] }) {
  const qc = useQueryClient();
  const { data: me } = useEnsureUser();
  const createComment = useCreateComment();
  const deleteComment = useDeleteComment();
  const voteComment = useVoteComment();
  const [text, setText] = useState('');

  const invalidate = () => qc.invalidateQueries({ queryKey: ['explore-content', city, countryId] });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = text.trim();
    if (!trimmed || placeId === undefined) return;
    createComment.mutate({ placeId, text: trimmed }, { onSuccess: invalidate });
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
          canEdit={placeId !== undefined}
          onDelete={() => deleteComment.mutate({ commentId: c.id, placeId: placeId! }, { onSuccess: invalidate })}
          onVote={up => voteComment.mutate({ commentId: c.id, isUpvote: up, placeId: placeId! }, { onSuccess: invalidate })}
        />
      ))}

      {placeId !== undefined && (
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
      )}
    </section>
  );
}

function CommentRow({ comment, isOwn, canEdit, onDelete, onVote }: {
  comment: PlaceComment;
  isOwn: boolean;
  canEdit: boolean;
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
        {isOwn && canEdit && (
          <button className={`${styles.iconBtn} ${styles.deleteBtn}`} onClick={onDelete} title="Delete comment">
            <Trash2 size={13} />
          </button>
        )}
      </div>
    </div>
  );
}
