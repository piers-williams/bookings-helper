import axios from 'axios';
import type {
  Email,
  EmailDetail,
  Booking,
  BookingDetail,
  Comment,
  Link,
  CreateLinkRequest
} from '../types';

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Emails API
export const emailsApi = {
  getUnread: async (): Promise<Email[]> => {
    const response = await apiClient.get<Email[]>('/emails/unread');
    return response.data;
  },

  getById: async (id: number): Promise<EmailDetail> => {
    const response = await apiClient.get<EmailDetail>(`/emails/${id}`);
    return response.data;
  },
};

// Bookings API
export const bookingsApi = {
  getProvisional: async (status?: string): Promise<Booking[]> => {
    const params = status ? { status } : {};
    const response = await apiClient.get<Booking[]>('/bookings/provisional', { params });
    return response.data;
  },

  getById: async (id: number): Promise<BookingDetail> => {
    const response = await apiClient.get<BookingDetail>(`/bookings/${id}`);
    return response.data;
  },
};

// Comments API
export const commentsApi = {
  getNew: async (): Promise<Comment[]> => {
    const response = await apiClient.get<Comment[]>('/comments/new');
    return response.data;
  },
};

// Links API
export const linksApi = {
  create: async (request: CreateLinkRequest): Promise<Link> => {
    const response = await apiClient.post<Link>('/links', request);
    return response.data;
  },

  getByEmail: async (emailId: number): Promise<Link[]> => {
    const response = await apiClient.get<Link[]>(`/links/email/${emailId}`);
    return response.data;
  },

  getByBooking: async (bookingId: number): Promise<Link[]> => {
    const response = await apiClient.get<Link[]>(`/links/booking/${bookingId}`);
    return response.data;
  },
};

// Sync API
export const syncApi = {
  sync: async (): Promise<{ success: boolean; message: string }> => {
    const response = await apiClient.post<{ success: boolean; message: string }>('/sync');
    return response.data;
  },
};

export default apiClient;
