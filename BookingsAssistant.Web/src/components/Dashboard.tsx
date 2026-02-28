import { useState, useEffect, useCallback } from 'react';
import { bookingsApi, emailsApi, syncApi } from '../services/apiClient';
import apiClient from '../services/apiClient';
import type { BookingStats, Email } from '../types';

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
  const [emails, setEmails] = useState<Email[]>([]);
  const [emailTotal, setEmailTotal] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadStats = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [statsRes, authRes, emailsRes] = await Promise.all([
        bookingsApi.getStats(),
        apiClient.get<{ authenticated: boolean }>('/auth/osm/status'),
        emailsApi.getAll(1, 10),
      ]);
      setStats(statsRes);
      setAuthenticated(authRes.data.authenticated);
      setEmails(emailsRes.items);
      setEmailTotal(emailsRes.total);
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

      {/* Recent emails */}
      <div className="mt-8">
        <div className="flex justify-between items-center mb-3">
          <h2 className="text-lg font-semibold text-gray-700">
            Recent Emails
            {emailTotal !== null && (
              <span className="ml-2 text-sm font-normal text-gray-400">
                ({emailTotal} total)
              </span>
            )}
          </h2>
        </div>
        {loading ? (
          <p className="text-sm text-gray-400">Loading…</p>
        ) : emails.length === 0 ? (
          <p className="text-sm text-gray-400">No emails captured yet.</p>
        ) : (
          <div className="bg-white rounded-lg shadow divide-y divide-gray-100">
            {emails.map((email) => (
              <div key={email.id} className="flex items-start gap-3 px-4 py-3">
                <div className={`mt-1 w-2 h-2 rounded-full flex-shrink-0 ${email.isRead ? 'bg-gray-300' : 'bg-blue-500'}`} />
                <div className="min-w-0 flex-1">
                  <div className="flex justify-between items-baseline gap-2">
                    <p className="text-sm font-medium text-gray-800 truncate">
                      {email.senderName ?? 'Unknown sender'}
                    </p>
                    <p className="text-xs text-gray-400 flex-shrink-0">
                      {new Date(email.receivedDate).toLocaleDateString()}
                    </p>
                  </div>
                  <p className="text-sm text-gray-600 truncate">{email.subject}</p>
                  {email.extractedBookingRef && (
                    <p className="text-xs text-indigo-600 mt-0.5">
                      Ref: #{email.extractedBookingRef}
                    </p>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
