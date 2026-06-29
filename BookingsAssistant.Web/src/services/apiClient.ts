import axios from 'axios';
import type {
  Booking,
  BookingActionResult,
  BookingDetail,
  BookingItem,
  ChangeSiteRequest,
  Email,
  Link,
  CreateLinkRequest,
  BookingStats,
  MoveActivityRequest,
  MoveDatesRequest,
  PagedResult
} from '../types';

const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

const TOKEN_KEY = 'apiToken';

// Attach the shared API token (if the user has set one) to every request.
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) config.headers['X-Api-Token'] = token;
  return config;
});

// On 401 the addon has an api_token configured but ours is missing/wrong.
// Prompt once, store it, and reload. Guarded so concurrent 401s prompt only once.
let promptingForToken = false;
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && !promptingForToken) {
      promptingForToken = true;
      const entered = window.prompt(
        'This Bookings Assistant requires an API token (set in the add-on configuration). Enter it to continue:'
      );
      if (entered) {
        localStorage.setItem(TOKEN_KEY, entered.trim());
        window.location.reload();
      } else {
        promptingForToken = false;
      }
    }
    return Promise.reject(error);
  }
);

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

  postComment: async (id: number, comment: string): Promise<void> => {
    await apiClient.post(`/bookings/${id}/comments`, { comment });
  },

  getItems: async (id: number): Promise<BookingItem[]> => {
    const response = await apiClient.get<BookingItem[]>(`/bookings/${id}/items`);
    return response.data;
  },

  moveActivity: async (id: number, req: MoveActivityRequest): Promise<BookingActionResult> => {
    const response = await apiClient.post<BookingActionResult>(`/bookings/${id}/actions/move-activity`, req);
    return response.data;
  },

  changeSite: async (id: number, req: ChangeSiteRequest): Promise<BookingActionResult> => {
    const response = await apiClient.post<BookingActionResult>(`/bookings/${id}/actions/change-site`, req);
    return response.data;
  },

  moveDates: async (id: number, req: MoveDatesRequest): Promise<BookingActionResult> => {
    const response = await apiClient.post<BookingActionResult>(`/bookings/${id}/actions/move-dates`, req);
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

// Emails API
export const emailsApi = {
  getAll: async (page = 1, pageSize = 20): Promise<PagedResult<Email>> => {
    const response = await apiClient.get<PagedResult<Email>>('/emails', {
      params: { page, pageSize },
    });
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
