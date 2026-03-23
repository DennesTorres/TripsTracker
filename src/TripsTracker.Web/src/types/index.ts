export interface Place {
  id: number;
  lon: number;
  lat: number;
  countryId: number;
  countryName: string;
  countryFlag: string;
  city: string;
  stateAbbr?: string;
  isHome: boolean;
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
}

export interface UpdatePlace {
  city: string;
  isHome: boolean;
}

export interface AddPlace {
  cityName: string;
  countryIsoAlpha2: string;
  isHome: boolean;
}

export interface VisitedState {
  id: number;
  countryId: number;
  stateAbbr: string;
}
