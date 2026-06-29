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

// Mirrors GateCodeStatusEvaluator on the backend. Each value is the precise
// reason a gate code has — or hasn't — been sent.
export type GateCodeStatus =
  | 'sent'
  | 'not_required'
  | 'awaiting_confirmation'
  | 'arrival_passed'
  | 'scheduled'
  | 'pending';

export interface Booking {
  id: number;
  osmBookingId: string;
  customerName: string;
  customerEmail?: string;
  startDate: string;
  endDate: string;
  status: string;
  gateCodeSentAt?: string | null;
  gateCodeStatus?: GateCodeStatus;
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

export interface BookingItem {
  itemId: string;
  /** "site" or "activity" */
  type: string;
  siteId?: string;
  activityId?: string;
  startDate?: string;
  endDate?: string;
  startTime?: string;
  endTime?: string;
  label: string;
}

/** Result of a booking mutation operation (item replacement). Mirrors BookingActionResult on the backend. */
export interface BookingActionResult {
  /** Ids of items successfully created during this operation. */
  created: string[];
  /** Ids of items successfully deleted during this operation. */
  deleted: string[];
  /** One of: "completed", "completed_with_warnings", "rolled_back", "failed". */
  status: string;
  /** Human-readable explanation of the outcome. */
  message: string;
  /** The booking's items after the operation completes. */
  items: BookingItem[];
}

/** Request to move an activity item (change time or date). */
export interface MoveActivityRequest {
  itemId: string;
  newStartDate?: string;
  newStartTime?: string;
  newEndTime?: string;
}

/** Request to move a site item to a different site. */
export interface ChangeSiteRequest {
  itemId: string;
  newSiteId: string;
}

/** Request to shift all items in a booking by the given number of days. */
export interface MoveDatesRequest {
  dayShift: number;
}
