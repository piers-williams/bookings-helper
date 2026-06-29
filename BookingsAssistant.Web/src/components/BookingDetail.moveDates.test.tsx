import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import BookingDetail from './BookingDetail';
import { bookingsApi } from '../services/apiClient';
import type { BookingActionResult, BookingItem } from '../types';

vi.mock('../services/apiClient', () => ({
  bookingsApi: {
    getById: vi.fn(),
    getItems: vi.fn(),
    getAvailableSites: vi.fn(),
    moveDates: vi.fn(),
    moveActivity: vi.fn(),
    changeSite: vi.fn(),
  },
}));

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const api = bookingsApi as any;

const booking = {
  id: 1, osmBookingId: '179743', customerName: 'Test', startDate: '2027-12-04',
  endDate: '2027-12-05', status: 'Provisional', fullDetails: '', comments: [], linkedEmails: [],
};
const initialItems: BookingItem[] = [
  { itemId: '411467', type: 'site', siteId: '1387', label: 'Hayvern', startDate: '2027-12-04', endDate: '2027-12-05' },
];

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
});

describe('move-dates action', () => {
  it('submits dayShift after confirmation and shows a success banner', async () => {
    const result: BookingActionResult = {
      created: ['999'], deleted: ['411467'], status: 'completed', message: 'Replaced 1 item(s) successfully.',
      items: [{ ...initialItems[0], itemId: '999', startDate: '2027-12-11', endDate: '2027-12-12' }],
    };
    api.moveDates.mockResolvedValue(result);
    const user = userEvent.setup();
    renderDetail();

    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '7');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));

    // Confirmation gate appears; no API call yet
    expect(api.moveDates).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveDates).toHaveBeenCalledWith(1, { dayShift: 7 }));
    expect(await screen.findByRole('status')).toHaveTextContent(/replaced 1 item/i);
    // Items list is refreshed in place from result.items (shifted date now shown)
    expect(await screen.findByText(/11 Dec 2027/)).toBeInTheDocument();
  });

  it('shows a red error banner when the action call throws', async () => {
    api.moveDates.mockRejectedValue(new Error('network down'));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '5');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    const banner = await screen.findByRole('status');
    expect(banner).toHaveTextContent(/could not be completed/i);
    expect(banner.className).toMatch(/red/);
  });

  it('shows an amber warnings banner for completed_with_warnings', async () => {
    api.moveDates.mockResolvedValue({
      created: ['999'], deleted: [], status: 'completed_with_warnings',
      message: 'Created 1 item(s) but only deleted 0 of 1 originals.', items: initialItems,
    } as BookingActionResult);
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '3');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    const banner = await screen.findByRole('status');
    expect(banner).toHaveTextContent(/only deleted 0 of 1/i);
    expect(banner.className).toMatch(/amber|yellow/);
  });

  it('cancelling the confirmation does not call the API', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '2');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(api.moveDates).not.toHaveBeenCalled();
  });
});
