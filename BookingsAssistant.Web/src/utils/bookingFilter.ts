import type { Booking } from '../types';

export interface BookingFilterParams {
  search: string;
  dateFrom: string;
  dateTo: string;
  includePastCancelled: boolean;
}

/** Statuses shown when "include past & cancelled" is OFF. */
const ACTIVE_STATUSES = new Set(['confirmed', 'future', 'provisional']);

/**
 * Filters and sorts bookings by the given search/date/status params.
 *
 * - search: case-insensitive substring match against osmBookingId OR customerName
 * - dateFrom / dateTo: stay-overlap filter (booking's [startDate, endDate] intersects [dateFrom, dateTo])
 * - includePastCancelled: when false, only active statuses (Confirmed, Future, Provisional) are shown
 * - Results are sorted by startDate ascending
 */
export function filterBookings(
  bookings: Booking[],
  { search, dateFrom, dateTo, includePastCancelled }: BookingFilterParams
): Booking[] {
  const query = search.trim().toLowerCase();

  return bookings
    .filter((b) => {
      // Status filter
      if (!includePastCancelled && !ACTIVE_STATUSES.has(b.status.toLowerCase())) {
        return false;
      }

      // Free-text search
      if (query) {
        const matchesId = b.osmBookingId.toLowerCase().includes(query);
        const matchesName = b.customerName.toLowerCase().includes(query);
        if (!matchesId && !matchesName) return false;
      }

      // Date range overlap: booking's [startDate, endDate] intersects [dateFrom, dateTo]
      // Overlap condition: bookingStart <= dateTo AND bookingEnd >= dateFrom
      if (dateFrom || dateTo) {
        const bookingStart = b.startDate.slice(0, 10); // take YYYY-MM-DD from ISO string
        const bookingEnd = b.endDate.slice(0, 10);     // take YYYY-MM-DD from ISO string

        if (dateTo && bookingStart > dateTo) return false;
        if (dateFrom && bookingEnd < dateFrom) return false;
      }

      return true;
    })
    .sort((a, b) => a.startDate.localeCompare(b.startDate));
}
