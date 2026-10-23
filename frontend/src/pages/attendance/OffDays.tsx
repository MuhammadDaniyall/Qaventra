import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { attendanceRulesApi } from '../../api/attendanceRules';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, Field, FormError, Input, Modal, StatusBadge, Table, TBody, Td, Textarea, Tr, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, todayIso } from '../../lib/format';
import { DAYS_OF_WEEK, type DayOfWeek } from '../../types';

const short = (days: DayOfWeek[]) => days.map((d) => d.slice(0, 3)).join(', ');

/** Ask to take different weekly days off for a period. */
export function OffDayModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [from, setFrom] = useState(todayIso());
  const [to, setTo] = useState(todayIso());
  const [days, setDays] = useState<DayOfWeek[]>([]);
  const [reason, setReason] = useState('');
  const save = useMutation({
    mutationFn: () => attendanceRulesApi.requestOffDays({ fromDate: from, toDate: to, offDays: days, reason: reason.trim() }),
    onSuccess: () => {
      toast.success('Sent for approval');
      qc.invalidateQueries({ queryKey: qk.offDays });
      qc.invalidateQueries({ queryKey: qk.attendance });
      onClose();
    },
  });
  const toggle = (d: DayOfWeek) => setDays((x) => (x.includes(d) ? x.filter((y) => y !== d) : [...x, d]));
  return (
    <Modal
      open
      onClose={onClose}
      title="Change my days off"
      description="Take different weekly days off for a period, e.g. Friday instead of Sunday. Your usual days become working days."
      footer={
        <Button loading={save.isPending} disabled={!days.length || !reason.trim()} onClick={() => save.mutate()}>
          Send
        </Button>
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="From">
          <Input type="date" min={todayIso()} value={from} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="To">
          <Input type="date" min={from} value={to} onChange={(e) => setTo(e.target.value)} />
        </Field>
      </div>
      <fieldset className="mt-4">
        <legend className="mb-2 text-sm font-medium text-zinc-700 dark:text-zinc-300">New days off</legend>
        <div className="flex flex-wrap gap-2">
          {DAYS_OF_WEEK.map((d) => (
            <button
              key={d}
              type="button"
              aria-pressed={days.includes(d)}
              onClick={() => toggle(d)}
              className={cn(
                'h-9 rounded-lg border px-3 text-sm font-medium',
                days.includes(d)
                  ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                  : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800',
              )}
            >
              {d.slice(0, 3)}
            </button>
          ))}
        </div>
      </fieldset>
      <div className="mt-4">
        <Field label="Why" required>
          <Textarea rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

/** The signed-in user's day-off change requests. */
export function MyOffDays() {
  const toast = useToast();
  const qc = useQueryClient();
  const mine = useQuery({ queryKey: qk.offDays, queryFn: attendanceRulesApi.myOffDays });
  const cancel = useMutation({
    mutationFn: attendanceRulesApi.cancelOffDays,
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.offDays }),
    onError: (e) => toast.error(e),
  });
  if (!mine.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Days off changes" />
      <Table>
        <TBody>
          {mine.data.map((x) => (
            <Tr key={x.id}>
              <Td className="whitespace-nowrap">
                {formatDate(x.fromDate)} – {formatDate(x.toDate)}
              </Td>
              <Td>
                {short(x.offDays)} <span className="text-xs text-zinc-500">instead of {short(x.usualOffDays)}</span>
              </Td>
              <Td className="text-sm">
                {x.reason}
                {x.decisionNote && <p className="text-xs text-zinc-500">“{x.decisionNote}”</p>}
              </Td>
              <Td>
                <StatusBadge status={x.status} />
              </Td>
              <Td className="text-right">
                {x.status === 'Pending' && (
                  <Button size="sm" variant="ghost" loading={cancel.isPending && cancel.variables === x.id} onClick={() => cancel.mutate(x.id)}>
                    Cancel
                  </Button>
                )}
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}
