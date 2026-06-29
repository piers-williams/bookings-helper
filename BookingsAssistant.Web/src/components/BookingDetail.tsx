import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { bookingsApi } from '../services/apiClient';
import type { BookingDetail as BookingDetailType, BookingItem } from '../types';

export default function BookingDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [booking, setBooking] = useState<BookingDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newComment, setNewComment] = useState('');
  const [posting, setPosting] = useState(false);
  const [postError, setPostError] = useState<string | null>(null);
  const [items, setItems] = useState<BookingItem[] | null>(null);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [itemsUnavailable, setItemsUnavailable] = useState(false);

  useEffect(() => {
    const fetchBooking = async () => {
      if (!id) return;
      setLoading(true);
      setError(null);
      try {
        const bookingData = await bookingsApi.getById(parseInt(id));
        setBooking(bookingData);
      } catch (err) {
        setError('Failed to load booking details');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    fetchBooking();
  }, [id]);

  useEffect(() => {
    const fetchItems = async () => {
      if (!id) return;
      setItemsLoading(true);
      setItemsUnavailable(false);
      try {
        const data = await bookingsApi.getItems(parseInt(id));
        setItems(data);
      } catch (err: unknown) {
        // 501 = OSM parsing seam not yet wired; show graceful message rather than crashing
        const status = (err as { response?: { status?: number } })?.response?.status;
        setItemsUnavailable(true);
        if (status !== 501) console.error('Failed to load items', err);
      } finally {
        setItemsLoading(false);
      }
    };

    fetchItems();
  }, [id]);

  const handlePostComment = async () => {
    if (!newComment.trim() || !id) return;
    setPosting(true);
    setPostError(null);
    try {
      await bookingsApi.postComment(parseInt(id), newComment);
      setNewComment('');
      // Refresh booking to get updated comments
      const bookingData = await bookingsApi.getById(parseInt(id));
      setBooking(bookingData);
    } catch (err) {
      setPostError('Failed to post comment');
      console.error(err);
    } finally {
      setPosting(false);
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="text-center text-gray-600">Loading booking...</div>
      </div>
    );
  }

  if (error || !booking) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="p-4 bg-red-100 border border-red-400 text-red-700 rounded">
          {error || 'Booking not found'}
        </div>
        <button
          onClick={() => navigate('/')}
          className="mt-4 px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
        >
          Back to Dashboard
        </button>
      </div>
    );
  }

  const osmUrl = `https://www.onlinescoutmanager.co.uk/bookings/${booking.osmBookingId}`;

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Back Button */}
      <button
        onClick={() => navigate('/')}
        className="mb-4 px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
      >
        Back to Dashboard
      </button>

      {/* Booking Header */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <div className="flex items-center justify-between mb-4">
          <h1 className="text-2xl font-bold text-gray-800">
            Booking #{booking.osmBookingId}
          </h1>
          <span className={`px-4 py-2 rounded-full text-sm font-medium ${
            booking.status === 'Provisional' ? 'bg-yellow-100 text-yellow-800' :
            booking.status === 'Confirmed' ? 'bg-green-100 text-green-800' :
            booking.status === 'Cancelled' ? 'bg-red-100 text-red-800' :
            'bg-gray-100 text-gray-800'
          }`}>
            {booking.status}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <h2 className="text-lg font-semibold text-gray-700 mb-2">Customer Information</h2>
            <div className="text-gray-600">
              <div><span className="font-semibold">Name:</span> {booking.customerName}</div>
              {booking.customerEmail && (
                <div><span className="font-semibold">Email:</span> {booking.customerEmail}</div>
              )}
            </div>
          </div>

          <div>
            <h2 className="text-lg font-semibold text-gray-700 mb-2">Booking Dates</h2>
            <div className="text-gray-600">
              <div><span className="font-semibold">Start:</span> {new Date(booking.startDate).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })}</div>
              <div><span className="font-semibold">End:</span> {new Date(booking.endDate).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })}</div>
            </div>
          </div>
        </div>

        {booking.fullDetails && (
          <div className="mt-4">
            <h2 className="text-lg font-semibold text-gray-700 mb-2">Details</h2>
            <div className="text-gray-600 whitespace-pre-wrap">
              {booking.fullDetails}
            </div>
          </div>
        )}
      </div>

      {/* Comments Timeline */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">
          Comments Timeline ({booking.comments.length})
        </h2>

        {booking.comments.length > 0 ? (
          <div className="space-y-4">
            {booking.comments
              .sort((a, b) => new Date(a.createdDate).getTime() - new Date(b.createdDate).getTime())
              .map((comment) => (
                <div key={comment.id} className="border-l-4 border-blue-500 pl-4 py-2">
                  <div className="flex items-center justify-between mb-1">
                    <div className="font-semibold text-gray-800">{comment.authorName}</div>
                    <div className="text-sm text-gray-500">
                      {new Date(comment.createdDate).toLocaleString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}
                    </div>
                  </div>
                  <div className="text-gray-700">{comment.textPreview}</div>
                  {comment.isNew && (
                    <span className="inline-block mt-2 px-2 py-1 bg-blue-100 text-blue-800 text-xs rounded">
                      New
                    </span>
                  )}
                </div>
              ))}
          </div>
        ) : (
          <p className="text-gray-500">No comments on this booking.</p>
        )}

        <div className="mt-4 border-t pt-4">
          <textarea
            value={newComment}
            onChange={(e) => setNewComment(e.target.value)}
            placeholder="Add a comment..."
            className="w-full p-3 border border-gray-300 rounded resize-none"
            rows={3}
            disabled={posting}
          />
          {postError && <div className="text-red-600 text-sm mt-1">{postError}</div>}
          <button
            onClick={handlePostComment}
            disabled={posting || !newComment.trim()}
            className="mt-2 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {posting ? 'Posting...' : 'Post Comment'}
          </button>
        </div>
      </div>

      {/* Linked Emails */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">
          Linked Emails ({booking.linkedEmails.length})
        </h2>

        {booking.linkedEmails.length > 0 ? (
          <div className="space-y-3">
            {booking.linkedEmails.map((email) => (
              <div
                key={email.id}
                className="p-4 border border-gray-200 rounded bg-gray-50"
              >
                <div className="font-semibold text-gray-800">{email.subject}</div>
                {email.senderName && (
                  <div className="text-sm text-gray-600">From: {email.senderName}</div>
                )}
                <div className="text-sm text-gray-500">
                  {new Date(email.receivedDate).toLocaleString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-gray-500">No emails linked to this booking.</p>
        )}
      </div>

      {/* Items */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">Items</h2>
        {itemsLoading ? (
          <p className="text-gray-500">Loading items...</p>
        ) : itemsUnavailable ? (
          <p className="text-gray-400 italic">Items not available yet.</p>
        ) : items && items.length > 0 ? (
          <div className="space-y-3">
            {items.map((item) => (
              <div key={item.itemId} className="p-4 border border-gray-200 rounded bg-gray-50">
                <div className="flex items-center gap-2 mb-1">
                  <span className={`px-2 py-0.5 rounded text-xs font-medium ${
                    item.type === 'site' ? 'bg-green-100 text-green-800' : 'bg-purple-100 text-purple-800'
                  }`}>
                    {item.type}
                  </span>
                  <span className="font-semibold text-gray-800">{item.label}</span>
                </div>
                <div className="text-sm text-gray-600 space-y-0.5">
                  {item.siteId && <div>Site: {item.siteId}</div>}
                  {item.activityId && <div>Activity: {item.activityId}</div>}
                  {item.startDate && (
                    <div>
                      Start: {new Date(item.startDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
                      {item.startTime && ` at ${item.startTime}`}
                    </div>
                  )}
                  {item.endDate && (
                    <div>
                      End: {new Date(item.endDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })}
                      {item.endTime && ` at ${item.endTime}`}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-gray-500">No items on this booking.</p>
        )}
      </div>

      {/* External Link */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">External Actions</h2>
        <a
          href={osmUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-block px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Open in OSM →
        </a>
      </div>
    </div>
  );
}
