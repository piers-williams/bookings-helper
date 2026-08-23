import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import BookingDetail from './BookingDetail';
import { bookingsApi } from '../services/apiClient';
import type { BookingActionResult, BookingItem } from '../types';

vi.mock('../services/apiClient', () => ({
  bookingsApi: {
    getById: vi.fn(), getItems: vi.fn(), getAvailableSites: vi.fn(), getAvailableActivities: vi.fn(),
    moveDates: vi.fn(), moveActivity: vi.fn(), changeSite: vi.fn(), addActivity: vi.fn(), removeActivity: vi.fn(),
    changeNumbers: vi.fn(),
  },
}));
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const api = bookingsApi as any;

const booking = {
  id: 1, osmBookingId: '179743', customerName: 'Test', startDate: '2027-12-04',
  endDate: '2027-12-05', status: 'Provisional', fullDetails: '', comments: [],
};
const activity: BookingItem = { itemId: '411468', type: 'activity', activityId: '4961', label: 'Air Rifle', startDate: '2027-12-05', startTime: '09:00', endTime: '10:00' };
const site: BookingItem = { itemId: '411467', type: 'site', siteId: '1387', label: 'Hayvern', startDate: '2027-12-04', endDate: '2027-12-05' };

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
  api.getItems.mockResolvedValue([activity, site]);
  api.getAvailableSites.mockResolvedValue([]);
  api.getAvailableActivities.mockResolvedValue([]);
});

describe('change-numbers action', () => {
  it('submits a new headcount for an item after confirmation and shows a success banner', async () => {
    const result: BookingActionResult = {
      created: ['999'], deleted: ['411468'], status: 'completed',
      message: "Replaced 1 item(s) successfully.", items: [site],
    };
    api.changeNumbers.mockResolvedValue(result);
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    const activityRow = screen.getByText('Air Rifle').closest('div')!.parentElement!;
    await user.click(within(activityRow).getByRole('button', { name: /change numbers/i }));
    await user.type(screen.getByLabelText(/new number of people/i), '10');
    await user.click(screen.getByRole('button', { name: /^update$/i }));   // open confirm gate
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.changeNumbers).toHaveBeenCalledWith(1, {
      itemId: '411468', newNumberPeople: 10,
    }));
    expect(await screen.findByRole('status')).toHaveTextContent(/replaced 1 item/i);
  });

  it('disables the submit button until a positive number is entered', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    const activityRow = screen.getByText('Air Rifle').closest('div')!.parentElement!;
    await user.click(within(activityRow).getByRole('button', { name: /change numbers/i }));
    expect(screen.getByRole('button', { name: /^update$/i })).toBeDisabled();

    await user.type(screen.getByLabelText(/new number of people/i), '0');
    expect(screen.getByRole('button', { name: /^update$/i })).toBeDisabled();
  });

  it('includes an optional note in the request', async () => {
    api.changeNumbers.mockResolvedValue({
      created: ['999'], deleted: ['411467'], status: 'completed', message: 'Replaced 1 item(s) successfully.', items: [],
    } as BookingActionResult);
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    const siteRow = screen.getByText('Hayvern').closest('div')!.parentElement!;
    await user.click(within(siteRow).getByRole('button', { name: /change numbers/i }));
    await user.type(screen.getByLabelText(/new number of people/i), '15');
    await user.click(screen.getByRole('button', { name: /^update$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'group grew');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.changeNumbers).toHaveBeenCalledWith(1, {
      itemId: '411467', newNumberPeople: 15, note: 'group grew',
    }));
  });

  it('cancelling the confirmation does not call the API', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    const activityRow = screen.getByText('Air Rifle').closest('div')!.parentElement!;
    await user.click(within(activityRow).getByRole('button', { name: /change numbers/i }));
    await user.type(screen.getByLabelText(/new number of people/i), '10');
    await user.click(screen.getByRole('button', { name: /^update$/i }));
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(api.changeNumbers).not.toHaveBeenCalled();
  });
});
