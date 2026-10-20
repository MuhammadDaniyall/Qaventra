import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarHeart, ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, EmptyState, ErrorState, Field, IconButton, Input, SkeletonRows, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatDate } from '../../lib/format';
import type { Holiday } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

const WEEKDAY = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function weekday(iso: string) {
  const [y, m, d] = iso.split('-').map(Number);
  return WEEKDAY[new Date(y, m - 1, d).getDay()];
}

export function HolidaysTab() {
  const [year, setYear] = useState(new Date().getFullYear());
  const list = useQuery({ queryKey: qk.holidays(year), queryFn: () => setupApi.holidays(year) });
  const [adding, setAdding] = useState(false);
  const [editing, setEditing] = useState<Holiday | null>(null);
  const [deleting, setDeleting] = useState<Holiday | null>(null);

  return (
    <Card>
      <CardHeader
        title={`Public holidays ${year}`}
        description="Holidays are skipped when counting leave days and absences."
        actions={
          <>
            <div className="flex items-center">
              <IconButton label="Previous year" onClick={() => setYear((y) => y - 1)}>
                <ChevronLeft className="size-4" />
              </IconButton>
              <span className="w-12 text-center text-sm font-medium tabular-nums">{year}</span>
              <IconButton label="Next year" onClick={() => setYear((y) => y + 1)}>
                <ChevronRight className="size-4" />
              </IconButton>
            </div>
            <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setAdding(true)}>
              Add holiday
            </Button>
          </>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={3} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={CalendarHeart}
          title={`No holidays for ${year}`}
          description="Add national holidays like Pakistan Day, Labour Day and Independence Day."
          action={<Button variant="secondary" onClick={() => setAdding(true)}>Add holiday</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Date</Th>
              <Th>Day</Th>
              <Th>Name</Th>
              <Th className="text-right">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((h) => (
              <Tr key={h.id}>
                <Td className="tabular-nums">{formatDate(h.date)}</Td>
                <Td>{weekday(h.date)}</Td>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{h.name}</Td>
                <Td>
                  <RowActions label={h.name} onEdit={() => setEditing(h)} onDelete={() => setDeleting(h)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {adding && <HolidayModal year={year} onClose={() => setAdding(false)} />}
      {editing && <HolidayModal year={year} item={editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name="Holiday"
        remove={setupApi.deleteHoliday}
        invalidate={[qk.holidaysAll, qk.dashboard]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function HolidayModal({ year, item, onClose }: { year: number; item?: Holiday; onClose: () => void }) {
  const [date, setDate] = useState(item?.date ?? `${year}-01-01`);
  const [name, setName] = useState(item?.name ?? '');
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: { date: string; name: string }) => (item ? setupApi.updateHoliday(item.id, b) : setupApi.createHoliday(b)),
    invalidate: [qk.holidaysAll, qk.dashboard],
    successMessage: item ? 'Holiday updated' : 'Holiday added',
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? 'Edit holiday' : 'Add holiday'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!date) fe.date = 'Pick a date.';
        if (!name.trim()) fe.name = 'Name is required.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate({ date, name: name.trim() });
      }}
    >
      <Field label="Date" required error={fieldErrors.date}>
        <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
      </Field>
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} placeholder="Pakistan Day" />
      </Field>
    </EditModal>
  );
}
