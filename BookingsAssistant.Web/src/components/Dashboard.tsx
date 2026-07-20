import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { bookingsApi, syncApi } from '../services/apiClient';
import apiClient from '../services/apiClient';
import type { Booking, BookingStats, GateCodeStatus } from '../types';

interface StatCardProps {
  label: string;
  value: number | null;
  colorClass: string;
}

function StatCard({ label, value, colorClass }: StatCardProps) {
  return (
    <div className="bg-white rounded-lg shadow p-6">
      <p className="text-sm text-gray-500 uppercase tracking-wide">{label}</p>
      <p className={`text-4xl font-bold mt-2 ${colorClass}`}>
        {value === null ? '–' : value}
      </p>
    </div>
  );
}

// Maps each gate-code status to a badge label and colour. Red == needs
// attention (a code is due but something is blocking it).
function gateCodeBadge(status: GateCodeStatus | undefined): { label: string; className: string } {
  switch (status) {
    case 'sent':
      return { label: 'Gate code sent', className: 'bg-emerald-100 text-emerald-800' };
    case 'not_required':
      return { label: 'Wardens on site', className: 'bg-gray-100 text-gray-600' };
    case 'scheduled':
      return { label: 'Gate code scheduled', className: 'bg-blue-100 text-blue-800' };
    case 'awaiting_confirmation':
      return { label: 'Awaiting confirmation', className: 'bg-yellow-100 text-yellow-800' };
    case 'arrival_passed':
      return { label: 'Not sent — arrival passed', className: 'bg-red-100 text-red-800' };
    case 'pending':
    default:
      return { label: 'Gate code pending', className: 'bg-orange-100 text-orange-800' };
  }
}

function formatLastSynced(iso: string | null): string {
  if (!iso) return 'Never';
  const d = new Date(iso);
  const diffMs = Date.now() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  return d.toLocaleDateString();
}

export default function Dashboard() {
  const [stats, setStats] = useState<BookingStats | null>(null);
  const [authenticated, setAuthenticated] = useState<boolean | null>(null);
  const [upcomingBookings, setUpcomingBookings] = useState<Booking[]>([]);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsRes, authRes, confirmedRes, futureRes] = await Promise.all([
        bookingsApi.getStats(),
        apiClient.get<{ authenticated: boolean }>('/auth/osm/status'),
        bookingsApi.getAll('confirmed'),
        bookingsApi.getAll('future'),
      ]);
      setStats(statsRes);
      setAuthenticated(authRes.data.authenticated);

      const now = new Date();
      const sevenDays = new Date(now);
      sevenDays.setDate(sevenDays.getDate() + 7);
      const upcoming = [...confirmedRes, ...futureRes]
        .filter(b => {
          const start = new Date(b.startDate);
          return start >= now && start <= sevenDays;
        })
        .sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());
      setUpcomingBookings(upcoming);
    } catch {
      setError('Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  const handleSync = async () => {
    setSyncing(true);
    setError(null);
    try {
      await syncApi.sync();
      await loadStats();
    } catch {
      setError('Sync failed — check OSM authentication');
    } finally {
      setSyncing(false);
    }
  };

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      {/* Header */}
      <div className="flex justify-between items-start mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-800">Bookings Assistant</h1>
          <div className="mt-1 text-sm text-gray-500">
            Last synced: {stats ? formatLastSynced(stats.lastSynced) : '—'}
          </div>
        </div>
        <div className="flex flex-col items-end gap-2">
          {authenticated !== null && (
            <span className="flex items-center gap-1.5 text-sm">
              <span className={`w-2.5 h-2.5 rounded-full ${authenticated ? 'bg-green-500' : 'bg-amber-500'}`} />
              {authenticated ? (
                <span className="text-green-700">OSM connected</span>
              ) : (
                <span className="text-amber-700">
                  Not connected —{' '}
                  <a href="/api/auth/osm/login" className="underline hover:text-amber-900">
                    authenticate
                  </a>
                </span>
              )}
            </span>
          )}
          <button
            onClick={handleSync}
            disabled={syncing || loading}
            className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400 text-sm"
          >
            {syncing ? 'Syncing…' : 'Sync from OSM'}
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-red-100 border border-red-400 text-red-700 rounded text-sm">
          {error}
        </div>
      )}

      {/* Stat cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="On site now"
          value={loading ? null : (stats?.onSiteNow ?? null)}
          colorClass="text-green-600"
        />
        <StatCard
          label="Arriving this week"
          value={loading ? null : (stats?.arrivingThisWeek ?? null)}
          colorClass="text-blue-600"
        />
        <StatCard
          label="Next 30 days"
          value={loading ? null : (stats?.arrivingNext30Days ?? null)}
          colorClass="text-indigo-600"
        />
        <StatCard
          label="Provisional"
          value={loading ? null : (stats?.provisional ?? null)}
          colorClass="text-amber-600"
        />
      </div>

      {/* Browse link — always visible once loading is done */}
      {!loading && (
        <div className="mt-6 text-right">
          <Link to="/bookings" className="text-sm text-blue-600 hover:underline">
            Browse all bookings →
          </Link>
        </div>
      )}

      {/* Upcoming arrivals */}
      {!loading && upcomingBookings.length > 0 && (
        <div className="mt-8">
          <h2 className="text-lg font-semibold text-gray-700 mb-3">
            Upcoming Arrivals
            <span className="ml-2 text-sm font-normal text-gray-400">
              (next 7 days)
            </span>
          </h2>
          <div className="bg-white rounded-lg shadow divide-y divide-gray-100">
            {upcomingBookings.map((booking) => {
              const start = new Date(booking.startDate);
              const daysUntil = Math.ceil((start.getTime() - Date.now()) / 86400000);
              const dateStr = start.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
              return (
                <a
                  key={booking.id}
                  href={`/bookings/${booking.id}`}
                  className="flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
                >
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-gray-800 truncate">{booking.customerName}</p>
                    <p className="text-xs text-gray-500">
                      {dateStr}
                      {daysUntil <= 0
                        ? ' — today'
                        : daysUntil === 1
                        ? ' — tomorrow'
                        : ` — in ${daysUntil} days`}
                    </p>
                  </div>
                  <div className="flex items-center ml-4 flex-shrink-0">
                    <span className={`px-2 py-1 text-xs rounded ${
                      booking.status === 'Provisional'
                        ? 'bg-yellow-100 text-yellow-800'
                        : 'bg-green-100 text-green-800'
                    }`}>
                      {booking.status}
                    </span>
                    {(() => {
                      const badge = gateCodeBadge(booking.gateCodeStatus);
                      return (
                        <span className={`ml-2 px-2 py-1 text-xs rounded ${badge.className}`}>
                          {badge.label}
                        </span>
                      );
                    })()}
                  </div>
                </a>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
