export interface Place {
  id: number;
  lon: number;
  lat: number;
  countryId: number;
  countryName: string;
  countryFlag: string;
  city: string;
  stateAbbr?: string;
  stateName?: string;
  isHome: boolean;
}

export interface DeletePlaceResult {
  promptHomeCountry: boolean;
  countryId?: number;
  countryName?: string;
}

export interface Country {
  id: number;
  isoNumeric: number;
  isoAlpha2: string;
  flag: string;
  name: string;
  region: string;
  isHome: boolean;
  isVisited: boolean;
  showStateBorders: boolean;
}

export interface UpdatePlace {
  isHome: boolean;
}

export interface AddPlace {
  cityName: string;
  countryIsoAlpha2: string;
  isHome: boolean;
}

export interface CitySuggestion {
  city: string;
  countryName: string;
  countryIsoAlpha2: string;
  stateName?: string;
  stateAbbr?: string;
}

export interface VisitedState {
  id: number;
  countryId: number;
  stateAbbr: string;
  stateName?: string;
}

export interface UserProfile {
  id: number;
  email: string;
  displayName?: string;
  createdAt: string;
}

export interface UpdateUser {
  displayName?: string;
}
