// TypeScript types matching the backend DTOs

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
  startDate: string;
  endDate: string;
  status: string;
  gateCodeSentAt?: string | null;
  gateCodeStatus?: GateCodeStatus;
}

export interface BookingDetail {
  id: number;
  osmBookingId: string;
  customerName: string;
  startDate: string;
  endDate: string;
  status: string;
  fullDetails: string;
  comments: Comment[];
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
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to move a site item to a different site. */
export interface ChangeSiteRequest {
  itemId: string;
  newSiteId: string;
  /** Display name of the target site, shown in the available-sites dropdown. */
  newSiteName?: string;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to shift all items in a booking by the given number of days. */
export interface MoveDatesRequest {
  dayShift: number;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to add a brand-new activity item to a booking (no existing item to clone). */
export interface AddActivityRequest {
  activityId: string;
  startDate: string;
  endDate: string;
  startTime?: string;
  endTime?: string;
  numberPeople: number;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to remove (hard-delete) an existing item — activity or site — from a booking. */
export interface RemoveActivityRequest {
  itemId: string;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to change the headcount (number of people) on an existing item. */
export interface ChangeNumbersRequest {
  itemId: string;
  newNumberPeople: number;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** A bookable site/pitch a booked item can be moved to (for change-site). */
export interface AvailableSite {
  id: string;
  name: string;
}

/** Mirrors ProposedPlanDto on the backend. */
export type PlanStatusValue = 'AwaitingApproval' | 'Executed' | 'Rejected' | 'Failed';

export interface ProposedPlan {
  id: number;
  status: PlanStatusValue;
  sourceEmailText?: string | null;
  osmBookingId?: string | null;
  /** JSON-encoded array of PlanAction, in execution order. */
  actionsJson?: string | null;
  /**
   * Set when drafting succeeded but an automatic availability pre-check found a date-carrying
   * action (currently addActivity) whose slot was still unavailable after drafting's one retry.
   * Drafting isn't failed for this — the plan is saved as normal — but a human reviewing it
   * here should see the conflict before approving.
   */
  draftWarning?: string | null;
  /** JSON-encoded array of PlanActionExecutionResult, parallel to the actions array. */
  executionResultJson?: string | null;
  createdAt: string;
}

/**
 * One entry from a ProposedPlan's ActionsJson. Fields are flat alongside "type" (mirrors the
 * shape the LLM is prompted to produce — see PlanDraftingService on the backend). Only the
 * fields relevant to `type` are populated; the rest are undefined.
 */
export interface PlanAction {
  type: string;
  /** draftEmailReply / postComment */
  text?: string;
  /** moveDates */
  dayShift?: number;
  /** changeSite / moveActivity */
  itemId?: string;
  /** changeSite */
  newSiteId?: string;
  newSiteName?: string;
  /** addActivity / checkAvailability */
  activityId?: string;
  /** moveActivity / addActivity / checkAvailability */
  newStartDate?: string;
  newStartTime?: string;
  newEndTime?: string;
  /** addActivity / checkAvailability */
  newEndDate?: string;
  numberPeople?: number;
  /** changeNumbers */
  newNumberPeople?: number;
  /** moveDates / changeSite / moveActivity / addActivity / removeActivity / changeNumbers / checkAvailability */
  note?: string;
}

/** Mirrors PlanActionExecutionResult on the backend. One per action, in order. */
export interface PlanActionExecutionResult {
  type: string;
  /** One of: "succeeded", "failed", "not_attempted". */
  status: 'succeeded' | 'failed' | 'not_attempted';
  reason?: string | null;
  /**
   * Present for actions that produce a result beyond success/failure (currently only
   * "checkAvailability" — e.g. "Available" or "Not available: ..."). Unlike `reason`, this is
   * populated on a "succeeded" outcome too.
   */
  detail?: string | null;
}
