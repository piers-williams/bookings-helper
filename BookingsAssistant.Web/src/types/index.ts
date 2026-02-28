// TypeScript types matching the backend DTOs

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Email {
  id: number;
  senderName?: string;
  subject: string;
  receivedDate: string;
  isRead: boolean;
  extractedBookingRef?: string;
}

export interface Booking {
  id: number;
  osmBookingId: string;
  customerName: string;
  customerEmail?: string;
  startDate: string;
  endDate: string;
  status: string;
}

// Summary of a linked email as returned by GET /api/bookings/{id}/links
export interface LinkedEmail {
  id: number;
  senderName?: string;
  subject: string;
  receivedDate: string;
  isRead: boolean;
  extractedBookingRef?: string;
}

export interface BookingDetail {
  id: number;
  osmBookingId: string;
  customerName: string;
  customerEmail?: string;
  startDate: string;
  endDate: string;
  status: string;
  fullDetails: string;
  comments: Comment[];
  linkedEmails: LinkedEmail[];
}

export interface Comment {
  id: number;
  osmBookingId: string;
  osmCommentId: string;
  authorName: string;
  textPreview: string;
  createdDate: string;
  isNew: boolean;
  booking?: Booking;
}

export interface Link {
  id: number;
  emailMessageId: number;
  osmBookingId: number;
  createdByUserId?: number;
  createdDate: string;
  isAutoLinked: boolean;
}

export interface CreateLinkRequest {
  emailMessageId: number;
  osmBookingId: number;
}

export interface BookingStats {
  onSiteNow: number;
  arrivingThisWeek: number;
  arrivingNext30Days: number;
  provisional: number;
  lastSynced: string | null;
}
