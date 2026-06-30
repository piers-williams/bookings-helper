import { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { bookingsApi } from '../services/apiClient';
import type { Booking } from '../types';
import BookingCard from './BookingCard';
import { filterBookings } from '../utils/bookingFilter';

export default function BookingList() {
  const [allBookings, setAllBookings] = useState<Booking[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [includePastCancelled, setIncludePastCancelled] = useState(false);

  useEffect(() => {
    // Loads all bookings (all statuses) into the browser; filtering is done client-side.
    // Revisit with a server-side status filter / pagination if the dataset grows large.
    bookingsApi
      .getAll()
      .then(setAllBookings)
      .catch(() => setError('Failed to load bookings'))
      .finally(() => setLoading(false));
  }, []);

  const filteredBookings = useMemo(
    () => filterBookings(allBookings, { search, dateFrom, dateTo, includePastCancelled }),
    [allBookings, search, dateFrom, dateTo, includePastCancelled]
  );

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <div className="flex items-center gap-4 mb-6">
        <Link to="/" className="text-sm text-blue-600 hover:underline">
          ← Dashboard
        </Link>
        <h1 className="text-2xl font-bold text-gray-800">All Bookings</h1>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg shadow p-4 mb-6 space-y-4">
        <div>
          <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
            Search
          </label>
          <input
            id="search"
            type="text"
            placeholder="Name or booking ID…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div className="flex flex-wrap gap-4">
          <div className="flex-1 min-w-36">
            <label htmlFor="dateFrom" className="block text-sm font-medium text-gray-700 mb-1">
              From
            </label>
            <input
              id="dateFrom"
              type="date"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div className="flex-1 min-w-36">
            <label htmlFor="dateTo" className="block text-sm font-medium text-gray-700 mb-1">
              To
            </label>
            <input
              id="dateTo"
              type="date"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
          <input
            type="checkbox"
            checked={includePastCancelled}
            onChange={(e) => setIncludePastCancelled(e.target.checked)}
            className="rounded border-gray-300"
          />
          Include past &amp; cancelled
        </label>
      </div>

      {/* Results */}
      {loading && <p className="text-sm text-gray-400">Loading…</p>}

      {error && (
        <div className="p-4 bg-red-100 border border-red-400 text-red-700 rounded text-sm">
          {error}
        </div>
      )}

      {!loading && !error && filteredBookings.length === 0 && (
        <p className="text-sm text-gray-400">No bookings match your search.</p>
      )}

      {!loading && !error && filteredBookings.length > 0 && (
        <div className="space-y-2">
          {filteredBookings.map((booking) => (
            <BookingCard key={booking.id} booking={booking} />
          ))}
        </div>
      )}
    </div>
  );
}
