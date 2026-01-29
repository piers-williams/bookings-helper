import { useState } from 'react';
import { linksApi } from '../services/apiClient';
import type { Booking } from '../types';

interface LinkBookingModalProps {
  emailId: number;
  bookings: Booking[];
  onClose: () => void;
  onLinked: () => void;
}

export default function LinkBookingModal({ emailId, bookings, onClose, onLinked }: LinkBookingModalProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [linking, setLinking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const filteredBookings = bookings.filter((booking) => {
    const search = searchTerm.toLowerCase();
    return (
      booking.osmBookingId.toLowerCase().includes(search) ||
      booking.customerName.toLowerCase().includes(search) ||
      (booking.customerEmail && booking.customerEmail.toLowerCase().includes(search))
    );
  });

  const handleLink = async (bookingId: number) => {
    setLinking(true);
    setError(null);
    try {
      await linksApi.create({
        emailMessageId: emailId,
        osmBookingId: bookingId,
      });
      onLinked();
      onClose();
    } catch (err) {
      setError('Failed to create link');
      console.error(err);
    } finally {
      setLinking(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full mx-4 max-h-[80vh] flex flex-col">
        {/* Modal Header */}
        <div className="p-6 border-b border-gray-200">
          <h2 className="text-2xl font-bold text-gray-800">Link to Booking</h2>
          <p className="text-gray-600 mt-1">Search for a booking to link to this email</p>
        </div>

        {/* Search Box */}
        <div className="p-6 border-b border-gray-200">
          <input
            type="text"
            placeholder="Search by booking number, customer name, or email..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            autoFocus
          />
        </div>

        {/* Error Message */}
        {error && (
          <div className="mx-6 mt-4 p-3 bg-red-100 border border-red-400 text-red-700 rounded">
            {error}
          </div>
        )}

        {/* Results List */}
        <div className="flex-1 overflow-y-auto p-6">
          {filteredBookings.length > 0 ? (
            <div className="space-y-3">
              {filteredBookings.map((booking) => (
                <div
                  key={booking.id}
                  className="p-4 border border-gray-200 rounded hover:bg-gray-50"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex-1">
                      <div className="font-semibold text-gray-800">
                        Booking #{booking.osmBookingId}
                      </div>
                      <div className="text-gray-600">{booking.customerName}</div>
                      {booking.customerEmail && (
                        <div className="text-sm text-gray-500">{booking.customerEmail}</div>
                      )}
                      <div className="text-sm text-gray-500 mt-1">
                        {new Date(booking.startDate).toLocaleDateString()} - {new Date(booking.endDate).toLocaleDateString()}
                      </div>
                      <span className={`inline-block mt-2 px-3 py-1 rounded-full text-xs font-medium ${
                        booking.status === 'Provisional' ? 'bg-yellow-100 text-yellow-800' :
                        booking.status === 'Confirmed' ? 'bg-green-100 text-green-800' :
                        'bg-gray-100 text-gray-800'
                      }`}>
                        {booking.status}
                      </span>
                    </div>
                    <button
                      onClick={() => handleLink(booking.id)}
                      disabled={linking}
                      className="ml-4 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-gray-400"
                    >
                      {linking ? 'Linking...' : 'Link'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-center text-gray-500 py-8">
              {searchTerm ? 'No bookings found matching your search' : 'Enter a search term to find bookings'}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        <div className="p-6 border-t border-gray-200">
          <button
            onClick={onClose}
            className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
