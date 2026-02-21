import { Link } from 'react-router-dom';
import type { Comment } from '../types';

interface Props {
  comment: Comment;
}

export default function CommentCard({ comment }: Props) {
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
      to={`/bookings/${comment.booking?.id || 0}`}
      className="block p-4 border border-gray-200 rounded hover:bg-gray-50 transition"
    >
      <div className="flex justify-between items-start">
        <div className="flex-1">
          <p className="text-sm text-gray-600">
            Booking #{comment.osmBookingId} - {comment.authorName}
          </p>
          <p className="text-gray-900 mt-1">"{comment.textPreview}..."</p>
        </div>
        <span className="text-sm text-gray-500">
          {formatDate(comment.createdDate)}
        </span>
      </div>
    </Link>
  );
}
