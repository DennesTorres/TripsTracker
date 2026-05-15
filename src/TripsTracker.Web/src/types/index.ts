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
  isoAlpha3?: string;
  flag: string;
  name: string;
  region: string;
  isHome: boolean;
  isVisited: boolean;
  showStateBorders: boolean;
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
  isDiscoverable: boolean;
}

export interface UpdateUser {
  displayName?: string;
  homeCountryId?: number;
  isDiscoverable?: boolean;
}

export interface ShareLink {
  id: number;
  token: string;
  isActive: boolean;
  createdAt: string;
  expiresAt?: string;
  viewCount: number;
}

export interface PlacePhoto {
  id: number;
  placeId: number;
  userId: number;
  originalFileName?: string;
  contentType: string;
  sizeBytes: number;
  caption?: string;
  sortOrder: number;
  uploadedAt: string;
  averageRating: number;
  ratingCount: number;
  currentUserRating?: number | null;
}

export interface PlaceComment {
  id: number;
  placeId: number;
  userId: number;
  userDisplayName: string;
  text: string;
  createdAt: string;
  updatedAt?: string;
  upvoteCount: number;
  downvoteCount: number;
  parentCommentId?: number | null;
  currentUserVote?: boolean | null;
}

export interface PublicShareSummary {
  token: string;
  displayName: string;
  continentsVisited: number;
  countriesVisited: number;
  placesCount: number;
}

export interface PublicMapData {
  ownerDisplayName: string;
  places: Place[];
  countries: Country[];
  visitedStates: VisitedState[];
}

export interface ExploreLocation {
  city: string;
  stateName: string | null;
  countryName: string;
  countryId: number;
  lat: number;
  lon: number;
  userCount: number;
  photoCount: number;
  commentCount: number;
}

export interface ExploreContent {
  photos: PlacePhoto[];
  comments: PlaceComment[];
}

export interface StorageUsage {
  usedBytes: number;
  limitBytes: number;
  lastRefreshedAt: string | null;
}
