import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import Triage from './Triage';
import { plansApi } from '../services/apiClient';
import type { ProposedPlan } from '../types';

vi.mock('../services/apiClient', () => ({
  plansApi: {
    create: vi.fn(),
    list: vi.fn(),
    getById: vi.fn(),
    approve: vi.fn(),
    reject: vi.fn(),
  },
}));
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const api = plansApi as any;

// The plan's status also appears as filter-button text, so status-badge assertions must be
// scoped to the <span> badge rather than matching any element with that text.
function getStatusBadge(text: string | RegExp) {
  const matches = screen.getAllByText(text);
  const badge = matches.find((el) => el.tagName === 'SPAN');
  if (!badge) throw new Error(`No <span> status badge found matching ${text}`);
  return badge;
}

function renderTriage() {
  return render(
    <MemoryRouter initialEntries={['/triage']}>
      <Routes><Route path="/triage" element={<Triage />} /></Routes>
    </MemoryRouter>
  );
}

const draftReplyPlan: ProposedPlan = {
  id: 1,
  status: 'AwaitingApproval',
  sourceEmailText: 'Can I move my visit to next week?',
  osmBookingId: '179743',
  actionsJson: JSON.stringify([
    { type: 'draftEmailReply', text: 'Sure, we can move your visit — confirming the new dates now.' },
    { type: 'moveDates', dayShift: 7, note: 'Requested by customer' },
  ]),
  executionResultJson: null,
  createdAt: '2026-07-20T09:00:00Z',
};

const executedPlan: ProposedPlan = {
  id: 2,
  status: 'Executed',
  sourceEmailText: null,
  osmBookingId: '179744',
  actionsJson: JSON.stringify([
    { type: 'postComment', text: 'Customer confirmed arrival time' },
    { type: 'sendTemplateEmail' },
  ]),
  executionResultJson: JSON.stringify([
    { type: 'postComment', status: 'succeeded' },
    { type: 'sendTemplateEmail', status: 'succeeded' },
  ]),
  createdAt: '2026-07-19T09:00:00Z',
};

const partiallyFailedPlan: ProposedPlan = {
  id: 3,
  status: 'Failed',
  sourceEmailText: null,
  osmBookingId: '179745',
  actionsJson: JSON.stringify([
    { type: 'postComment', text: 'Noting the request' },
    { type: 'changeSite', itemId: '411467', newSiteId: '1404', newSiteName: 'Birch' },
    { type: 'sendTemplateEmail' },
  ]),
  executionResultJson: JSON.stringify([
    { type: 'postComment', status: 'succeeded' },
    { type: 'changeSite', status: 'failed', reason: 'Site 1404 is not available' },
    { type: 'sendTemplateEmail', status: 'not_attempted' },
  ]),
  createdAt: '2026-07-18T09:00:00Z',
};

beforeEach(() => {
  vi.clearAllMocks();
  api.list.mockResolvedValue([]);
});

describe('drafting a plan', () => {
  it('submits the pasted email and displays the resulting plan', async () => {
    api.create.mockResolvedValue(draftReplyPlan);
    const user = userEvent.setup();
    renderTriage();

    await user.type(screen.getByLabelText(/customer email/i), 'Can I move my visit to next week?');
    await user.type(screen.getByLabelText(/booking id/i), '179743');
    await user.click(screen.getByRole('button', { name: /draft plan/i }));

    await waitFor(() =>
      expect(api.create).toHaveBeenCalledWith('Can I move my visit to next week?', '179743')
    );
    expect(await screen.findByText(/plan #1/i)).toBeInTheDocument();
    expect(getStatusBadge(/awaitingapproval/i)).toBeInTheDocument();
  });

  it('shows an error if drafting fails', async () => {
    api.create.mockRejectedValue(new Error('boom'));
    const user = userEvent.setup();
    renderTriage();

    await user.type(screen.getByLabelText(/customer email/i), 'Hello');
    await user.click(screen.getByRole('button', { name: /draft plan/i }));

    expect(await screen.findByText(/failed to draft plan/i)).toBeInTheDocument();
  });
});

describe('plan list', () => {
  it('fetches and renders plans, refetching by status when a filter is chosen', async () => {
    api.list.mockResolvedValue([draftReplyPlan, executedPlan]);
    const user = userEvent.setup();
    renderTriage();

    await waitFor(() => expect(api.list).toHaveBeenCalledWith(undefined));
    expect(await screen.findByText(/plan #1/i)).toBeInTheDocument();
    expect(screen.getByText(/plan #2/i)).toBeInTheDocument();

    api.list.mockResolvedValue([executedPlan]);
    await user.click(screen.getByRole('button', { name: /^executed$/i }));

    await waitFor(() => expect(api.list).toHaveBeenCalledWith('Executed'));
  });

  it('opens a plan detail view when a plan row is selected', async () => {
    api.list.mockResolvedValue([executedPlan]);
    const user = userEvent.setup();
    renderTriage();

    const row = await screen.findByRole('button', { name: /plan #2/i });
    await user.click(row);

    expect(await screen.findByText(/post comment: customer confirmed arrival time/i)).toBeInTheDocument();
  });
});

describe('plan detail actions', () => {
  it('approves an AwaitingApproval plan and updates the displayed status', async () => {
    api.create.mockResolvedValue(draftReplyPlan);
    api.approve.mockResolvedValue({ ...draftReplyPlan, status: 'Executed', sourceEmailText: null });
    const user = userEvent.setup();
    renderTriage();

    await user.type(screen.getByLabelText(/customer email/i), 'Hi');
    await user.click(screen.getByRole('button', { name: /draft plan/i }));
    await screen.findByText(/plan #1/i);

    await user.click(screen.getByRole('button', { name: /^approve$/i }));

    await waitFor(() => expect(api.approve).toHaveBeenCalledWith(1));
    await waitFor(() => expect(getStatusBadge(/^executed$/i)).toBeInTheDocument());
  });

  it('rejects an AwaitingApproval plan and updates the displayed status', async () => {
    api.create.mockResolvedValue(draftReplyPlan);
    api.reject.mockResolvedValue({ ...draftReplyPlan, status: 'Rejected', sourceEmailText: null });
    const user = userEvent.setup();
    renderTriage();

    await user.type(screen.getByLabelText(/customer email/i), 'Hi');
    await user.click(screen.getByRole('button', { name: /draft plan/i }));
    await screen.findByText(/plan #1/i);

    await user.click(screen.getByRole('button', { name: /^reject$/i }));

    await waitFor(() => expect(api.reject).toHaveBeenCalledWith(1));
    await waitFor(() => expect(getStatusBadge(/^rejected$/i)).toBeInTheDocument());
  });

  it('does not show Approve/Reject for a plan that is not AwaitingApproval', async () => {
    api.list.mockResolvedValue([executedPlan]);
    const user = userEvent.setup();
    renderTriage();

    await user.click(await screen.findByRole('button', { name: /plan #2/i }));

    await screen.findByText(/post comment: customer confirmed arrival time/i);
    expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument();
  });
});

describe('action rendering', () => {
  it('renders a draftEmailReply action as copyable text and an OSM action as a readable description', async () => {
    api.create.mockResolvedValue(draftReplyPlan);
    const user = userEvent.setup();
    renderTriage();

    await user.type(screen.getByLabelText(/customer email/i), 'Hi');
    await user.click(screen.getByRole('button', { name: /draft plan/i }));
    await screen.findByText(/plan #1/i);

    expect(screen.getByText(/sure, we can move your visit/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy/i })).toBeInTheDocument();
    expect(screen.getByText(/move dates by 7 day\(s\)/i)).toBeInTheDocument();
  });

  it('distinguishes succeeded, failed, and not_attempted actions in a partial-failure result', async () => {
    api.list.mockResolvedValue([partiallyFailedPlan]);
    const user = userEvent.setup();
    renderTriage();

    await user.click(await screen.findByRole('button', { name: /plan #3/i }));

    const commentRow = (await screen.findByText(/post comment: noting the request/i)).closest('div')!.parentElement!;
    expect(within(commentRow).getByText(/succeeded/i)).toBeInTheDocument();

    const changeSiteRow = screen.getByText(/change site/i).closest('div')!.parentElement!;
    expect(within(changeSiteRow).getByText(/failed/i)).toBeInTheDocument();
    expect(within(changeSiteRow).getByText(/site 1404 is not available/i)).toBeInTheDocument();

    const templateEmailRow = screen.getByText(/send template email/i).closest('div')!.parentElement!;
    expect(within(templateEmailRow).getByText(/not_attempted/i)).toBeInTheDocument();
  });

  it('renders an addActivity action with a readable description', async () => {
    api.list.mockResolvedValue([{
      id: 4,
      status: 'AwaitingApproval',
      sourceEmailText: 'Can we add an archery session?',
      osmBookingId: '179746',
      actionsJson: JSON.stringify([
        { type: 'addActivity', activityId: '4962', newStartDate: '2026-08-02', newStartTime: '10:00', numberPeople: 8 },
      ]),
      executionResultJson: null,
      createdAt: '2026-07-21T09:00:00Z',
    }]);
    const user = userEvent.setup();
    renderTriage();

    await user.click(await screen.findByRole('button', { name: /plan #4/i }));

    expect(screen.getByText(/add activity 4962/i)).toBeInTheDocument();
    expect(screen.getByText(/8 people/i)).toBeInTheDocument();
  });

  it('renders a removeActivity action with a readable description', async () => {
    api.list.mockResolvedValue([{
      id: 5,
      status: 'AwaitingApproval',
      sourceEmailText: 'Please cancel our archery session',
      osmBookingId: '179747',
      actionsJson: JSON.stringify([
        { type: 'removeActivity', itemId: '411468', note: 'customer cancelled' },
      ]),
      executionResultJson: null,
      createdAt: '2026-07-22T09:00:00Z',
    }]);
    const user = userEvent.setup();
    renderTriage();

    await user.click(await screen.findByRole('button', { name: /plan #5/i }));

    expect(screen.getByText(/remove item 411468/i)).toBeInTheDocument();
  });

  it('renders a changeNumbers action with a readable description', async () => {
    api.list.mockResolvedValue([{
      id: 6,
      status: 'AwaitingApproval',
      sourceEmailText: 'Two more people are joining our archery session.',
      osmBookingId: '179748',
      actionsJson: JSON.stringify([
        { type: 'changeNumbers', itemId: '411468', newNumberPeople: 10, note: 'two more joined' },
      ]),
      executionResultJson: null,
      createdAt: '2026-07-23T09:00:00Z',
    }]);
    const user = userEvent.setup();
    renderTriage();

    await user.click(await screen.findByRole('button', { name: /plan #6/i }));

    expect(screen.getByText(/change numbers for item 411468 to 10/i)).toBeInTheDocument();
  });
});
