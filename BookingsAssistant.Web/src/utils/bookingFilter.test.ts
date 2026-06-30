import { describe, it, expect } from 'vitest';
import { filterBookings } from './bookingFilter';
import type { Booking } from '../types';

// ── Helpers ──────────────────────────────────────────────────────────────────

function makeBooking(overrides: Partial<Booking> & { id: number }): Booking {
  return {
    osmBookingId: String(overrides.id * 100),
    customerName: 'Default Name',
    startDate: '2027-06-01',
    endDate: '2027-06-07',
    status: 'Confirmed',
    ...overrides,
  };
}

// includePastCancelled: true so status filtering doesn't interfere with the other test groups
const noFilter = {
  search: '',
  dateFrom: '',
  dateTo: '',
  includePastCancelled: true,
};

// ── Free-text search ──────────────────────────────────────────────────────────

describe('filterBookings — free-text search', () => {
  const bookings = [
    makeBooking({ id: 1, osmBookingId: '12345', customerName: 'Alice Smith' }),
    makeBooking({ id: 2, osmBookingId: '67890', customerName: 'Bob Jones' }),
  ];

  it('matches customerName case-insensitively with a lowercase query', () => {
    const lower = filterBookings(bookings, { ...noFilter, search: 'alice' });
    expect(lower).toHaveLength(1);
    expect(lower[0].customerName).toBe('Alice Smith');
  });

  it('matches customerName case-insensitively with an uppercase query', () => {
    const upper = filterBookings(bookings, { ...noFilter, search: 'ALICE' });
    expect(upper).toHaveLength(1);
    expect(upper[0].customerName).toBe('Alice Smith');
  });

  it('matches a booking whose osmBookingId contains the query', () => {
    const result = filterBookings(bookings, { ...noFilter, search: '678' });
    expect(result).toHaveLength(1);
    expect(result[0].osmBookingId).toBe('67890');
  });

  it('returns an empty array when the query matches nothing', () => {
    const result = filterBookings(bookings, { ...noFilter, search: 'zzznomatch' });
    expect(result).toHaveLength(0);
  });

  it('returns all bookings when search is empty', () => {
    const result = filterBookings(bookings, { ...noFilter, search: '' });
    expect(result).toHaveLength(2);
  });

  it('returns an empty array when given an empty bookings list', () => {
    const result = filterBookings([], noFilter);
    expect(result).toHaveLength(0);
  });
});

// ── Date range filtering ──────────────────────────────────────────────────────

describe('filterBookings — date range', () => {
  // stay: 10–20 June
  const stayJune = makeBooking({ id: 1, startDate: '2027-06-10', endDate: '2027-06-20' });
  // stay: 25 June – 5 July (straddles the 1 July boundary)
  const stayJulyOverlap = makeBooking({ id: 2, startDate: '2027-06-25', endDate: '2027-07-05' });
  // stay: entirely in May
  const stayMay = makeBooking({ id: 3, startDate: '2027-05-01', endDate: '2027-05-10' });
  // stay: 1–30 July
  const stayJuly = makeBooking({ id: 4, startDate: '2027-07-01', endDate: '2027-07-30' });

  it('includes a stay fully inside the date range', () => {
    const result = filterBookings([stayJune], { ...noFilter, dateFrom: '2027-06-01', dateTo: '2027-06-30' });
    expect(result).toHaveLength(1);
  });

  it('includes a stay that straddles the lower bound (starts before dateFrom, ends after it)', () => {
    // booking 25 Jun–5 Jul; range from 1 Jul — the booking's end crosses into the range
    const result = filterBookings([stayJulyOverlap], { ...noFilter, dateFrom: '2027-07-01', dateTo: '2027-07-31' });
    expect(result).toHaveLength(1);
  });

  it('excludes a stay entirely before the date range', () => {
    const result = filterBookings([stayMay], { ...noFilter, dateFrom: '2027-06-01', dateTo: '2027-06-30' });
    expect(result).toHaveLength(0);
  });

  it('excludes a stay entirely after the date range', () => {
    const result = filterBookings([stayJuly], { ...noFilter, dateFrom: '2027-05-01', dateTo: '2027-05-31' });
    expect(result).toHaveLength(0);
  });

  it('includes a stay whose endDate exactly equals dateFrom (boundary-exact overlap)', () => {
    // stayJune ends 20 Jun; dateFrom exactly 20 Jun — boundary should be inclusive
    const result = filterBookings([stayJune], { ...noFilter, dateFrom: '2027-06-20', dateTo: '2027-06-30' });
    expect(result).toHaveLength(1);
  });

  it('includes a stay whose startDate exactly equals dateTo (boundary-exact overlap)', () => {
    // stayJuly starts 1 Jul; dateTo exactly 1 Jul — boundary should be inclusive
    const result = filterBookings([stayJuly], { ...noFilter, dateFrom: '2027-06-01', dateTo: '2027-07-01' });
    expect(result).toHaveLength(1);
  });

  it('with only dateFrom set, includes stays ending on or after dateFrom', () => {
    // stayJune ends 20 Jun; dateFrom 15 Jun => overlaps; stayMay ends 10 May => excluded
    const result = filterBookings([stayJune, stayMay], { ...noFilter, dateFrom: '2027-06-15', dateTo: '' });
    expect(result).toHaveLength(1);
    expect(result[0]).toBe(stayJune);
  });

  it('with only dateTo set, includes stays starting on or before dateTo', () => {
    // stayJuly starts 1 Jul; dateTo 15 Jul => included; stayMay starts 1 May => included
    const result = filterBookings([stayJuly, stayMay], { ...noFilter, dateFrom: '', dateTo: '2027-07-15' });
    expect(result).toHaveLength(2);
  });

  it('with neither dateFrom nor dateTo, applies no date filtering', () => {
    const all = [stayJune, stayJulyOverlap, stayMay, stayJuly];
    const result = filterBookings(all, { ...noFilter, dateFrom: '', dateTo: '' });
    expect(result).toHaveLength(4);
  });
});

// ── includePastCancelled toggle ───────────────────────────────────────────────

describe('filterBookings — includePastCancelled toggle', () => {
  const confirmed = makeBooking({ id: 1, status: 'Confirmed' });
  const future = makeBooking({ id: 2, status: 'Future' });
  const provisional = makeBooking({ id: 3, status: 'Provisional' });
  const past = makeBooking({ id: 4, status: 'Past' });
  const cancelled = makeBooking({ id: 5, status: 'Cancelled' });

  it('hides past and cancelled when includePastCancelled is false', () => {
    const result = filterBookings(
      [confirmed, future, provisional, past, cancelled],
      { ...noFilter, includePastCancelled: false }
    );
    expect(result.map((b) => b.status)).toEqual(
      expect.arrayContaining(['Confirmed', 'Future', 'Provisional'])
    );
    expect(result.map((b) => b.status)).not.toContain('Past');
    expect(result.map((b) => b.status)).not.toContain('Cancelled');
  });

  it('shows past and cancelled when includePastCancelled is true', () => {
    const result = filterBookings(
      [confirmed, future, provisional, past, cancelled],
      { ...noFilter, includePastCancelled: true }
    );
    expect(result).toHaveLength(5);
  });
});

// ── Combined filters ──────────────────────────────────────────────────────────

describe('filterBookings — combined filters', () => {
  it('applies search, date range, and status toggle together', () => {
    const alice = makeBooking({ id: 1, customerName: 'Alice Smith', startDate: '2027-06-10', endDate: '2027-06-20', status: 'Confirmed' });
    const bob = makeBooking({ id: 2, customerName: 'Bob Jones', startDate: '2027-06-10', endDate: '2027-06-20', status: 'Confirmed' });
    const alicePast = makeBooking({ id: 3, customerName: 'Alice Smith', startDate: '2026-01-01', endDate: '2026-01-07', status: 'Past' });

    // search="alice", dateFrom in June 2027, includePastCancelled=false
    // => alice (June, Confirmed) matches; bob excluded by search; alicePast excluded by status
    const result = filterBookings(
      [alice, bob, alicePast],
      { search: 'alice', dateFrom: '2027-06-01', dateTo: '2027-06-30', includePastCancelled: false }
    );
    expect(result).toHaveLength(1);
    expect(result[0]).toBe(alice);
  });
});

// ── Sort order ────────────────────────────────────────────────────────────────

describe('filterBookings — sort order', () => {
  it('returns results sorted by startDate ascending', () => {
    const bookings = [
      makeBooking({ id: 1, startDate: '2027-09-01', endDate: '2027-09-07' }),
      makeBooking({ id: 2, startDate: '2027-06-01', endDate: '2027-06-07' }),
      makeBooking({ id: 3, startDate: '2027-07-15', endDate: '2027-07-20' }),
    ];
    const result = filterBookings(bookings, noFilter);
    expect(result.map((b) => b.startDate)).toEqual([
      '2027-06-01',
      '2027-07-15',
      '2027-09-01',
    ]);
  });
});
