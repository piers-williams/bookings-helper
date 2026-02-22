import axios from 'axios';
import type {
  EmailDetail,
  Booking,
  BookingDetail,
  Link,
  CreateLinkRequest,
  BookingStats
} from '../types';

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Emails API (capture/detail only — no unread list endpoint)
export const emailsApi = {
  getById: async (id: number): Promise<EmailDetail> => {
    const response = await apiClient.get<EmailDetail>(`/emails/${id}`);
    return response.data;
  },
};

// Bookings API
export const bookingsApi = {
  getAll: async (status?: string): Promise<Booking[]> => {
    const params = status ? { status } : {};
    const response = await apiClient.get<Booking[]>('/bookings', { params });
    return response.data;
  },

  getStats: async (): Promise<BookingStats> => {
    const response = await apiClient.get<BookingStats>('/bookings/stats');
    return response.data;
  },

  getById: async (id: number): Promise<BookingDetail> => {
    const response = await apiClient.get<BookingDetail>(`/bookings/${id}`);
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

// Sync API — endpoint is POST /api/bookings/sync
export const syncApi = {
  sync: async (): Promise<{ added: number; updated: number; total: number }> => {
    const response = await apiClient.post<{ added: number; updated: number; total: number }>('/bookings/sync');
    return response.data;
  },
};

export default apiClient;
