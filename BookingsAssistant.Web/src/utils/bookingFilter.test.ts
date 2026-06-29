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

  it('matches a booking whose customerName contains the query (case-insensitive)', () => {
    const result = filterBookings(bookings, { ...noFilter, search: 'alice' });
    expect(result).toHaveLength(1);
    expect(result[0].customerName).toBe('Alice Smith');
  });

  it('matches a booking whose osmBookingId contains the query', () => {
    const result = filterBookings(bookings, { ...noFilter, search: '678' });
    expect(result).toHaveLength(1);
    expect(result[0].osmBookingId).toBe('67890');
  });

  it('matches case-insensitively against customerName', () => {
    const result = filterBookings(bookings, { ...noFilter, search: 'ALICE' });
    expect(result).toHaveLength(1);
    expect(result[0].customerName).toBe('Alice Smith');
  });

  it('returns an empty array when the query matches nothing', () => {
    const result = filterBookings(bookings, { ...noFilter, search: 'zzznomatch' });
    expect(result).toHaveLength(0);
  });

  it('returns all bookings when search is empty', () => {
    const result = filterBookings(bookings, { ...noFilter, search: '' });
    expect(result).toHaveLength(2);
  });
});

// ── Date range filtering ──────────────────────────────────────────────────────

describe('filterBookings — date range', () => {
  // stay: 10–20 June
  const stayJune = makeBooking({ id: 1, startDate: '2027-06-10', endDate: '2027-06-20' });
  // stay: 25 June – 5 July (overlaps into July)
  const stayJulyOverlap = makeBooking({ id: 2, startDate: '2027-06-25', endDate: '2027-07-05' });
  // stay: entirely in May
  const stayMay = makeBooking({ id: 3, startDate: '2027-05-01', endDate: '2027-05-10' });
  // stay: 1–30 July
  const stayJuly = makeBooking({ id: 4, startDate: '2027-07-01', endDate: '2027-07-30' });

  it('includes a stay fully inside the date range', () => {
    const result = filterBookings([stayJune], { ...noFilter, dateFrom: '2027-06-01', dateTo: '2027-06-30' });
    expect(result).toHaveLength(1);
  });

  it('includes a stay that overlaps the start boundary (booking starts before dateFrom but ends within range)', () => {
    // booking 25 Jun–5 Jul, range from 1 Jul; booking ends after dateFrom so it overlaps
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

  it('with only dateFrom set, includes stays ending on or after dateFrom', () => {
    // stayJune ends 20 Jun; dateFrom 15 Jun => overlaps
    const result = filterBookings([stayJune, stayMay], { ...noFilter, dateFrom: '2027-06-15', dateTo: '' });
    expect(result).toHaveLength(1);
    expect(result[0]).toBe(stayJune);
  });

  it('with only dateTo set, includes stays starting on or before dateTo', () => {
    // stayJuly starts 1 Jul; dateTo 15 Jul => included
    // stayMay starts 1 May; dateTo 15 Jul => also included
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

  it('always shows active statuses regardless of toggle', () => {
    const result = filterBookings([confirmed, future, provisional], {
      ...noFilter,
      includePastCancelled: false,
    });
    expect(result).toHaveLength(3);
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
