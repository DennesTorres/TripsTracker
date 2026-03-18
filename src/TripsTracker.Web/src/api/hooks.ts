import { useQuery } from '@tanstack/react-query';
import type { Country, Place, VisitedState } from '@/types';
import apiClient from './client';

export function usePlaces() {
  return useQuery<Place[]>({
    queryKey: ['places'],
    queryFn: () => apiClient.get<Place[]>('/api/places').then(r => r.data),
  });
}

export function useCountries() {
  return useQuery<Country[]>({
    queryKey: ['countries'],
    queryFn: () => apiClient.get<Country[]>('/api/countries').then(r => r.data),
  });
}

export function useVisitedStates() {
  return useQuery<VisitedState[]>({
    queryKey: ['visited-states'],
    queryFn: () => apiClient.get<VisitedState[]>('/api/visited-states').then(r => r.data),
  });
}
