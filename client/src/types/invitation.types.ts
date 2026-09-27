export interface WeddingInvitation {
  id: string;
  customerId: string;
  slug: string;
  groomName: string;
  brideName: string;
  eventDate: string;
  venueName: string;
  venueAddress: string;
  mapUrl?: string;
  loveStory?: string;
  coverImageUrl?: string;
  musicUrl?: string;
  templateStyle: string; // 'rustic' | 'minimalist' | 'luxury'
  bankInfo?: string;
  isPublished: boolean;
  createdAt: string;
  totalGuests: number;
  attendingCount: number;
  notAttendingCount: number;
  totalAccompanying: number;
  estimatedTables: number;
}

export interface SaveInvitationRequest {
  slug?: string;
  groomName: string;
  brideName: string;
  eventDate: string;
  venueName: string;
  venueAddress: string;
  mapUrl?: string;
  loveStory?: string;
  coverImageUrl?: string;
  musicUrl?: string;
  templateStyle: string;
  bankInfo?: string;
}

export interface GuestRsvp {
  id: string;
  invitationId: string;
  guestName: string;
  phoneNumber?: string;
  status: 'Attending' | 'NotAttending';
  companionCount: number;
  wishes?: string;
  dietaryPreference?: string;
  createdAt: string;
}

export interface PublicWishItem {
  guestName: string;
  wishes: string;
  createdAt: string;
}

export interface PublicInvitation {
  id: string;
  slug: string;
  groomName: string;
  brideName: string;
  eventDate: string;
  venueName: string;
  venueAddress: string;
  mapUrl?: string;
  loveStory?: string;
  coverImageUrl?: string;
  musicUrl?: string;
  templateStyle: string;
  bankInfo?: string;
  recentWishes: PublicWishItem[];
}

export interface SubmitRsvpRequest {
  guestName: string;
  phoneNumber?: string;
  status: 'Attending' | 'NotAttending';
  companionCount: number;
  wishes?: string;
  dietaryPreference?: string;
}

export interface RsvpResponse {
  success: boolean;
  message: string;
  rsvpId?: string;
}
