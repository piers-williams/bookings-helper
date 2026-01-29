// TypeScript types matching the backend DTOs

export interface Email {
  id: number;
  senderEmail: string;
  senderName?: string;
  subject: string;
  receivedDate: string;
  isRead: boolean;
  extractedBookingRef?: string;
}

export interface EmailDetail {
  id: number;
  messageId: string;
  senderEmail: string;
  senderName?: string;
  subject: string;
  receivedDate: string;
  isRead: boolean;
  body: string;
  extractedBookingRef?: string;
  linkedBookings: Booking[];
  relatedEmails: Email[];
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
  linkedEmails: Email[];
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
