import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import BookingDetail from './BookingDetail';
import { bookingsApi } from '../services/apiClient';
import type { BookingActionResult, BookingItem } from '../types';

vi.mock('../services/apiClient', () => ({
  bookingsApi: {
    getById: vi.fn(), getItems: vi.fn(), getAvailableSites: vi.fn(),
    moveDates: vi.fn(), moveActivity: vi.fn(), changeSite: vi.fn(),
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

const ok = (items: BookingItem[]): BookingActionResult =>
  ({ created: ['999'], deleted: ['x'], status: 'completed', message: 'Replaced 1 item(s) successfully.', items });

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
  api.getAvailableSites.mockResolvedValue([
    { id: '1387', name: 'Hayvern' }, { id: '1404', name: 'Birch' }, { id: '1386', name: 'Alpha House' },
  ]);
});

describe('move-activity action', () => {
  it('submits new times for an activity item after confirmation', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.type(screen.getByLabelText(/new end time/i), '15:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));   // open confirm gate
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartTime: '14:00', newEndTime: '15:00',
    }));
    expect(await screen.findByRole('status')).toHaveTextContent(/replaced 1 item/i);
  });

  it('submits a new date when only the date field is filled', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new date/i), '2027-12-20');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartDate: '2027-12-20',
    }));
  });

  it('disables Move until at least one field is changed (no pointless no-op)', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    expect(screen.getByRole('button', { name: /^move$/i })).toBeDisabled();
  });
});

describe('change-site action', () => {
  it('offers other sites (excluding the current one) and submits the chosen site', async () => {
    api.changeSite.mockResolvedValue(ok([site]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /change site/i }));

    const select = await screen.findByLabelText(/new site/i);
    // Current site (Hayvern/1387) must be excluded; alternatives present
    expect(screen.queryByRole('option', { name: 'Hayvern' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Birch' })).toBeInTheDocument();

    await user.selectOptions(select, '1404');
    await user.click(screen.getByRole('button', { name: /^change$/i }));   // open confirm gate
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.changeSite).toHaveBeenCalledWith(1, { itemId: '411467', newSiteId: '1404', newSiteName: 'Birch' }));
  });

  it('shows a no-alternatives message when only the current site exists', async () => {
    api.getAvailableSites.mockResolvedValue([{ id: '1387', name: 'Hayvern' }]);
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /change site/i }));
    expect(await screen.findByText(/no alternative sites/i)).toBeInTheDocument();
    expect(api.changeSite).not.toHaveBeenCalled();
  });
});
