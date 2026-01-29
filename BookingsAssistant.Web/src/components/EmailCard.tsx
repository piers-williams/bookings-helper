import { Link } from 'react-router-dom';
import type { Email } from '../types';

interface Props {
  email: Email;
}

export default function EmailCard({ email }: Props) {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

    if (diffHours < 1) return 'Just now';
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  };

  return (
    <Link
      to={`/emails/${email.id}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div className="flex-1">
          <p className="text-sm text-gray-600">
            From: {email.senderName || email.senderEmail}
          </p>
          <p className="font-medium text-gray-900 mt-1">{email.subject}</p>
          {email.extractedBookingRef && (
            <p className="text-sm text-blue-600 mt-1">
              🔗 Booking #{email.extractedBookingRef}
            </p>
          )}
        </div>
        <span className="text-sm text-gray-500">
          {formatDate(email.receivedDate)}
        </span>
      </div>
    </Link>
  );
}
