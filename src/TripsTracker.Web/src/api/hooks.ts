import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Country, Place, SavePlace, VisitedState } from '@/types';
// VisitedState import kept — useVisitedStates still used by MapPage for map colouring
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
    mutationFn: (dto: SavePlace) => apiClient.post<Place>('/api/places', dto).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['places'] }),
  });
}

export function useUpdatePlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: number; dto: SavePlace }) =>
      apiClient.put<Place>(`/api/places/${id}`, dto).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['places'] }),
  });
}

export function useDeletePlace() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiClient.delete(`/api/places/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['places'] }),
  });
}

export function useCountries() {
  return useQuery<Country[]>({
    queryKey: ['countries'],
    queryFn: () =>
      apiClient.get<Country[]>('/api/countries').then(r => r.data.map(c => decodeStrings(c))),
  });
}

export function useSetCountryVisited() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isVisited }: { id: number; isVisited: boolean }) =>
      apiClient.put<Country>(`/api/countries/${id}/visited?value=${isVisited}`).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['countries'] }),
  });
}

export function useSetCountryHome() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiClient.put<Country>(`/api/countries/${id}/home`).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['countries'] }),
  });
}

export function useVisitedStates() {
  return useQuery<VisitedState[]>({
    queryKey: ['visited-states'],
    queryFn: () => apiClient.get<VisitedState[]>('/api/visited-states').then(r => r.data),
  });
}

