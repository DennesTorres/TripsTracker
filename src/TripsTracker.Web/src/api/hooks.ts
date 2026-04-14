import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { AddPlace, CitySuggestion, Country, DeletePlaceResult, Place, PlaceComment, PlacePhoto, PublicMapData, PublicShareSummary, ShareLink, UpdatePlace, UpdateUser, UserPointsSummary, UserProfile, VisitedState } from '@/types';
// VisitedState import kept — useVisitedStates still used by MapPage for map colouring
// useSetCountryVisited removed — IsVisited is now derived from Places (auto-managed by PlacesProcess)
import { decodeStrings } from '@/lib/cp1252';
import apiClient, { publicApiClient } from './client';

export function usePlaces() {
  return useQuery<Place[]>({
    queryKey: ['places'],
    queryFn: () =>
      apiClient.get<Place[]>('/api/places').then(r => r.data.map(p => decodeStrings(p))),
  });
}

export function useCreatePlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: AddPlace) => apiClient.post<Place>('/api/places', dto).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['places'] });
      qc.invalidateQueries({ queryKey: ['countries'] });
    },
  });
}

export function useUpdatePlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: UpdatePlace }) =>
      apiClient.put<Place>(`/api/places/${id}`, dto).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['places'] }),
  });
}

export function useDeletePlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiClient.delete<DeletePlaceResult>(`/api/places/${id}`).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['places'] });
      qc.invalidateQueries({ queryKey: ['countries'], refetchType: 'all' });
    },
  });
}

export function useCountries() {
  return useQuery<Country[]>({
    queryKey: ['countries'],
    queryFn: () =>
      apiClient.get<Country[]>('/api/countries').then(r => r.data.map(c => decodeStrings(c))),
  });
}

export function useCitySuggestions(query: string, countryCode = '') {
  return useQuery<CitySuggestion[]>({
    queryKey: ['city-suggestions', query, countryCode],
    queryFn: () => {
      const country = countryCode ? `&country=${encodeURIComponent(countryCode)}` : '';
      return apiClient.get<CitySuggestion[]>(`/api/cities/suggest?q=${encodeURIComponent(query)}${country}`).then(r => r.data);
    },
    enabled: query.trim().length >= 2,
    staleTime: 30_000,
    placeholderData: [],
  });
}

// ── Photos & Comments ───────────────────────────────────────────────────────

export function usePlacePhotos(placeId: number) {
  return useQuery<PlacePhoto[]>({
    queryKey: ['photos', placeId],
    queryFn: () => apiClient.get<PlacePhoto[]>(`/api/places/${placeId}/photos`).then(r => r.data),
    enabled: placeId > 0,
  });
}

export function useUploadPhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ placeId, file, caption }: { placeId: number; file: File; caption?: string }) => {
      const form = new FormData();
      form.append('file', file);
      if (caption) form.append('caption', caption);
      return apiClient.post<PlacePhoto>(`/api/places/${placeId}/photos`, form).then(r => r.data);
    },
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['photos', vars.placeId] }),
  });
}

export function useDeletePhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ photoId }: { photoId: number; placeId: number }) =>
      apiClient.delete(`/api/photos/${photoId}`),
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['photos', vars.placeId] }),
  });
}

export function useRatePhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ photoId, rating }: { photoId: number; rating: number; placeId: number }) =>
      apiClient.put(`/api/photos/${photoId}/rating`, { rating }),
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['photos', vars.placeId] }),
  });
}

export function usePlaceComments(placeId: number) {
  return useQuery<PlaceComment[]>({
    queryKey: ['comments', placeId],
    queryFn: () => apiClient.get<PlaceComment[]>(`/api/places/${placeId}/comments`).then(r => r.data),
    enabled: placeId > 0,
  });
}

export function useCreateComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ placeId, text }: { placeId: number; text: string }) =>
      apiClient.post<PlaceComment>(`/api/places/${placeId}/comments`, { text }).then(r => r.data),
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['comments', vars.placeId] }),
  });
}

export function useDeleteComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId }: { commentId: number; placeId: number }) =>
      apiClient.delete(`/api/comments/${commentId}`),
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['comments', vars.placeId] }),
  });
}

export function useVoteComment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, isUpvote }: { commentId: number; isUpvote: boolean; placeId: number }) =>
      apiClient.put(`/api/comments/${commentId}/vote`, { isUpvote }),
    onSuccess: (_d, vars) => qc.invalidateQueries({ queryKey: ['comments', vars.placeId] }),
  });
}

// ── Points ──────────────────────────────────────────────────────────────────

export function usePointsSummary() {
  return useQuery<UserPointsSummary>({
    queryKey: ['points'],
    queryFn: () => apiClient.get<UserPointsSummary>('/api/me/points').then(r => r.data),
  });
}

// ── Countries ───────────────────────────────────────────────────────────────

export function useSetStateBorders() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, show }: { id: number; show: boolean }) =>
      apiClient.put<Country>(`/api/countries/${id}/state-borders?value=${show}`).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['countries'] }),
  });
}

export function useVisitedStates() {
  return useQuery<VisitedState[]>({
    queryKey: ['visited-states'],
    queryFn: () => apiClient.get<VisitedState[]>('/api/visited-states').then(r => r.data),
  });
}

/**
 * Calls GET /api/me on mount after every login.
 * Idempotent on the backend: creates the user record on first login and adopts
 * legacy unassigned places/country flags. A no-op on subsequent logins.
 */
export function useEnsureUser() {
  return useQuery<UserProfile>({
    queryKey: ['me'],
    queryFn: () => apiClient.get<UserProfile>('/api/me').then(r => r.data),
    staleTime: Infinity,
    retry: false,
  });
}

export function useShareLinks() {
  return useQuery<ShareLink[]>({
    queryKey: ['share-links'],
    queryFn: () => apiClient.get<ShareLink[]>('/api/share-links').then(r => r.data),
  });
}

export function useCreateShareLink() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiClient.post<ShareLink>('/api/share-links', {}).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['share-links'] }),
  });
}

export function useDiscoverMaps(query: string) {
  return useQuery<PublicShareSummary[]>({
    queryKey: ['discover-maps', query],
    queryFn: () =>
      publicApiClient.get<PublicShareSummary[]>(`/api/discover?q=${encodeURIComponent(query)}`).then(r => r.data),
    staleTime: 30_000,
    placeholderData: [],
  });
}

export function useDeactivateShareLink() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiClient.delete(`/api/share-links/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['share-links'] }),
  });
}

export function useSharedMap(token: string) {
  return useQuery<PublicMapData>({
    queryKey: ['shared-map', token],
    queryFn: () => publicApiClient.get<PublicMapData>(`/api/shared/${token}`).then(r => r.data),
    enabled: !!token,
    retry: false,
  });
}

export function useUpdateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: UpdateUser) =>
      apiClient.put<UserProfile>('/api/me', dto).then(r => r.data),
    onSuccess: (data) => {
      qc.setQueryData(['me'], data);
      qc.invalidateQueries({ queryKey: ['countries'] });
      qc.invalidateQueries({ queryKey: ['discover-maps'] });
    },
  });
}
