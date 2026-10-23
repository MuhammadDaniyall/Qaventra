import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Banknote,
  CalendarDays,
  CalendarPlus,
  Check,
  ChevronDown,
  Clock,
  HandCoins,
  Inbox,
  GraduationCap,
  TimerOff,
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
  Modal,
  PageHeader,
  SkeletonRows,
  Switch,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { ClaimDrawer } from '../expenses/shared';
import { ProfileChangeDetails } from '../profile/ProfileChanges';
import { Delegations } from './Delegations';

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
};

export function ApprovalsInboxPage() {
  const user = useUser();
  const hr = isHr(user);
  const [filter, setFilter] = useState<Filter>('all');
  const [onlyMine, setOnlyMine] = useState(true);
  const [open, setOpen] = useState<string | null>(null);
  const [claimId, setClaimId] = useState<string | null>(null);
  const [deciding, setDeciding] = useState<{ item: InboxItem; approve: boolean } | null>(null);
  const inbox = useQuery({ queryKey: qk.approvalInbox, queryFn: approvalsApi.inbox });

  const all = inbox.data ?? [];
  const visible = all.filter((i) => (filter === 'all' || i.requestType === filter) && (!hr || !onlyMine || i.assignedToMe));
  const count = (t: Filter) => all.filter((i) => (t === 'all' || i.requestType === t) && (!hr || !onlyMine || i.assignedToMe)).length;

  return (
    <>
      <PageHeader
        title="Approvals"
        description={
          all.length
            ? `${count('all')} request${count('all') === 1 ? '' : 's'} waiting for your decision.`
            : 'Leave requests, expense claims and more, in one place.'
        }
      />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <Tabs<Filter>
          value={filter}
          onChange={setFilter}
          items={(
            [
              'all',
              'Leave',
              'Expense',
              'Attendance',
              'CompOff',
              'PayRequest',
              'Loan',
              'ProfileChange',
              'ShiftChange',
              'Asset',
              'JobRequest',
              'Travel',
              'Training',
              'Exemption',
            ] as Filter[]
          ).map((t) => ({
            value: t,
            label: (
              <>
                {t === 'all' ? 'All' : REQUEST_LABELS[t]}
                {count(t) > 0 && (
                  <span className="ml-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800 dark:bg-amber-500/20 dark:text-amber-200">{count(t)}</span>
                )}
              </>
            ),
          }))}
        />
        {hr && (
          <div className="w-72">
            <Switch checked={onlyMine} onChange={setOnlyMine} label="Only steps assigned to me" />
          </div>
        )}
      </div>

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
                  <div className="flex flex-wrap items-center gap-4 px-4 py-3.5 sm:px-5">
                    <button
                      type="button"
                      onClick={() => setOpen(expanded ? null : item.subjectId)}
                      aria-expanded={expanded}
                      className="flex min-w-0 flex-1 items-center gap-3 text-left"
                    >
                      <Avatar name={item.employeeName} size="sm" />
                      <span className="min-w-0">
                        <span className="flex items-center gap-2">
                          <span className="truncate font-medium text-zinc-900 dark:text-white">{item.employeeName}</span>
                          <Icon className="size-3.5 shrink-0 text-zinc-400" aria-label={item.requestType} />
                          {!item.assignedToMe && <Badge tone="blue">Waiting for someone else</Badge>}
                          {item.actingFor && <Badge tone="violet">For {item.actingFor}</Badge>}
                        </span>
                        <span className="block truncate text-sm text-zinc-600 dark:text-zinc-300">{item.summary}</span>
                        <span className="block text-xs text-zinc-500">
                          {item.totalSteps > 1 ? `Step ${item.step} of ${item.totalSteps} · ` : ''}submitted {formatDateTimeUtc(item.createdAtUtc)}
                        </span>
                      </span>
                      <ChevronDown className={cn('ml-auto size-4 shrink-0 text-zinc-400 transition-transform', expanded && 'rotate-180')} aria-hidden="true" />
                    </button>
                    <div className="flex gap-2">
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
