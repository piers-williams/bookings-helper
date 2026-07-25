import axios from 'axios';
import type {
  AddActivityRequest,
  AvailableSite,
  Booking,
  BookingActionResult,
  BookingDetail,
  BookingItem,
  ChangeSiteRequest,
  BookingStats,
  MoveActivityRequest,
  MoveDatesRequest,
  ProposedPlan,
  RemoveActivityRequest,
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

  getAvailableSites: async (id: number): Promise<AvailableSite[]> => {
    const response = await apiClient.get<AvailableSite[]>(`/bookings/${id}/available-sites`);
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

  getAvailableActivities: async (id: number): Promise<AvailableSite[]> => {
    const response = await apiClient.get<AvailableSite[]>(`/bookings/${id}/available-activities`);
    return response.data;
  },

  addActivity: async (id: number, req: AddActivityRequest): Promise<BookingActionResult> => {
    const response = await apiClient.post<BookingActionResult>(`/bookings/${id}/actions/add-activity`, req);
    return response.data;
  },

  removeActivity: async (id: number, req: RemoveActivityRequest): Promise<BookingActionResult> => {
    const response = await apiClient.post<BookingActionResult>(`/bookings/${id}/actions/remove-activity`, req);
    return response.data;
  },
};

// Plans API — LLM-drafted action plans awaiting human approval
export const plansApi = {
  create: async (sourceEmailText: string, osmBookingId?: string): Promise<ProposedPlan> => {
    const response = await apiClient.post<ProposedPlan>('/plans', { sourceEmailText, osmBookingId });
    return response.data;
  },

  list: async (status?: string): Promise<ProposedPlan[]> => {
    const params = status ? { status } : {};
    const response = await apiClient.get<ProposedPlan[]>('/plans', { params });
    return response.data;
  },

  getById: async (id: number): Promise<ProposedPlan> => {
    const response = await apiClient.get<ProposedPlan>(`/plans/${id}`);
    return response.data;
  },

  approve: async (id: number): Promise<ProposedPlan> => {
    const response = await apiClient.post<ProposedPlan>(`/plans/${id}/approve`);
    return response.data;
  },

  reject: async (id: number): Promise<ProposedPlan> => {
    const response = await apiClient.post<ProposedPlan>(`/plans/${id}/reject`);
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
