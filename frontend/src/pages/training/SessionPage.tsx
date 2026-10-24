import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { ClipboardCheck, Pencil, UserPlus, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { ENROLLMENT_LABELS, MODE_LABELS, trainingApi, type EnrollmentStatus, type SessionDetail } from '../../api/training';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Button,
  Card,
  CardHeader,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, formatDateTimeUtc } from '../../lib/format';
import { SessionModal } from './TrainingPage';

/** One session: details, who is booked, nominations, attendance and the participants' feedback. */
export function SessionPage() {
  const { id = '' } = useParams();
  const detail = useQuery({ queryKey: qk.trainingSession(id), queryFn: () => trainingApi.session(id) });
  if (detail.isLoading) return <SkeletonRows rows={6} cols={4} />;
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => detail.refetch()} />;
  return <SessionView d={detail.data!} />;
}

function SessionView({ d }: { d: SessionDetail }) {
  const s = d.session;
  const user = useUser();
  const toast = useToast();
  const qc = useQueryClient();
  const [nominating, setNominating] = useState(false);
  const [marking, setMarking] = useState(false);
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const hr = isHr(user);
  const upcoming = s.status === 'Scheduled' && new Date(/[zZ]$/.test(s.startsAtUtc) ? s.startsAtUtc : `${s.startsAtUtc}Z`).getTime() > Date.now();
  const started = !upcoming && s.status !== 'Cancelled';
  const cancel = useMutation({
    mutationFn: () => trainingApi.cancelSession(s.id),
    onSuccess: () => {
      toast.success('Session cancelled', 'Everyone booked has been told.');
      qc.invalidateQueries({ queryKey: qk.training });
      setCancelling(false);
    },
    onError: (e) => toast.error(e),
  });
  const withdraw = useMutation({
    mutationFn: trainingApi.withdraw,
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.training }),
    onError: (e) => toast.error(e),
  });
  const active = d.enrollments.filter((e) => e.status !== 'Withdrawn');
  const rated = active.filter((e) => e.feedbackRating != null);

  return (
    <>
      <PageHeader
        back={{ to: '/training?tab=sessions', label: 'Sessions' }}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {s.program} <StatusBadge status={s.status} />
          </span>
        }
        description={`${formatDateTimeUtc(s.startsAtUtc)} – ${formatDateTimeUtc(s.endsAtUtc)} · ${MODE_LABELS[s.mode]}${s.venue ? ` · ${s.venue}` : ''}`}
        actions={
          <div className="flex flex-wrap gap-2">
            {d.canNominate && upcoming && (
              <Button size="sm" icon={<UserPlus className="size-3.5" />} onClick={() => setNominating(true)}>
                Nominate
              </Button>
            )}
            {s.canRun && started && active.length > 0 && (
              <Button size="sm" icon={<ClipboardCheck className="size-3.5" />} onClick={() => setMarking(true)}>
                Mark attendance
              </Button>
            )}
            {hr && s.status === 'Scheduled' && (
              <>
                <Button size="sm" variant="ghost" icon={<Pencil className="size-3.5" />} onClick={() => setEditing(true)}>
                  Edit
                </Button>
                <Button size="sm" variant="ghost" icon={<X className="size-3.5" />} onClick={() => setCancelling(true)}>
                  Cancel session
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="grid gap-5 lg:grid-cols-[1fr_20rem]">
        <Card>
          <CardHeader title="Attendees" description={s.canRun ? undefined : 'You see yourself and your team.'} />
          {!active.length ? (
            <EmptyState icon={UserPlus} title="Nobody booked yet" />
          ) : (
            <Table>
              <THead>
                <Tr>
                  <Th>Employee</Th>
                  <Th>Status</Th>
                  <Th>Score</Th>
                  {s.canRun && <Th>Feedback</Th>}
                  <Th />
                </Tr>
              </THead>
              <TBody>
                {active.map((e) => (
                  <Tr key={e.id}>
                    <Td>
                      <p className="font-medium">{e.employeeName}</p>
                      <p className="text-xs text-zinc-500 dark:text-zinc-400">
                        {[e.employeeCode, e.department, e.nominatedByName ? `by ${e.nominatedByName}` : null].filter(Boolean).join(' · ')}
                      </p>
                    </Td>
                    <Td>
                      <StatusBadge status={e.status} label={ENROLLMENT_LABELS[e.status]} />
                      {e.completedOn && <p className="text-xs text-zinc-500 dark:text-zinc-400">{formatDate(e.completedOn)}</p>}
                    </Td>
                    <Td className="tabular-nums">{e.score != null ? `${e.score}%` : '—'}</Td>
                    {s.canRun && (
                      <Td className="text-sm">
                        {e.feedbackRating != null ? (
                          <>
                            ★ {e.feedbackRating}
                            {e.trainerRating != null && <span className="text-xs text-zinc-500 dark:text-zinc-400"> · trainer {e.trainerRating}</span>}
                            {e.feedbackComment && <p className="text-xs text-zinc-500">“{e.feedbackComment}”</p>}
                          </>
                        ) : (
                          '—'
                        )}
                      </Td>
                    )}
                    <Td className="text-end">
                      {upcoming && e.status === 'Enrolled' && (
                        <Button size="sm" variant="ghost" loading={withdraw.isPending && withdraw.variables === e.id} onClick={() => withdraw.mutate(e.id)}>
                          Withdraw
                        </Button>
                      )}
                    </Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
          )}
        </Card>
        <Card className="self-start">
          <CardHeader title="Details" />
          <dl className="space-y-2.5 px-5 py-4 text-sm">
            {(
              [
                ['Trainer', s.trainer ?? s.externalTrainer ?? '—'],
                ['Seats', s.capacity ? `${s.enrolled} of ${s.capacity}` : `${s.enrolled} booked`],
                ['Completed', String(s.completed)],
                ['Average rating', rated.length ? `${s.averageRating} from ${rated.length}` : '—'],
                ...(s.cost != null ? ([['Cost', formatCurrency(s.cost)]] as Array<[string, string]>) : []),
              ] as Array<[string, string]>
            ).map(([k, v]) => (
              <div key={k} className="flex justify-between gap-3">
                <dt className="text-zinc-500 dark:text-zinc-400">{k}</dt>
                <dd className="text-end">{v}</dd>
              </div>
            ))}
            {s.notes && <p className="border-t border-zinc-100 pt-2 whitespace-pre-line dark:border-zinc-800">{s.notes}</p>}
          </dl>
        </Card>
      </div>

      {nominating && <NominateModal d={d} onClose={() => setNominating(false)} />}
      {marking && <AttendanceModal d={d} onClose={() => setMarking(false)} />}
      {editing && <SessionModal session={s} onClose={() => setEditing(false)} />}
      <ConfirmDialog
        open={cancelling}
        title="Cancel this session?"
        message="Everyone booked will be told."
        confirmLabel="Cancel session"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setCancelling(false)}
      />
    </>
  );
}

function NominateModal({ d, onClose }: { d: SessionDetail; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useQuery({ queryKey: qk.trainingNominees, queryFn: trainingApi.nominees });
  const booked = new Set(d.enrollments.filter((e) => e.status !== 'Withdrawn').map((e) => e.employeeId));
  const [ids, setIds] = useState<string[]>([]);
  const [search, setSearch] = useState('');
  const left = d.session.capacity ? d.session.capacity - d.session.enrolled : null;
  const save = useMutation({
    mutationFn: () => trainingApi.enroll(d.session.id, ids),
    onSuccess: (r) => {
      toast.success(`${r.added} booked`, 'They have been told.');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
    },
  });
  const shown = people.data?.filter((p) => !booked.has(p.id) && p.name.toLowerCase().includes(search.toLowerCase())) ?? [];
  return (
    <Modal
      open
      onClose={onClose}
      title={`Nominate for ${d.session.program}`}
      description={left != null ? `${left} seats left` : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!ids.length || (left != null && ids.length > left)} onClick={() => save.mutate()}>
            Book {ids.length || ''}
          </Button>
        </>
      }
    >
      <Input className="mb-2" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search people" />
      <ul className="max-h-72 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
        {shown.map((p) => (
          <li key={p.id} className="px-3 py-1.5">
            <Checkbox
              label={p.name}
              checked={ids.includes(p.id)}
              onChange={(e) => setIds((x) => (e.target.checked ? [...x, p.id] : x.filter((y) => y !== p.id)))}
            />
          </li>
        ))}
        {!shown.length && <li className="px-3 py-4 text-sm text-zinc-500 dark:text-zinc-400">Nobody else to nominate.</li>}
      </ul>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function AttendanceModal({ d, onClose }: { d: SessionDetail; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const rows = d.enrollments.filter((e) => e.status !== 'Withdrawn');
  const [marks, setMarks] = useState(
    rows.map((e) => ({ enrollmentId: e.id, status: (e.status === 'Enrolled' ? 'Completed' : e.status) as EnrollmentStatus, score: e.score?.toString() ?? '' })),
  );
  const save = useMutation({
    mutationFn: () =>
      trainingApi.attendance(
        d.session.id,
        marks.map((m) => ({ enrollmentId: m.enrollmentId, status: m.status, score: m.score ? Number(m.score) : null })),
      ),
    onSuccess: () => {
      toast.success('Attendance saved', 'Attendees will be asked for feedback.');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Mark attendance"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {rows.map((e, i) => (
          <li key={e.id} className="grid grid-cols-[1fr_10rem_6rem] items-center gap-2 py-2">
            <span className="text-sm">{e.employeeName}</span>
            <Select
              aria-label="Result"
              value={marks[i].status}
              onChange={(ev) => setMarks((x) => x.map((m, j) => (j === i ? { ...m, status: ev.target.value as EnrollmentStatus } : m)))}
            >
              {(['Completed', 'Attended', 'Failed', 'NoShow'] as EnrollmentStatus[]).map((st) => (
                <option key={st} value={st}>
                  {ENROLLMENT_LABELS[st]}
                </option>
              ))}
            </Select>
            <Input
              aria-label="Score"
              type="number"
              min={0}
              max={100}
              placeholder="Score %"
              value={marks[i].score}
              onChange={(ev) => setMarks((x) => x.map((m, j) => (j === i ? { ...m, score: ev.target.value } : m)))}
            />
          </li>
        ))}
      </ul>
      <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">Completed counts towards required training. Attended means they came but there is nothing to pass.</p>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
