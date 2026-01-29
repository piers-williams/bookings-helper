import { Link } from 'react-router-dom';
import type { Booking } from '../types';

interface Props {
  booking: Booking;
}

export default function BookingCard({ booking }: Props) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    });
  };

  return (
    <Link
      to={`/bookings/${booking.id}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div>
          <p className="font-medium text-gray-900">
            #{booking.osmBookingId} - {booking.customerName}
          </p>
          <p className="text-sm text-gray-600 mt-1">
            {formatDate(booking.startDate)} - {formatDate(booking.endDate)}
          </p>
        </div>
        <span className={`px-2 py-1 text-xs rounded ${
          booking.status === 'Provisional'
            ? 'bg-yellow-100 text-yellow-800'
            : 'bg-green-100 text-green-800'
        }`}>
          {booking.status}
        </span>
      </div>
    </Link>
  );
}
