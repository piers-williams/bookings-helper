import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { emailsApi, bookingsApi } from '../services/apiClient';
import type { EmailDetail as EmailDetailType, Booking } from '../types';
import LinkBookingModal from './LinkBookingModal';

export default function EmailDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [email, setEmail] = useState<EmailDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showLinkModal, setShowLinkModal] = useState(false);
  const [allBookings, setAllBookings] = useState<Booking[]>([]);

  useEffect(() => {
    const fetchEmail = async () => {
      if (!id) return;
      setLoading(true);
      setError(null);
      try {
        const emailData = await emailsApi.getById(parseInt(id));
        setEmail(emailData);

        // Fetch all bookings for the modal
        const bookingsData = await bookingsApi.getProvisional();
        setAllBookings(bookingsData);
      } catch (err) {
        setError('Failed to load email details');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    fetchEmail();
  }, [id]);

  const handleLinkCreated = async () => {
    // Refresh email data to show the new link
    if (!id) return;
    try {
      const emailData = await emailsApi.getById(parseInt(id));
      setEmail(emailData);
    } catch (err) {
      console.error('Failed to refresh email:', err);
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="text-center text-gray-600">Loading email...</div>
      </div>
    );
  }

  if (error || !email) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="p-4 bg-red-100 border border-red-400 text-red-700 rounded">
          {error || 'Email not found'}
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

  const outlookUrl = `https://outlook.office.com/mail/inbox/id/${email.messageId}`;

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Back Button */}
      <button
        onClick={() => navigate('/')}
        className="mb-4 px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
      >
        ← Back to Dashboard
      </button>

      {/* Email Header */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h1 className="text-2xl font-bold text-gray-800 mb-4">{email.subject}</h1>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div>
            <span className="font-semibold text-gray-700">From:</span>{' '}
            <span className="text-gray-600">
              {email.senderName ? `${email.senderName} <${email.senderEmail}>` : email.senderEmail}
            </span>
          </div>
          <div>
            <span className="font-semibold text-gray-700">Date:</span>{' '}
            <span className="text-gray-600">
              {new Date(email.receivedDate).toLocaleString()}
            </span>
          </div>
        </div>

        {email.extractedBookingRef && (
          <div className="mt-4 p-3 bg-blue-50 border border-blue-200 rounded">
            <span className="font-semibold text-blue-800">Detected Booking Reference:</span>{' '}
            <span className="text-blue-700">{email.extractedBookingRef}</span>
          </div>
        )}
      </div>

      {/* Email Body */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">Message</h2>
        <div className="prose max-w-none text-gray-700 whitespace-pre-wrap">
          {email.body}
        </div>
      </div>

      {/* Smart Links Section */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">Linked Bookings</h2>

        {email.linkedBookings.length > 0 ? (
          <div className="space-y-3">
            {email.linkedBookings.map((booking) => (
              <div
                key={booking.id}
                className="p-4 border border-gray-200 rounded hover:bg-gray-50 cursor-pointer"
                onClick={() => navigate(`/bookings/${booking.id}`)}
              >
                <div className="flex items-center justify-between">
                  <div>
                    <div className="font-semibold text-gray-800">
                      Booking #{booking.osmBookingId}
                    </div>
                    <div className="text-gray-600">{booking.customerName}</div>
                    <div className="text-sm text-gray-500">
                      {new Date(booking.startDate).toLocaleDateString()} - {new Date(booking.endDate).toLocaleDateString()}
                    </div>
                  </div>
                  <div>
                    <span className={`px-3 py-1 rounded-full text-sm font-medium ${
                      booking.status === 'Provisional' ? 'bg-yellow-100 text-yellow-800' :
                      booking.status === 'Confirmed' ? 'bg-green-100 text-green-800' :
                      'bg-gray-100 text-gray-800'
                    }`}>
                      {booking.status}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div>
            <p className="text-gray-500 mb-4">No bookings linked to this email.</p>
            <button
              onClick={() => setShowLinkModal(true)}
              className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
            >
              Search & Link Manually
            </button>
          </div>
        )}

        {email.linkedBookings.length > 0 && (
          <button
            onClick={() => setShowLinkModal(true)}
            className="mt-4 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
          >
            Link Another Booking
          </button>
        )}
      </div>

      {/* Related Emails */}
      {email.relatedEmails.length > 0 && (
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <h2 className="text-xl font-semibold text-gray-800 mb-4">
            Related Emails from {email.senderName || email.senderEmail}
          </h2>
          <div className="space-y-3">
            {email.relatedEmails.map((relatedEmail) => (
              <div
                key={relatedEmail.id}
                className="p-4 border border-gray-200 rounded hover:bg-gray-50 cursor-pointer"
                onClick={() => navigate(`/emails/${relatedEmail.id}`)}
              >
                <div className="font-semibold text-gray-800">{relatedEmail.subject}</div>
                <div className="text-sm text-gray-500">
                  {new Date(relatedEmail.receivedDate).toLocaleString()}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* External Link */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-semibold text-gray-800 mb-4">External Actions</h2>
        <a
          href={outlookUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-block px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          Open in Outlook Web →
        </a>
      </div>

      {/* Link Booking Modal */}
      {showLinkModal && (
        <LinkBookingModal
          emailId={parseInt(id!)}
          bookings={allBookings}
          onClose={() => setShowLinkModal(false)}
          onLinked={handleLinkCreated}
        />
      )}
    </div>
  );
}
