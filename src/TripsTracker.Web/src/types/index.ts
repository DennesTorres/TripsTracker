export interface Place {
  id: number;
  lon: number;
  lat: number;
  flag: string;
  countryName: string;
  city: string;
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

export interface VisitedState {
  id: number;
  countryCode: string;
  stateAbbr: string;
}
