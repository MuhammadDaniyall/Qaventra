import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { DoorOpen, Plus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { separationsApi } from '../../api/separations';
import {
  Avatar,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
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
import { formatDate, todayIso } from '../../lib/format';
import type { SeparationSummary, SeparationType } from '../../types/workflows';
import { SEPARATION_TYPES, typeLabel } from './shared';

type TabValue = 'open' | 'Completed' | 'closed';

export function SeparationsPage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState<TabValue>('open');
  const [starting, setStarting] = useState(false);
  const list = useQuery({ queryKey: qk.separationList(''), queryFn: () => separationsApi.list('') });

  const all = list.data ?? [];
  const isOpen = (s: SeparationSummary) => s.status === 'Requested' || s.status === 'InProgress';
  const rows = all.filter((s) =>
    tab === 'open' ? isOpen(s) : tab === 'Completed' ? s.status === 'Completed' : s.status === 'Rejected' || s.status === 'Withdrawn',
  );
  const waiting = all.filter((s) => s.status === 'Requested').length;

  return (
    <>
      <PageHeader
        title="Separations"
        description={waiting ? `${waiting} resignation${waiting === 1 ? '' : 's'} waiting for your decision.` : 'Resignations, terminations, clearance and final settlements.'}
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setStarting(true)}>
            Start separation
          </Button>
        }
      />
      <Tabs<TabValue>
        className="mb-4"
        value={tab}
        onChange={setTab}
        items={[
          {
            value: 'open',
            label: (
              <>
                In progress
                {!!all.filter(isOpen).length && (
                  <span className="ml-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800 dark:bg-amber-500/20 dark:text-amber-200">
                    {all.filter(isOpen).length}
                  </span>
                )}
              </>
            ),
          },
          { value: 'Completed', label: 'Completed' },
          { value: 'closed', label: 'Withdrawn / declined' },
        ]}
      />
      <Card>
        {list.isLoading ? (
          <SkeletonRows rows={5} cols={6} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !rows.length ? (
          <EmptyState icon={DoorOpen} title={tab === 'open' ? 'Nobody is leaving right now' : 'Nothing here yet'} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Type</Th>
                <Th>Notice given</Th>
                <Th>Last working day</Th>
                <Th>Clearance</Th>
                <Th>Status</Th>
              </tr>
            </THead>
            <TBody>
              {rows.map((s) => {
                const pct = s.clearanceTotal ? Math.round((s.clearanceDone / s.clearanceTotal) * 100) : 0;
                return (
                  <Tr key={s.id} className="cursor-pointer" onClick={() => navigate(`/separations/${s.id}`)}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar name={s.employeeName} size="sm" />
                        <div>
                          <p className="font-medium text-zinc-900 dark:text-zinc-100">{s.employeeName}</p>
                          <p className="text-xs text-zinc-500">{[s.designation, s.department].filter(Boolean).join(' · ') || s.employeeCode}</p>
                        </div>
                      </div>
                    </Td>
                    <Td>{typeLabel(s.type)}</Td>
                    <Td>{formatDate(s.noticeDate)}</Td>
                    <Td>
                      {formatDate(s.lastWorkingDay ?? s.proposedLastDay)}
                      {!s.lastWorkingDay && <span className="ml-1 text-xs text-zinc-500">(proposed)</span>}
                    </Td>
                    <Td>
                      {s.clearanceTotal ? (
                        <div className="flex items-center gap-2">
                          <div className="h-1.5 w-20 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
                            <div className="h-full rounded-full bg-accent-500" style={{ width: `${pct}%` }} />
                          </div>
                          <span className="text-xs text-zinc-500 tabular-nums">
                            {s.clearanceDone}/{s.clearanceTotal}
                          </span>
                        </div>
                      ) : (
                        '—'
                      )}
                    </Td>
                    <Td>
                      <StatusBadge status={s.status} label={s.status === 'Requested' ? 'Needs decision' : undefined} />
                    </Td>
                  </Tr>
                );
              })}
            </TBody>
          </Table>
        )}
      </Card>
      {starting && <StartSeparationModal onClose={() => setStarting(false)} />}
    </>
  );
}

function StartSeparationModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const employees = useEmployeeLookup();
  const [employeeId, setEmployeeId] = useState('');
  const [type, setType] = useState<SeparationType>('Resignation');
  const [noticeDate, setNoticeDate] = useState(todayIso());
  const [lastDay, setLastDay] = useState('');
  const [notice, setNotice] = useState('30');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const start = useMutation({
    mutationFn: () =>
      separationsApi.initiate({
        employeeId,
        type,
        noticeDate,
        lastWorkingDay: lastDay,
        noticePeriodDays: Number(notice) || 0,
        reason: reason.trim() || null,
      }),
    onSuccess: (s) => {
      toast.success('Separation started', s.summary.employeeName);
      qc.invalidateQueries({ queryKey: qk.separations });
      navigate(`/separations/${s.summary.id}`);
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!employeeId) return setError('Choose the employee.');
    if (!lastDay) return setError('Choose the last working day.');
    if (lastDay < noticeDate) return setError('The last working day cannot be before the notice date.');
    start.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Start a separation"
      description="For terminations, retirements, contract ends, or a resignation received on paper."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="start-separation-form" loading={start.isPending}>
            Start
          </Button>
        </>
      }
    >
      <form id="start-separation-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <Field label="Employee" required>
          <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
            <option value="">Choose…</option>
            {employees.data?.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Type">
          <Select value={type} onChange={(e) => setType(e.target.value as SeparationType)}>
            {SEPARATION_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Notice date" required>
            <Input type="date" value={noticeDate} onChange={(e) => setNoticeDate(e.target.value)} />
          </Field>
          <Field label="Last working day" required>
            <Input type="date" value={lastDay} min={noticeDate} onChange={(e) => setLastDay(e.target.value)} />
          </Field>
        </div>
        <Field label="Notice period (days)">
          <Input type="number" min="0" max="365" value={notice} onChange={(e) => setNotice(e.target.value)} />
        </Field>
        <Field label="Reason">
          <Textarea value={reason} maxLength={2000} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </form>
    </Modal>
  );
}
