import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, ClipboardCheck, X } from 'lucide-react';
import { leaveApi } from '../../api/leave';
import { qk } from '../../api/keys';
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
import { formatDate, formatDateTimeUtc, formatNumber } from '../../lib/format';
import type { LeaveRequest, LeaveStatus } from '../../types';

type TabValue = Extract<LeaveStatus, 'Pending' | 'Approved' | 'Rejected'>;

export function LeaveApprovalsPage() {
  const [status, setStatus] = useState<TabValue>('Pending');
  const [deciding, setDeciding] = useState<{ request: LeaveRequest; approve: boolean } | null>(null);
  const list = useQuery({ queryKey: qk.leaveApprovals(status), queryFn: () => leaveApi.approvals(status) });
  const pendingCount = useQuery({ queryKey: qk.leaveApprovals('Pending'), queryFn: () => leaveApi.approvals('Pending') });

  return (
    <>
      <PageHeader title="Leave approvals" description="Review leave requests from people who report to you." />
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
                  <span className="ml-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800 dark:bg-amber-500/20 dark:text-amber-200">
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
                <Th className="text-right">Days</Th>
                <Th>Reason</Th>
                <Th>{status === 'Pending' ? 'Requested' : 'Decided'}</Th>
                {status === 'Pending' ? <Th className="text-right">Decision</Th> : <Th>Note</Th>}
              </tr>
            </THead>
            <TBody>
              {list.data.map((r) => (
                <Tr key={r.id}>
                  <Td>
                    <div className="flex items-center gap-3">
                      <Avatar name={r.employeeName} size="sm" />
                      <div>
                        <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.employeeName}</p>
                        <p className="font-mono text-xs text-zinc-500">{r.employeeCode}</p>
                      </div>
                    </div>
                  </Td>
                  <Td>{r.leaveType}</Td>
                  <Td>
                    {formatDate(r.fromDate)}
                    {r.toDate !== r.fromDate && <> – {formatDate(r.toDate)}</>}
                  </Td>
                  <Td className="text-right tabular-nums">{formatNumber(r.days)}</Td>
                  <Td className="max-w-xs truncate" title={r.reason ?? undefined}>{r.reason ?? '—'}</Td>
                  <Td>{formatDateTimeUtc(status === 'Pending' ? r.createdAtUtc : r.decidedAtUtc)}</Td>
                  {status === 'Pending' ? (
                    <Td className="text-right">
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
                      <span className="mr-2"><StatusBadge status={r.status} /></span>
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
      <Field label="Note to employee" hint="Optional">
        <Textarea value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
    </Modal>
  );
}
