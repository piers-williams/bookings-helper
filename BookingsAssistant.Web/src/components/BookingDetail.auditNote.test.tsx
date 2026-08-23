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
    { id: '1387', name: 'Hayvern' }, { id: '1404', name: 'Birch' },
  ]);
  api.getAvailableActivities.mockResolvedValue([]);
});

describe('audit-trail note field', () => {
  it('sends the note with a move-activity confirm', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'customer requested a later slot');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartTime: '14:00', note: 'customer requested a later slot',
    }));
  });

  it('omits note from the move-activity request when left blank', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartTime: '14:00',
    }));
  });

  it('sends the note and resolved site name with a change-site confirm', async () => {
    api.changeSite.mockResolvedValue(ok([site]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /change site/i }));
    await user.selectOptions(await screen.findByLabelText(/new site/i), '1404');
    await user.click(screen.getByRole('button', { name: /^change$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'customer requested closer pitch');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.changeSite).toHaveBeenCalledWith(1, {
      itemId: '411467', newSiteId: '1404', newSiteName: 'Birch', note: 'customer requested closer pitch',
    }));
  });

  it('sends the note with a move-dates confirm', async () => {
    api.moveDates.mockResolvedValue(ok([site]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '7');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'weather forecast');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveDates).toHaveBeenCalledWith(1, {
      dayShift: 7, note: 'weather forecast',
    }));
  });

  it('clears the note when the form is closed and reopened', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'draft note');
    await user.click(screen.getByRole('button', { name: /cancel/i })); // back to pre-confirm stage
    await user.click(screen.getByRole('button', { name: /cancel/i })); // closes the form entirely

    await user.click(screen.getByRole('button', { name: /move activity/i })); // reopen
    await user.type(screen.getByLabelText(/new start time/i), '15:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));

    expect(screen.getByLabelText(/add a note/i)).toHaveValue('');
  });
});
