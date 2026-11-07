import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, CalendarDays, Check, ClipboardCheck, Inbox, Palmtree, Paperclip, Scale, X } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { leaveApi } from '../../api/leave';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import { durationLabel } from './MyLeavePage';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { ExportRangeButton } from '../../components/excel/ExcelButtons';
import { RecordStampCell } from '../../components/RecordStamp';
import { LeaveDetailDrawer } from './LeaveDetail';
import {
  Avatar,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Field,
  Modal,
  PageHeader,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { StatCard } from '../../components/ui/StatCard';
import { txf } from '../../lib/i18n';
import { formatDate, formatDateTimeUtc, formatNumber } from '../../lib/format';
import type { LeaveRequest, LeaveStatus } from '../../types';
import { ModuleSettingsButton } from '../../components/ModuleSettingsButton';

type TabValue = Extract<LeaveStatus, 'Pending' | 'Approved' | 'Rejected'>;

/** What waits for a decision: requests, days asked for, and how soon they start. */
function LeaveOverview({ pending, loading }: { pending: LeaveRequest[] | undefined; loading: boolean }) {
  const list = pending ?? [];
  const today = new Date().toISOString().slice(0, 10);
  const week = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10);
  const days = list.reduce((n, r) => n + r.days, 0);
  const soon = list.filter((r) => r.fromDate <= week).length;
  const started = list.filter((r) => r.fromDate <= today).length;
  const types = new Set(list.map((r) => r.leaveType)).size;
  return (
    <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
      <StatCard label="Waiting for you" value={list.length} icon={Inbox} tone="violet" loading={loading} />
      <StatCard label="Days asked for" value={formatNumber(days)} icon={CalendarDays} tone="blue" loading={loading} />
      <StatCard label="Starting within a week" value={soon} icon={CalendarClock} tone={soon ? 'amber' : 'green'} loading={loading} hint={started ? txf('{n} already started', { n: started }) : undefined} />
      <StatCard label="Leave types" value={types} icon={Palmtree} tone="green" loading={loading} />
    </div>
  );
}

export function LeaveApprovalsPage() {
  const hr = isHr(useUser());
  const navigate = useNavigate();
  const [status, setStatus] = useState<TabValue>('Pending');
  const [deciding, setDeciding] = useState<{ request: LeaveRequest; approve: boolean } | null>(null);
  const [viewing, setViewing] = useState<LeaveRequest | null>(null);
  const list = useQuery({ queryKey: qk.leaveApprovals(status), queryFn: () => leaveApi.approvals(status) });
  const pendingCount = useQuery({ queryKey: qk.leaveApprovals('Pending'), queryFn: () => leaveApi.approvals('Pending') });

  return (
    <>
      <PageHeader
        title="Leave approvals"
        description="Review leave requests from people who report to you."
        actions={
          hr && (
            <>
              <ModuleSettingsButton to="/leave/settings" />
              <Button variant="secondary" icon={<Scale className="size-4" />} onClick={() => navigate('/leave/opening-balances')}>
                Opening balances
              </Button>
              <ExportRangeButton path="/exports/leave" name="leave" />
            </>
          )
        }
      />
      <LeaveOverview pending={pendingCount.data} loading={pendingCount.isLoading} />
      <Tabs<TabValue>
        className="mb-4"
        value={status}
        onChange={setStatus}
        items={[
          {
            value: 'Pending',
            label: (
              <>
                Pending
                {!!pendingCount.data?.length && (
                  <span className="ms-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800 dark:bg-amber-500/20 dark:text-amber-200">
                    {pendingCount.data.length}
                  </span>
                )}
              </>
            ),
          },
          { value: 'Approved', label: 'Approved' },
          { value: 'Rejected', label: 'Rejected' },
        ]}
      />
      <Card>
        {list.isLoading ? (
          <SkeletonRows rows={5} cols={6} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data?.length ? (
          <EmptyState
            icon={ClipboardCheck}
            title={status === 'Pending' ? 'All caught up' : `No ${status.toLowerCase()} requests`}
            description={status === 'Pending' ? 'There are no leave requests waiting for you.' : undefined}
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Type</Th>
                <Th>Dates</Th>
                <Th className="text-end">Days</Th>
                <Th>Reason</Th>
                <Th>{status === 'Pending' ? 'Requested' : 'Decided'}</Th>
                <Th>Added / modified</Th>
                {status === 'Pending' ? <Th className="text-end">Decision</Th> : <Th>Note</Th>}
              </tr>
            </THead>
            <TBody>
              {list.data.map((r) => (
                <Tr key={r.id} onClick={() => setViewing(r)}>
                  <Td>
                    <div className="flex items-center gap-3">
                      <Avatar name={r.employeeName} size="sm" />
                      <div>
                        <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.employeeName}</p>
                        <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{r.employeeCode}</p>
                      </div>
                    </div>
                  </Td>
                  <Td>{r.leaveType}</Td>
                  <Td>
                    {formatDate(r.fromDate)}
                    {r.toDate !== r.fromDate && <> – {formatDate(r.toDate)}</>}
                    {r.duration !== 'FullDay' && <span className="block text-xs text-zinc-500 dark:text-zinc-400">{durationLabel(r)}</span>}
                  </Td>
                  <Td className="text-end tabular-nums">{formatNumber(r.days)}</Td>
                  <Td className="max-w-xs truncate" title={r.reason ?? undefined}>
                    {r.attachmentCount > 0 && <Paperclip className="me-1 inline size-3.5 text-zinc-500 dark:text-zinc-400" aria-label={`${r.attachmentCount} file(s)`} />}
                    {r.reason ?? '—'}
                  </Td>
                  <Td>{formatDateTimeUtc(status === 'Pending' ? r.createdAtUtc : r.decidedAtUtc)}</Td>
                  <Td>
                    <RecordStampCell stamps={r.stamps} />
                  </Td>
                  {status === 'Pending' ? (
                    <Td className="text-end" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
                      <div className="inline-flex gap-2">
                        <Button size="sm" variant="secondary" icon={<X className="size-3.5" />} onClick={() => setDeciding({ request: r, approve: false })}>
                          Reject
                        </Button>
                        <Button size="sm" variant="success" icon={<Check className="size-3.5" />} onClick={() => setDeciding({ request: r, approve: true })}>
                          Approve
                        </Button>
                      </div>
                    </Td>
                  ) : (
                    <Td className="max-w-xs truncate" title={r.decisionNote ?? undefined}>
                      <span className="me-2"><StatusBadge status={r.status} /></span>
                      {r.decisionNote ?? ''}
                    </Td>
                  )}
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {deciding && <DecisionModal {...deciding} onClose={() => setDeciding(null)} />}
      <LeaveDetailDrawer request={viewing} onClose={() => setViewing(null)} />
    </>
  );
}

function DecisionModal({ request, approve, onClose }: { request: LeaveRequest; approve: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const decide = useMutation({
    mutationFn: () => (approve ? leaveApi.approve : leaveApi.reject)(request.id, note.trim() || null),
    onSuccess: () => {
      toast.success(approve ? 'Leave approved' : 'Leave rejected', request.employeeName);
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.approvals });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      qc.invalidateQueries({ queryKey: qk.attendance });
      onClose();
    },
    onError: (err) => toast.error(err),
  });

  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={approve ? 'Approve leave?' : 'Reject leave?'}
      description={`${request.employeeName} · ${request.leaveType} · ${formatDate(request.fromDate)}${
        request.toDate !== request.fromDate ? ` – ${formatDate(request.toDate)}` : ''
      } (${formatNumber(request.days)} day${request.days === 1 ? '' : 's'})`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant={approve ? 'success' : 'danger'} loading={decide.isPending} onClick={() => decide.mutate()}>
            {approve ? 'Approve' : 'Reject'}
          </Button>
        </>
      }
    >
      {request.reason && (
        <p className="mb-4 rounded-lg bg-zinc-50 p-3 text-sm text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-300">“{request.reason}”</p>
      )}
      {request.attachmentCount > 0 && <AttachmentList owner="Leave" ownerId={request.id} className="mb-4" />}
      <Field label="Note to employee" hint="Optional">
        <Textarea value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
    </Modal>
  );
}
