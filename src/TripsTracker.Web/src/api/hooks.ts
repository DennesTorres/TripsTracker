import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { AddPlace, CitySuggestion, DeletePlaceResult, Place, UpdatePlace, VisitedState } from '@/types';
// VisitedState import kept — useVisitedStates still used by MapPage for map colouring
// useSetCountryVisited removed — IsVisited is now derived from Places (auto-managed by PlacesProcess)
import { decodeStrings } from '@/lib/cp1252';
import apiClient from './client';

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

export function useCitySuggestions(query: string) {
  return useQuery<CitySuggestion[]>({
    queryKey: ['city-suggestions', query],
    queryFn: () =>
      apiClient.get<CitySuggestion[]>(`/api/cities/suggest?q=${encodeURIComponent(query)}`).then(r => r.data),
    enabled: query.trim().length >= 2,
    staleTime: 30_000,
    placeholderData: [],
  });
}

export function useVisitedStates() {
  return useQuery<VisitedState[]>({
    queryKey: ['visited-states'],
    queryFn: () => apiClient.get<VisitedState[]>('/api/visited-states').then(r => r.data),
  });
}
