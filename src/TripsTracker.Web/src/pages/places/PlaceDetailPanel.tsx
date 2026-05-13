import { useState, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  useExploreContent, useUploadPhoto, useDeletePhoto, useRatePhoto,
  useCreateComment, useCreateReply, useDeleteComment, useVoteComment, useEnsureUser,
} from '@/api/hooks';
import type { PlacePhoto, PlaceComment } from '@/types';
import { Trash2, ThumbsUp, ThumbsDown, Star, ChevronLeft, ChevronRight } from 'lucide-react';
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
  const { data: me } = useEnsureUser();
  const upload = useUploadPhoto();
  const deletePhoto = useDeletePhoto();
  const ratePhoto = useRatePhoto();
  const fileRef = useRef<HTMLInputElement>(null);
  const [caption, setCaption] = useState('');
  const [photoIndex, setPhotoIndex] = useState(0);

  const invalidate = () => qc.invalidateQueries({ queryKey: ['explore-content', city, countryId] });

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file || placeId === undefined) return;
    upload.mutate({ placeId, file, caption: caption || undefined }, { onSuccess: invalidate });
    setCaption('');
    if (fileRef.current) fileRef.current.value = '';
  }

  const safeIndex = photos.length > 0 ? Math.min(photoIndex, photos.length - 1) : 0;
  const photo = photos[safeIndex];

  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>Photos</h3>

      {isLoading && <p className={styles.empty}>Loading…</p>}
      {!isLoading && photos.length === 0 && (
        <p className={styles.empty}>No photos yet.</p>
      )}

      {photo && (
        <div className={styles.photoSlider}>
          <img
            src={`${import.meta.env.VITE_API_BASE_URL}/api/photos/${photo.id}/blob`}
            alt={photo.caption || photo.originalFileName || 'Photo'}
            className={styles.sliderImg}
            onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
          {photos.length > 1 && (
            <div className={styles.sliderControls}>
              <button
                className={styles.sliderBtn}
                onClick={() => setPhotoIndex(i => Math.max(0, i - 1))}
                disabled={safeIndex === 0}
              >
                <ChevronLeft size={16} />
              </button>
              <span className={styles.sliderCount}>{safeIndex + 1} / {photos.length}</span>
              <button
                className={styles.sliderBtn}
                onClick={() => setPhotoIndex(i => Math.min(photos.length - 1, i + 1))}
                disabled={safeIndex === photos.length - 1}
              >
                <ChevronRight size={16} />
              </button>
            </div>
          )}
          <div className={styles.sliderMeta}>
            <span className={styles.photoName}>{photo.caption || photo.originalFileName || 'Untitled'}</span>
            <span className={styles.photoInfo}>
              {photo.ratingCount > 0
                ? `${photo.averageRating.toFixed(1)} ★ (${photo.ratingCount})`
                : 'Not rated'}
            </span>
          </div>
          <div className={styles.photoActions}>
            {[1, 2, 3, 4, 5].map(r => (
              <button key={r} className={styles.starBtn} onClick={() => ratePhoto.mutate({ photoId: photo.id, rating: r, placeId: placeId! }, { onSuccess: invalidate })} title={`Rate ${r}`}>
                <Star size={13} />
              </button>
            ))}
            {me?.id === photo.userId && (
              <button
                className={`${styles.iconBtn} ${styles.deleteBtn}`}
                onClick={() => deletePhoto.mutate({ photoId: photo.id, placeId: placeId! }, { onSuccess: invalidate })}
                title="Delete photo"
              >
                <Trash2 size={14} />
              </button>
            )}
          </div>
        </div>
      )}

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

function CommentsSection({ city, countryId, placeId, comments, isLoading }: SectionProps & { comments: PlaceComment[] }) {
  const qc = useQueryClient();
  const { data: me } = useEnsureUser();
  const createComment = useCreateComment();
  const createReply = useCreateReply();
  const deleteComment = useDeleteComment();
  const voteComment = useVoteComment();
  const [text, setText] = useState('');
  const [replyingTo, setReplyingTo] = useState<number | null>(null);
  const [replyText, setReplyText] = useState('');

  const invalidate = () => qc.invalidateQueries({ queryKey: ['explore-content', city, countryId] });

  const topLevel = comments.filter(c => !c.parentCommentId);
  const repliesFor = (id: number) => comments.filter(c => c.parentCommentId === id);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = text.trim();
    if (!trimmed || placeId === undefined) return;
    createComment.mutate({ placeId, text: trimmed }, { onSuccess: invalidate });
    setText('');
  }

  function handleReplySubmit(parentId: number) {
    const trimmed = replyText.trim();
    if (!trimmed || placeId === undefined) return;
    createReply.mutate({ parentCommentId: parentId, text: trimmed, placeId }, { onSuccess: () => { invalidate(); setReplyingTo(null); setReplyText(''); } });
  }

  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>Comments</h3>

      {isLoading && <p className={styles.empty}>Loading…</p>}
      {!isLoading && topLevel.length === 0 && (
        <p className={styles.empty}>No comments yet.</p>
      )}

      {topLevel.map(c => (
        <div key={c.id}>
          <CommentRow
            comment={c}
            isOwn={me?.id === c.userId}
            canEdit={placeId !== undefined}
            onDelete={() => deleteComment.mutate({ commentId: c.id, placeId: placeId! }, { onSuccess: invalidate })}
            onVote={up => voteComment.mutate({ commentId: c.id, isUpvote: up, placeId: placeId! }, { onSuccess: invalidate })}
            onReply={placeId !== undefined ? () => { setReplyingTo(c.id); setReplyText(''); } : undefined}
          />
          {repliesFor(c.id).map(r => (
            <div key={r.id} className={styles.replyRow}>
              <CommentRow
                comment={r}
                isOwn={me?.id === r.userId}
                canEdit={placeId !== undefined}
                onDelete={() => deleteComment.mutate({ commentId: r.id, placeId: placeId! }, { onSuccess: invalidate })}
                onVote={up => voteComment.mutate({ commentId: r.id, isUpvote: up, placeId: placeId! }, { onSuccess: invalidate })}
              />
            </div>
          ))}
          {replyingTo === c.id && (
            <div className={styles.replyForm}>
              <textarea
                className={styles.commentInput}
                placeholder="Write a reply…"
                value={replyText}
                onChange={e => setReplyText(e.target.value)}
                rows={2}
                autoFocus
              />
              <div className={styles.replyFormBtns}>
                <button className={styles.submitBtn} onClick={() => handleReplySubmit(c.id)} disabled={!replyText.trim() || createReply.isPending}>
                  {createReply.isPending ? 'Posting…' : 'Reply'}
                </button>
                <button className={styles.cancelBtn} onClick={() => setReplyingTo(null)}>Cancel</button>
              </div>
            </div>
          )}
        </div>
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

function CommentRow({ comment, isOwn, canEdit, onDelete, onVote, onReply }: {
  comment: PlaceComment;
  isOwn: boolean;
  canEdit: boolean;
  onDelete: () => void;
  onVote: (up: boolean) => void;
  onReply?: () => void;
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
        {onReply && (
          <button className={styles.replyBtn} onClick={onReply}>Reply</button>
        )}
        {isOwn && canEdit && (
          <button className={`${styles.iconBtn} ${styles.deleteBtn}`} onClick={onDelete} title="Delete comment">
            <Trash2 size={13} />
          </button>
        )}
      </div>
    </div>
  );
}
