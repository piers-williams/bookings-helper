import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import BookingDetail from './BookingDetail';
import { bookingsApi } from '../services/apiClient';
import type { BookingActionResult, BookingItem } from '../types';

vi.mock('../services/apiClient', () => ({
  bookingsApi: {
    getById: vi.fn(), getItems: vi.fn(), getAvailableSites: vi.fn(), getAvailableActivities: vi.fn(),
    moveDates: vi.fn(), moveActivity: vi.fn(), changeSite: vi.fn(), addActivity: vi.fn(),
  },
}));
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const api = bookingsApi as any;

const booking = {
  id: 1, osmBookingId: '179743', customerName: 'Test', startDate: '2027-12-04',
  endDate: '2027-12-05', status: 'Provisional', fullDetails: '', comments: [],
};
const initialItems: BookingItem[] = [];

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={['/bookings/1']}>
      <Routes><Route path="/bookings/:id" element={<BookingDetail />} /></Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  api.getById.mockResolvedValue(booking);
  api.getItems.mockResolvedValue(initialItems);
  api.getAvailableSites.mockResolvedValue([]);
  api.getAvailableActivities.mockResolvedValue([
    { id: '4962', name: 'ACTIVITY - Archery' },
    { id: '4961', name: 'ACTIVITY - Air Rifle Shooting' },
  ]);
});

describe('add-activity action', () => {
  it('submits a new activity after confirmation and shows a success banner', async () => {
    const result: BookingActionResult = {
      created: ['999'], deleted: [], status: 'completed', message: 'Added new activity item 999.',
      items: [{
        itemId: '999', type: 'activity', activityId: '4962', label: 'ACTIVITY - Archery',
        startDate: '2027-12-04', startTime: '10:00', endTime: '12:00',
      }],
    };
    api.addActivity.mockResolvedValue(result);
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    const select = await screen.findByLabelText(/^activity$/i);
    await user.selectOptions(select, '4962');
    await user.type(screen.getByLabelText(/start date/i), '2027-12-04');
    await user.type(screen.getByLabelText(/end date/i), '2027-12-04');
    await user.type(screen.getByLabelText(/number of people/i), '8');
    await user.click(screen.getByRole('button', { name: /^add activity$/i }));   // open confirm gate
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.addActivity).toHaveBeenCalledWith(1, {
      activityId: '4962', startDate: '2027-12-04', endDate: '2027-12-04', numberPeople: 8,
    }));
    expect(await screen.findByRole('status')).toHaveTextContent(/added new activity/i);
  });

  it('disables Add activity until an activity, both dates, and number of people are filled in', async () => {
    renderDetail();
    await screen.findByText(/Booking #179743/);

    expect(screen.getByRole('button', { name: /^add activity$/i })).toBeDisabled();
  });

  it('cancelling the confirmation does not call the API', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.selectOptions(await screen.findByLabelText(/^activity$/i), '4962');
    await user.type(screen.getByLabelText(/start date/i), '2027-12-04');
    await user.type(screen.getByLabelText(/end date/i), '2027-12-04');
    await user.type(screen.getByLabelText(/number of people/i), '8');
    await user.click(screen.getByRole('button', { name: /^add activity$/i }));
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(api.addActivity).not.toHaveBeenCalled();
  });

  it('disables Add activity until a positive number of people is entered', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.selectOptions(await screen.findByLabelText(/^activity$/i), '4962');
    await user.type(screen.getByLabelText(/start date/i), '2027-12-04');
    await user.type(screen.getByLabelText(/end date/i), '2027-12-04');
    expect(screen.getByRole('button', { name: /^add activity$/i })).toBeDisabled();

    await user.type(screen.getByLabelText(/number of people/i), '0');
    expect(screen.getByRole('button', { name: /^add activity$/i })).toBeDisabled();
  });
});
