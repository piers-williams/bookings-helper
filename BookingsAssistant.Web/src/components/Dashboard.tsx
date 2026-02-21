import { useState, useEffect } from 'react';
import { emailsApi, bookingsApi, commentsApi, syncApi } from '../services/apiClient';
import type { Email, Booking, Comment } from '../types';
import EmailCard from './EmailCard';
import BookingCard from './BookingCard';
import CommentCard from './CommentCard';

export default function Dashboard() {
  const [emails, setEmails] = useState<Email[]>([]);
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [comments, setComments] = useState<Comment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [emailsRes, bookingsRes, commentsRes] = await Promise.all([
        emailsApi.getUnread(),
        bookingsApi.getProvisional(),
        commentsApi.getNew(),
      ]);
      setEmails(emailsRes);
      setBookings(bookingsRes);
      setComments(commentsRes);
    } catch (err) {
      setError('Failed to load data');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    setLoading(true);
    try {
      await syncApi.sync();
      await fetchData();
    } catch (err) {
      setError('Failed to sync data');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-800">Bookings Assistant</h1>
        <button
          onClick={handleRefresh}
          disabled={loading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400"
        >
          {loading ? 'Loading...' : 'Refresh'}
        </button>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-100 border border-red-400 text-red-700 rounded">
          {error}
        </div>
      )}

      <div className="space-y-6">
        {/* Unread Emails Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">📧</span>
            Unread Emails ({emails.length})
          </h2>
          <div className="space-y-3">
            {emails.map(email => (
              <EmailCard key={email.id} email={email} />
            ))}
            {emails.length === 0 && (
              <p className="text-gray-500">No unread emails</p>
            )}
          </div>
        </section>

        {/* Provisional Bookings Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">📋</span>
            Provisional Bookings ({bookings.length})
          </h2>
          <div className="space-y-3">
            {bookings.map(booking => (
              <BookingCard key={booking.id} booking={booking} />
            ))}
            {bookings.length === 0 && (
              <p className="text-gray-500">No provisional bookings</p>
            )}
          </div>
        </section>

        {/* New Comments Section */}
        <section className="bg-white rounded-lg shadow p-6">
          <h2 className="text-xl font-semibold mb-4 flex items-center">
            <span className="mr-2">💬</span>
            New Comments ({comments.length})
          </h2>
          <div className="space-y-3">
            {comments.map(comment => (
              <CommentCard key={comment.id} comment={comment} />
            ))}
            {comments.length === 0 && (
              <p className="text-gray-500">No new comments</p>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
