import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Banknote,
  CalendarDays,
  CalendarPlus,
  Check,
  CheckCheck,
  ChevronDown,
  Clock,
  HandCoins,
  Inbox,
  GraduationCap,
  TimerOff,
  CalendarOff,
  ClipboardList,
  Search,
  PiggyBank,
  UserMinus,
  FileSignature,
  Package,
  Plane,
  Receipt,
  Repeat,
  UserPen,
  UserPlus,
  X,
  type LucideIcon,
} from 'lucide-react';
import { approvalsApi, REQUEST_LABELS, type ApprovalRequestType, type InboxItem } from '../../api/approvals';
import { attendanceRequestsApi } from '../../api/attendance';
import { profileChangesApi } from '../../api/employees';
import { assetsApi } from '../../api/assets';
import { expensesApi } from '../../api/expenses';
import { schedulingApi } from '../../api/scheduling';
import { qk } from '../../api/keys';
import { recruitmentApi } from '../../api/recruitment';
import { trainingApi } from '../../api/training';
import { exemptionsApi } from '../../api/leaveRules';
import { attendanceRulesApi } from '../../api/attendanceRules';
import { projectsApi } from '../../api/projects';
import { fundsApi } from '../../api/funds';
import { lettersApi } from '../../api/letters';
import { expenseSetupApi } from '../../api/expenseSetup';
import { travelApi } from '../../api/travel';
import { compOffApi, leaveApi } from '../../api/leave';
import { payApi } from '../../api/pay';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import {
  Avatar,
  Badge,
  Button,
  Card,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Modal,
  PageHeader,
  SkeletonRows,
  Switch,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { ClaimDrawer } from '../expenses/shared';
import { ProfileChangeDetails } from '../profile/ProfileChanges';
import { Delegations } from './Delegations';
import { tx } from '../../lib/i18n';

const TYPE_ICONS: Record<ApprovalRequestType, LucideIcon> = {
  Leave: CalendarDays,
  Expense: Receipt,
  Attendance: Clock,
  CompOff: CalendarPlus,
  PayRequest: Banknote,
  Loan: HandCoins,
  ProfileChange: UserPen,
  ShiftChange: Repeat,
  Asset: Package,
  JobRequest: UserPlus,
  Travel: Plane,
  Training: GraduationCap,
  Exemption: TimerOff,
  OffDay: CalendarOff,
  Timesheet: ClipboardList,
  FundWithdrawal: PiggyBank,
  ExpenseAdvance: HandCoins,
  FinalSettlement: UserMinus,
  Letter: FileSignature,
};

type Filter = 'all' | ApprovalRequestType;

const decideApi: Record<
  ApprovalRequestType,
  { approve: (id: string, note: string | null) => Promise<void>; reject: (id: string, note: string | null) => Promise<void> }
> = {
  Leave: { approve: leaveApi.approve, reject: leaveApi.reject },
  Expense: { approve: expensesApi.approve, reject: expensesApi.reject },
  Attendance: { approve: attendanceRequestsApi.approve, reject: attendanceRequestsApi.reject },
  CompOff: { approve: compOffApi.approve, reject: compOffApi.reject },
  PayRequest: { approve: payApi.approve, reject: payApi.reject },
  Loan: { approve: payApi.approveLoan, reject: payApi.rejectLoan },
  ProfileChange: { approve: profileChangesApi.approve, reject: profileChangesApi.reject },
  ShiftChange: { approve: schedulingApi.approve, reject: schedulingApi.reject },
  Asset: { approve: assetsApi.approve, reject: assetsApi.reject },
  JobRequest: { approve: recruitmentApi.approveRequest, reject: recruitmentApi.rejectRequest },
  Travel: { approve: travelApi.approve, reject: travelApi.reject },
  Training: { approve: trainingApi.approve, reject: trainingApi.reject },
  Exemption: { approve: exemptionsApi.approve, reject: exemptionsApi.reject },
  OffDay: { approve: attendanceRulesApi.approveOffDays, reject: attendanceRulesApi.rejectOffDays },
  Timesheet: { approve: projectsApi.approve, reject: projectsApi.reject },
  FundWithdrawal: { approve: fundsApi.approve, reject: fundsApi.reject },
  ExpenseAdvance: { approve: expenseSetupApi.approveAdvance, reject: expenseSetupApi.rejectAdvance },
  Letter: { approve: lettersApi.approve, reject: lettersApi.reject },
  FinalSettlement: {
    approve: (id, note) => approvalsApi.decide('FinalSettlement', id, { approve: true, note }),
    reject: (id, note) => approvalsApi.decide('FinalSettlement', id, { approve: false, note }),
  },
};

export function ApprovalsInboxPage() {
  const user = useUser();
  const hr = isHr(user);
  const [filter, setFilter] = useState<Filter>('all');
  const [onlyMine, setOnlyMine] = useState(true);
  const [open, setOpen] = useState<string | null>(null);
  const [claimId, setClaimId] = useState<string | null>(null);
  const [deciding, setDeciding] = useState<{ item: InboxItem; approve: boolean } | null>(null);
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<string[]>([]);
  const [bulk, setBulk] = useState(false);
  const inbox = useQuery({ queryKey: qk.approvalInbox, queryFn: approvalsApi.inbox });

  const term = search.trim().toLowerCase();
  const all = (inbox.data ?? []).filter(
    (i) => (!hr || !onlyMine || i.assignedToMe) && (!term || i.employeeName.toLowerCase().includes(term) || i.employeeCode.toLowerCase().includes(term)),
  );
  const visible = all.filter((i) => filter === 'all' || i.requestType === filter);
  const count = (t: Filter) => all.filter((i) => t === 'all' || i.requestType === t).length;
  const types = [...new Set(all.map((i) => i.requestType))];
  // Expense claims need their receipts looked at, so they are approved one by one.
  const bulkable = visible.filter((i) => i.assignedToMe && i.requestType !== 'Expense');
  const toApprove = bulkable.filter((i) => selected.includes(i.subjectId));

  return (
    <>
      <PageHeader
        title="Approvals"
        description={
          all.length
            ? `${all.length} request${all.length === 1 ? '' : 's'} waiting for your decision.`
            : 'Leave requests, expense claims and more, in one place.'
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <div className="relative w-full sm:w-64">
          <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by name" aria-label="Search by name" className="ps-9" />
        </div>
        {hr && (
          <div className="sm:ms-auto">
            <Switch checked={onlyMine} onChange={setOnlyMine} label="Only steps assigned to me" />
          </div>
        )}
      </div>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="flex flex-wrap gap-1.5" role="tablist" aria-label="Request type">
          {(['all', ...types] as Filter[]).map((t) => (
            <button
              key={t}
              type="button"
              role="tab"
              aria-selected={filter === t}
              onClick={() => setFilter(t)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm font-medium transition-colors',
                filter === t
                  ? 'border-accent-200 bg-accent-50 text-accent-700 dark:border-accent-500/30 dark:bg-accent-500/10 dark:text-accent-300'
                  : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800',
              )}
            >
              {tx(t === 'all' ? 'All' : REQUEST_LABELS[t])}
              <span className="text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">{count(t)}</span>
            </button>
          ))}
        </div>
      </div>

      {bulkable.length > 1 && (
        <div className="sticky top-14 z-20 mb-3 flex flex-wrap items-center gap-3 rounded-xl border border-zinc-200 bg-white/95 px-4 py-2.5 backdrop-blur dark:border-zinc-800 dark:bg-zinc-900/95">
          <Checkbox
            label={toApprove.length ? `${toApprove.length} selected` : 'Select all'}
            checked={toApprove.length > 0 && toApprove.length === bulkable.length}
            onChange={(e) => setSelected(e.target.checked ? bulkable.map((i) => i.subjectId) : [])}
          />
          {toApprove.length > 0 && (
            <Button size="sm" variant="success" className="ms-auto" icon={<CheckCheck className="size-3.5" />} onClick={() => setBulk(true)}>
              Approve {toApprove.length}
            </Button>
          )}
        </div>
      )}

      <Card>
        {inbox.isLoading ? (
          <SkeletonRows rows={5} cols={4} />
        ) : inbox.isError ? (
          <ErrorState error={inbox.error} onRetry={() => inbox.refetch()} />
        ) : !visible.length ? (
          <EmptyState icon={Inbox} title="All caught up" description="Nothing is waiting for you right now." />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {visible.map((item) => {
              const Icon = TYPE_ICONS[item.requestType];
              const expanded = open === item.subjectId;
              return (
                <li key={item.subjectId}>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3.5 sm:px-5">
                    {bulkable.length > 1 && (
                      <input
                        type="checkbox"
                        aria-label={`Select ${item.employeeName}`}
                        disabled={!bulkable.includes(item)}
                        checked={selected.includes(item.subjectId)}
                        onChange={(e) =>
                          setSelected((s) => (e.target.checked ? [...s, item.subjectId] : s.filter((x) => x !== item.subjectId)))
                        }
                        className="size-4 shrink-0 rounded border-zinc-300 accent-accent-600 disabled:opacity-30"
                      />
                    )}
                    <button
                      type="button"
                      onClick={() => setOpen(expanded ? null : item.subjectId)}
                      aria-expanded={expanded}
                      className="flex min-w-0 flex-1 items-center gap-3 text-start"
                    >
                      <Avatar name={item.employeeName} size="sm" />
                      <span className="min-w-0">
                        <span className="flex items-center gap-2">
                          <span className="truncate font-medium text-zinc-900 dark:text-white">{item.employeeName}</span>
                          <Icon className="size-3.5 shrink-0 text-zinc-500 dark:text-zinc-400" aria-label={item.requestType} />
                          {!item.assignedToMe && <Badge tone="blue">Waiting for someone else</Badge>}
                          {item.actingFor && <Badge tone="violet">For {item.actingFor}</Badge>}
                        </span>
                        <span className="block truncate text-sm text-zinc-600 dark:text-zinc-300">{item.summary}</span>
                        {item.filedBy && <span className="block text-xs text-zinc-500 dark:text-zinc-400">Filed by {item.filedBy}</span>}
                        <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                          {item.totalSteps > 1 ? `Step ${item.step} of ${item.totalSteps} · ` : ''}submitted {formatDateTimeUtc(item.createdAtUtc)}
                        </span>
                      </span>
                      <ChevronDown className={cn('ms-auto size-4 shrink-0 text-zinc-500 dark:text-zinc-400 transition-transform', expanded && 'rotate-180')} aria-hidden="true" />
                    </button>
                    <div className="flex w-full gap-2 sm:w-auto [&>button]:flex-1 sm:[&>button]:flex-none">
                      {item.requestType === 'Expense' ? (
                        <Button size="sm" variant="secondary" onClick={() => setClaimId(item.subjectId)}>
                          Review receipts
                        </Button>
                      ) : (
                        <Button size="sm" variant="secondary" icon={<X className="size-3.5" />} onClick={() => setDeciding({ item, approve: false })}>
                          Reject
                        </Button>
                      )}
                      <Button size="sm" variant="success" icon={<Check className="size-3.5" />} onClick={() => setDeciding({ item, approve: true })}>
                        Approve
                      </Button>
                    </div>
                  </div>
                  {expanded && (
                    <div className="space-y-4 border-t border-zinc-100 bg-zinc-50/60 px-5 py-4 dark:border-zinc-800 dark:bg-zinc-900/40">
                      {item.requestType === 'ProfileChange' && <ProfileChangeDetails id={item.subjectId} />}
                      <ApprovalTimeline type={item.requestType} subjectId={item.subjectId} />
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>

      <Delegations />

      {bulk && (
        <BulkApproveModal
          items={toApprove}
          onClose={(done) => {
            setBulk(false);
            if (done) setSelected([]);
          }}
        />
      )}
      {deciding && <DecisionModal {...deciding} onClose={() => setDeciding(null)} />}
      {claimId && <ClaimDrawer claimId={claimId} onClose={() => setClaimId(null)} />}
    </>
  );
}

function DecisionModal({ item, approve, onClose }: { item: InboxItem; approve: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const hr = isHr(useUser());
  const [note, setNote] = useState('');
  const [force, setForce] = useState(false);
  const decide = useMutation({
    mutationFn: () =>
      force
        ? approvalsApi.decide(item.requestType, item.subjectId, { approve: true, note: note.trim() || null, force: true })
        : decideApi[item.requestType][approve ? 'approve' : 'reject'](item.subjectId, note.trim() || null),
    onSuccess: () => {
      toast.success(approve ? 'Approved' : 'Rejected', `${item.employeeName} · ${item.summary}`);
      qc.invalidateQueries({ queryKey: qk.approvals });
      qc.invalidateQueries({
        queryKey: item.requestType === 'Leave' || item.requestType === 'CompOff' ? qk.leave : item.requestType === 'Expense' ? qk.expenses : qk.attendance,
      });
      qc.invalidateQueries({ queryKey: qk.pay });
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.assets });
      qc.invalidateQueries({ queryKey: qk.recruitment });
      qc.invalidateQueries({ queryKey: qk.travel });
      qc.invalidateQueries({ queryKey: qk.training });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      onClose();
    },
    onError: (err) => toast.error(err),
  });
  const more = item.step < item.totalSteps;

  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={approve ? 'Approve request?' : 'Reject request?'}
      description={`${item.employeeName} · ${item.summary}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant={approve ? 'success' : 'danger'} loading={decide.isPending} disabled={force && !note.trim()} onClick={() => decide.mutate()}>
            {force ? 'Approve all steps' : approve ? 'Approve' : 'Reject'}
          </Button>
        </>
      }
    >
      {approve && more && !force && (
        <p className="mb-4 rounded-lg bg-sky-50 p-3 text-sm text-sky-800 dark:bg-sky-500/10 dark:text-sky-200">
          After you approve, it goes to step {item.step + 1} of {item.totalSteps}.
        </p>
      )}
      {approve && hr && more && (
        <Checkbox
          className="mb-4"
          label="Approve over the whole chain"
          description="Skips the remaining approvers. Shown as forcefully approved; a reason is required."
          checked={force}
          onChange={(e) => setForce(e.target.checked)}
        />
      )}
      <Field
        label={approve ? 'Note' : 'Reason'}
        required={force}
        hint={force ? 'Why it cannot wait for the other approvers' : 'Optional, shown to the employee'}
      >
        <Textarea value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
    </Modal>
  );
}

/** Approves the selected requests one after another and reports how many went through. */
function BulkApproveModal({ items, onClose }: { items: InboxItem[]; onClose: (done: boolean) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const run = useMutation({
    mutationFn: async () => {
      const results = await Promise.allSettled(items.map((i) => decideApi[i.requestType].approve(i.subjectId, note.trim() || null)));
      return results.filter((r) => r.status === 'rejected').length;
    },
    onSuccess: (failed) => {
      const ok = items.length - failed;
      if (failed) toast.error(`${failed} could not be approved`, `${ok} approved. Open the others to see why.`);
      else toast.success(`${ok} approved`);
      qc.invalidateQueries();
      onClose(true);
    },
    onError: (err) => toast.error(err),
  });

  return (
    <Modal
      open
      onClose={() => onClose(false)}
      size="sm"
      title={`Approve ${items.length} requests?`}
      description="Each one moves to its next approver, or is approved if this is the last step."
      footer={
        <>
          <Button variant="secondary" onClick={() => onClose(false)}>
            Cancel
          </Button>
          <Button variant="success" loading={run.isPending} onClick={() => run.mutate()}>
            Approve {items.length}
          </Button>
        </>
      }
    >
      <ul className="mb-4 max-h-48 space-y-1 overflow-y-auto text-sm">
        {items.map((i) => (
          <li key={i.subjectId} className="flex gap-2">
            <span className="font-medium text-zinc-900 dark:text-white">{i.employeeName}</span>
            <span className="truncate text-zinc-500 dark:text-zinc-400">{i.summary}</span>
          </li>
        ))}
      </ul>
      <Field label="Note" hint="Optional, shown to each employee">
        <Textarea value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
    </Modal>
  );
}
