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
  isPremium?: boolean; // P2
  templateId?: string; lang?: string; contentJson?: string;
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
  templateId?: string; lang?: string; contentJson?: string; isPublished?: boolean;
}

export interface GuestRsvp {
  id: string;
  invitationId: string;
  guestName: string;
  phoneNumber?: string;
  status: 'Attending' | 'NotAttending' | 'Tentative';
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
  templateId?: string; lang?: string; contentJson?: string;
}

export interface SubmitRsvpRequest {
  guestName: string;
  phoneNumber?: string;
  status: 'Attending' | 'NotAttending' | 'Tentative';
  companionCount: number;
  wishes?: string;
  dietaryPreference?: string;
  type?: 'Attend' | 'Wish'; submissionKey?: string;
}

export interface RsvpResponse {
  success: boolean;
  message: string;
  rsvpId?: string;
}
