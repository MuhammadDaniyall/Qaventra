import { useState } from 'react';
import { Clock, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
import { useShifts } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  Input,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatTime, timeForInput, timeFromInput } from '../../lib/format';
import { DAYS_OF_WEEK, type DayOfWeek, type SaveShiftRequest, type Shift } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function ShiftsTab() {
  const list = useShifts();
  const [editing, setEditing] = useState<Shift | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Shift | null>(null);

  return (
    <Card>
      <CardHeader
        title="Shifts"
        description="Working hours decide when a check-in counts as late. Overnight shifts are not supported yet."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add shift
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={Clock}
          title="No shifts yet"
          action={
            <Button variant="secondary" onClick={() => setEditing('new')}>
              Add shift
            </Button>
          }
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Name</Th>
              <Th>Hours</Th>
              <Th className="text-right">Grace</Th>
              <Th>Weekly off</Th>
              <Th className="text-right">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((s) => (
              <Tr key={s.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                  <span className="inline-flex items-center gap-2">
                    {s.name}
                    {s.isDefault && <Badge tone="accent">Default</Badge>}
                  </span>
                </Td>
                <Td className="tabular-nums">
                  {formatTime(s.startTime)} – {formatTime(s.endTime)}
                </Td>
                <Td className="text-right tabular-nums">{s.graceMinutes} min</Td>
                <Td>{s.weeklyOffDays.length ? s.weeklyOffDays.map((d) => d.slice(0, 3)).join(', ') : 'None'}</Td>
                <Td>
                  <RowActions history={{ type: 'Shift', id: s.id }} label={s.name} onEdit={() => setEditing(s)} onDelete={s.isDefault ? undefined : () => setDeleting(s)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <ShiftModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Shift" remove={setupApi.deleteShift} invalidate={[qk.shifts]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function ShiftModal({ item, onClose }: { item: Shift | null; onClose: () => void }) {
  const [v, setV] = useState<SaveShiftRequest>(
    item
      ? (({ id: _id, ...rest }) => rest)(item)
      : {
          name: '',
          startTime: '09:00:00',
          endTime: '18:00:00',
          graceMinutes: 15,
          weeklyOffDays: ['Saturday', 'Sunday'],
          isDefault: false,
          earlyLeaveGraceMinutes: 15,
          halfDayMinutes: null,
          shortDayMinutes: null,
          earliestCheckInMinutes: null,
          breakMinutes: 0,
        },
  );
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: SaveShiftRequest) => (item ? setupApi.updateShift(item.id, b) : setupApi.createShift(b)),
    invalidate: [qk.shifts, qk.attendanceToday],
    successMessage: item ? 'Shift updated' : 'Shift added',
    onSuccess: onClose,
  });

  const toggleDay = (d: DayOfWeek) =>
    setV((s) => ({ ...s, weeklyOffDays: s.weeklyOffDays.includes(d) ? s.weeklyOffDays.filter((x) => x !== d) : [...s.weeklyOffDays, d] }));

  return (
    <EditModal
      open
      title={item ? 'Edit shift' : 'Add shift'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!v.name.trim()) fe.name = 'Name is required.';
        if (v.endTime <= v.startTime) fe.endTime = 'End time must be after the start time.';
        if (v.graceMinutes < 0 || v.graceMinutes > 180) fe.graceMinutes = 'Between 0 and 180 minutes.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate({ ...v, name: v.name.trim() });
      }}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={100} onChange={(e) => setV({ ...v, name: e.target.value })} placeholder="General" />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Start time" required error={fieldErrors.startTime}>
          <Input type="time" value={timeForInput(v.startTime)} onChange={(e) => setV({ ...v, startTime: timeFromInput(e.target.value) })} />
        </Field>
        <Field label="End time" required error={fieldErrors.endTime}>
          <Input type="time" value={timeForInput(v.endTime)} onChange={(e) => setV({ ...v, endTime: timeFromInput(e.target.value) })} />
        </Field>
      </div>
      <Field label="Grace period (minutes)" hint="Check-ins within this many minutes of the start time are not late." error={fieldErrors.graceMinutes}>
        <Input type="number" min={0} max={180} value={v.graceMinutes} onChange={(e) => setV({ ...v, graceMinutes: Number(e.target.value) || 0 })} />
      </Field>
      <details className="rounded-lg border border-zinc-200 px-3 py-2 dark:border-zinc-800">
        <summary className="cursor-pointer text-sm font-medium text-zinc-700 dark:text-zinc-300">Flag rules (early leave, short and half days, break)</summary>
        <div className="mt-3 grid grid-cols-2 gap-3">
          <Field label="Early-leave grace (minutes)" hint="Leaving earlier than this before the end is an early leave.">
            <Input
              type="number"
              min={0}
              max={240}
              value={v.earlyLeaveGraceMinutes}
              onChange={(e) => setV({ ...v, earlyLeaveGraceMinutes: Number(e.target.value) || 0 })}
            />
          </Field>
          <Field label="Break (minutes)" hint="Unpaid break inside the shift.">
            <Input type="number" min={0} max={240} value={v.breakMinutes} onChange={(e) => setV({ ...v, breakMinutes: Number(e.target.value) || 0 })} />
          </Field>
          <Field label="Half day under (hours)" hint="Empty: under half the shift.">
            <Input
              type="number"
              min={0}
              max={24}
              step={0.25}
              value={v.halfDayMinutes != null ? v.halfDayMinutes / 60 : ''}
              onChange={(e) => setV({ ...v, halfDayMinutes: e.target.value ? Math.round(Number(e.target.value) * 60) : null })}
            />
          </Field>
          <Field label="Short day under (hours)" hint="Empty: no short days.">
            <Input
              type="number"
              min={0}
              max={24}
              step={0.25}
              value={v.shortDayMinutes != null ? v.shortDayMinutes / 60 : ''}
              onChange={(e) => setV({ ...v, shortDayMinutes: e.target.value ? Math.round(Number(e.target.value) * 60) : null })}
            />
          </Field>
          <Field label="Check-in opens (minutes before start)" hint="Empty: any time.">
            <Input
              type="number"
              min={0}
              max={720}
              value={v.earliestCheckInMinutes ?? ''}
              onChange={(e) => setV({ ...v, earliestCheckInMinutes: e.target.value ? Number(e.target.value) : null })}
            />
          </Field>
        </div>
      </details>
      <details className="rounded-lg border border-zinc-200 px-3 py-2 dark:border-zinc-800">
        <summary className="cursor-pointer text-sm font-medium text-zinc-700 dark:text-zinc-300">Flag rules (early leave, short and half days, break)</summary>
        <div className="mt-3 grid grid-cols-2 gap-3">
          <Field label="Early-leave grace (minutes)" hint="Leaving earlier than this before the end is an early leave.">
            <Input
              type="number"
              min={0}
              max={240}
              value={v.earlyLeaveGraceMinutes}
              onChange={(e) => setV({ ...v, earlyLeaveGraceMinutes: Number(e.target.value) || 0 })}
            />
          </Field>
          <Field label="Break (minutes)" hint="Unpaid break inside the shift.">
            <Input type="number" min={0} max={240} value={v.breakMinutes} onChange={(e) => setV({ ...v, breakMinutes: Number(e.target.value) || 0 })} />
          </Field>
          <Field label="Half day under (hours)" hint="Empty: under half the shift.">
            <Input
              type="number"
              min={0}
              max={24}
              step={0.25}
              value={v.halfDayMinutes != null ? v.halfDayMinutes / 60 : ''}
              onChange={(e) => setV({ ...v, halfDayMinutes: e.target.value ? Math.round(Number(e.target.value) * 60) : null })}
            />
          </Field>
          <Field label="Short day under (hours)" hint="Empty: no short days.">
            <Input
              type="number"
              min={0}
              max={24}
              step={0.25}
              value={v.shortDayMinutes != null ? v.shortDayMinutes / 60 : ''}
              onChange={(e) => setV({ ...v, shortDayMinutes: e.target.value ? Math.round(Number(e.target.value) * 60) : null })}
            />
          </Field>
          <Field label="Check-in opens (minutes before start)" hint="Empty: any time.">
            <Input
              type="number"
              min={0}
              max={720}
              value={v.earliestCheckInMinutes ?? ''}
              onChange={(e) => setV({ ...v, earliestCheckInMinutes: e.target.value ? Number(e.target.value) : null })}
            />
          </Field>
        </div>
      </details>
      <fieldset>
        <legend className="mb-2 text-sm font-medium text-zinc-700 dark:text-zinc-300">Weekly off days</legend>
        <div className="flex flex-wrap gap-2">
          {DAYS_OF_WEEK.map((d) => {
            const on = v.weeklyOffDays.includes(d);
            return (
              <button
                key={d}
                type="button"
                aria-pressed={on}
                onClick={() => toggleDay(d)}
                className={cn(
                  'h-9 rounded-lg border px-3 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-accent-500',
                  on
                    ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                    : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800',
                )}
              >
                {d.slice(0, 3)}
              </button>
            );
          })}
        </div>
      </fieldset>
      <Checkbox
        label="Default shift"
        description="Used for employees without a specific shift."
        checked={v.isDefault}
        disabled={item?.isDefault}
        onChange={(e) => setV({ ...v, isDefault: e.target.checked })}
      />
    </EditModal>
  );
}
