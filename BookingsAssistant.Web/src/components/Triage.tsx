import { useState, useEffect, useCallback } from 'react';
import { plansApi } from '../services/apiClient';
import type { PlanAction, PlanActionExecutionResult, PlanStatusValue, ProposedPlan } from '../types';

const STATUS_FILTERS: Array<PlanStatusValue | 'All'> = ['All', 'AwaitingApproval', 'Executed', 'Rejected', 'Failed'];

function parseActions(actionsJson?: string | null): PlanAction[] {
  if (!actionsJson) return [];
  try {
    const parsed = JSON.parse(actionsJson);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function parseResults(executionResultJson?: string | null): PlanActionExecutionResult[] {
  if (!executionResultJson) return [];
  try {
    const parsed = JSON.parse(executionResultJson);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

// Renders each action type as a short human-readable label. draftEmailReply is handled
// separately by PlanActionRow (shown as a copyable text block) so it isn't described here.
function describeAction(action: PlanAction): string {
  switch (action.type) {
    case 'postComment':
      return `Post comment: ${action.text ?? ''}`;
    case 'sendTemplateEmail':
      return 'Send template email';
    case 'moveDates':
      return `Move dates by ${action.dayShift ?? '?'} day(s)${action.note ? ` — ${action.note}` : ''}`;
    case 'changeSite':
      return `Change site for item ${action.itemId} to ${action.newSiteName ?? action.newSiteId}`;
    case 'moveActivity': {
      const when = [action.newStartDate, action.newStartTime].filter(Boolean).join(' ');
      return `Move activity ${action.itemId}${when ? ` to ${when}` : ''}`;
    }
    case 'addActivity': {
      const when = [action.newStartDate, action.newStartTime].filter(Boolean).join(' ');
      const people = action.numberPeople != null ? ` for ${action.numberPeople} people` : '';
      return `Add activity ${action.activityId}${when ? ` on ${when}` : ''}${people}`;
    }
    case 'removeActivity':
      return `Remove item ${action.itemId}${action.note ? ` — ${action.note}` : ''}`;
    case 'changeNumbers':
      return `Change numbers for item ${action.itemId} to ${action.newNumberPeople ?? '?'}${action.note ? ` — ${action.note}` : ''}`;
    case 'checkAvailability': {
      const when = [action.newStartDate, action.newEndDate].filter(Boolean).join(' to ');
      return `Check availability for ${action.activityId}${when ? ` (${when})` : ''}`;
    }
    default:
      return `Unknown action: ${action.type}`;
  }
}

function resultBadgeClass(status: string): string {
  switch (status) {
    case 'succeeded': return 'bg-green-100 text-green-800';
    case 'failed': return 'bg-red-100 text-red-800';
    default: return 'bg-gray-100 text-gray-600'; // not_attempted
  }
}

function statusBadgeClass(status: string): string {
  switch (status) {
    case 'AwaitingApproval': return 'bg-yellow-100 text-yellow-800';
    case 'Executed': return 'bg-green-100 text-green-800';
    case 'Rejected': return 'bg-gray-100 text-gray-600';
    default: return 'bg-red-100 text-red-800'; // Failed
  }
}

function PlanActionRow({ action, result }: { action: PlanAction; result?: PlanActionExecutionResult }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(action.text ?? '');
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API unavailable (e.g. insecure context) — nothing more we can do.
    }
  };

  return (
    <div className="p-3 border border-gray-200 rounded bg-gray-50">
      {action.type === 'draftEmailReply' ? (
        <div>
          <div className="flex items-center justify-between mb-1">
            <span className="text-sm font-medium text-gray-700">Draft email reply</span>
            <button
              onClick={handleCopy}
              className="text-xs px-2 py-1 bg-blue-600 text-white rounded hover:bg-blue-700"
            >
              {copied ? 'Copied!' : 'Copy'}
            </button>
          </div>
          <pre className="whitespace-pre-wrap text-sm text-gray-800 select-all font-sans">{action.text}</pre>
        </div>
      ) : (
        <div className="text-sm text-gray-800">{describeAction(action)}</div>
      )}
      {result && (
        <div className="mt-2">
          <span className={`inline-block px-2 py-0.5 text-xs rounded ${resultBadgeClass(result.status)}`}>
            {result.status}
          </span>
          {result.reason && <span className="ml-2 text-xs text-red-700">{result.reason}</span>}
          {result.detail && <span className="ml-2 text-xs text-gray-700">{result.detail}</span>}
        </div>
      )}
    </div>
  );
}

interface PlanDetailProps {
  plan: ProposedPlan;
  onApprove: () => void;
  onReject: () => void;
  busy: boolean;
}

function PlanDetail({ plan, onApprove, onReject, busy }: PlanDetailProps) {
  const actions = parseActions(plan.actionsJson);
  const results = parseResults(plan.executionResultJson);

  return (
    <div className="bg-white rounded-lg shadow p-6 mb-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-800">Plan #{plan.id}</h2>
        <span className={`px-3 py-1 rounded-full text-sm font-medium ${statusBadgeClass(plan.status)}`}>
          {plan.status}
        </span>
      </div>

      {plan.osmBookingId && (
        <p className="text-sm text-gray-600 mb-2">Booking: {plan.osmBookingId}</p>
      )}

      {plan.draftWarning && (
        <div className="mb-4 p-3 bg-amber-50 border border-amber-300 rounded text-sm text-amber-800">
          <span className="font-semibold">Availability warning: </span>
          {plan.draftWarning}
        </div>
      )}

      {plan.sourceEmailText && (
        <div className="mb-4">
          <h3 className="text-sm font-semibold text-gray-700 mb-1">Source email</h3>
          <p className="text-sm text-gray-600 whitespace-pre-wrap p-3 bg-gray-50 rounded border border-gray-200">
            {plan.sourceEmailText}
          </p>
        </div>
      )}

      <div className="space-y-2 mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Actions</h3>
        {actions.length === 0 ? (
          <p className="text-sm text-gray-400">No actions.</p>
        ) : (
          actions.map((action, i) => (
            <PlanActionRow key={i} action={action} result={results[i]} />
          ))
        )}
      </div>

      {plan.status === 'AwaitingApproval' && (
        <div className="flex gap-2 border-t pt-4">
          <button
            onClick={onApprove}
            disabled={busy}
            className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50"
          >
            {busy ? 'Working…' : 'Approve'}
          </button>
          <button
            onClick={onReject}
            disabled={busy}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50"
          >
            {busy ? 'Working…' : 'Reject'}
          </button>
        </div>
      )}
    </div>
  );
}

export default function Triage() {
  const [sourceEmailText, setSourceEmailText] = useState('');
  const [osmBookingId, setOsmBookingId] = useState('');
  const [drafting, setDrafting] = useState(false);
  const [draftError, setDraftError] = useState<string | null>(null);

  const [activePlan, setActivePlan] = useState<ProposedPlan | null>(null);
  const [busy, setBusy] = useState(false);

  const [plans, setPlans] = useState<ProposedPlan[]>([]);
  const [statusFilter, setStatusFilter] = useState<PlanStatusValue | 'All'>('All');
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const refreshList = useCallback(async (filter: PlanStatusValue | 'All') => {
    setListLoading(true);
    setListError(null);
    try {
      const result = await plansApi.list(filter === 'All' ? undefined : filter);
      setPlans(result);
    } catch (err) {
      setListError('Failed to load plans');
      console.error(err);
    } finally {
      setListLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshList(statusFilter);
  }, [statusFilter, refreshList]);

  const handleDraft = async () => {
    if (!sourceEmailText.trim()) return;
    setDrafting(true);
    setDraftError(null);
    try {
      const plan = await plansApi.create(sourceEmailText.trim(), osmBookingId.trim() || undefined);
      setActivePlan(plan);
      setSourceEmailText('');
      setOsmBookingId('');
      refreshList(statusFilter);
    } catch (err) {
      setDraftError('Failed to draft plan');
      console.error(err);
    } finally {
      setDrafting(false);
    }
  };

  // Shared by Approve/Reject: both call a plansApi endpoint for the active plan, replace it
  // with the server's updated copy, and refresh the list — only the endpoint and error copy differ.
  const runPlanDecision = async (fn: (id: number) => Promise<ProposedPlan>, failureMessage: string) => {
    if (!activePlan) return;
    setBusy(true);
    setDraftError(null);
    try {
      const updated = await fn(activePlan.id);
      setActivePlan(updated);
      refreshList(statusFilter);
    } catch (err) {
      setDraftError(failureMessage);
      console.error(err);
    } finally {
      setBusy(false);
    }
  };

  const handleApprove = () => runPlanDecision(plansApi.approve, 'Failed to approve plan');
  const handleReject = () => runPlanDecision(plansApi.reject, 'Failed to reject plan');

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      <h1 className="text-2xl font-bold text-gray-800 mb-6">Triage</h1>

      {/* Draft form */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h2 className="text-lg font-semibold text-gray-800 mb-4">Draft a plan from a customer email</h2>
        <div className="mb-3">
          <label htmlFor="sourceEmailText" className="block text-sm font-medium text-gray-700 mb-1">
            Customer email
          </label>
          <textarea
            id="sourceEmailText"
            rows={6}
            value={sourceEmailText}
            onChange={(e) => setSourceEmailText(e.target.value)}
            disabled={drafting}
            className="w-full p-3 border border-gray-300 rounded resize-none"
          />
        </div>
        <div className="mb-3">
          <label htmlFor="osmBookingId" className="block text-sm font-medium text-gray-700 mb-1">
            Booking ID (optional)
          </label>
          <input
            id="osmBookingId"
            type="text"
            value={osmBookingId}
            onChange={(e) => setOsmBookingId(e.target.value)}
            disabled={drafting}
            className="w-48 p-2 border border-gray-300 rounded"
          />
        </div>
        {draftError && <div className="text-red-600 text-sm mb-2">{draftError}</div>}
        <button
          onClick={handleDraft}
          disabled={drafting || !sourceEmailText.trim()}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
        >
          {drafting ? 'Drafting…' : 'Draft Plan'}
        </button>
      </div>

      {/* Detail view for the drafted or selected plan */}
      {activePlan && (
        <PlanDetail plan={activePlan} onApprove={handleApprove} onReject={handleReject} busy={busy} />
      )}

      {/* Plan list */}
      <div className="bg-white rounded-lg shadow p-6">
        <div className="flex flex-wrap items-center justify-between gap-2 mb-4">
          <h2 className="text-lg font-semibold text-gray-800">Plans</h2>
          <div className="flex flex-wrap gap-2">
            {STATUS_FILTERS.map((f) => (
              <button
                key={f}
                onClick={() => setStatusFilter(f)}
                className={`px-3 py-1 text-sm rounded ${
                  statusFilter === f ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                {f}
              </button>
            ))}
          </div>
        </div>

        {listLoading && <p className="text-sm text-gray-400">Loading…</p>}
        {listError && <div className="text-red-600 text-sm">{listError}</div>}
        {!listLoading && !listError && plans.length === 0 && (
          <p className="text-sm text-gray-400">No plans.</p>
        )}
        {!listLoading && !listError && plans.length > 0 && (
          <div className="divide-y divide-gray-100">
            {plans.map((plan) => (
              <button
                key={plan.id}
                onClick={() => setActivePlan(plan)}
                className="w-full text-left flex items-center justify-between px-2 py-3 hover:bg-gray-50"
              >
                <div>
                  <span className="font-medium text-gray-800">Plan #{plan.id}</span>
                  {plan.osmBookingId && (
                    <span className="ml-2 text-sm text-gray-500">Booking {plan.osmBookingId}</span>
                  )}
                </div>
                <span className={`px-2 py-1 text-xs rounded-full ${statusBadgeClass(plan.status)}`}>
                  {plan.status}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
