import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarHeart, ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
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
  IconButton,
  Input,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { useLookups } from '../../api/employeeFile';
import { useEmployeeLookup } from '../../api/hooks';
import { formatDate } from '../../lib/format';
import type { Holiday, SaveHolidayRequest } from '../../types';
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
  const lookups = useLookups();
  const regionName = (id: string) => lookups.data?.find((l) => l.id === id)?.name ?? '';

  return (
    <Card>
      <CardHeader
        title={`Holidays ${year}`}
        description="Skipped when counting leave days and absences. A holiday can be limited to regions or a religion, and some people can be kept working."
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
          action={
            <Button variant="secondary" onClick={() => setAdding(true)}>
              Add holiday
            </Button>
          }
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
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                  {h.name}
                  {h.kind === 'Special' && <Badge className="ml-2">Special</Badge>}
                  {(h.regionIds.length > 0 || h.religion || h.exemptEmployeeIds.length > 0) && (
                    <span className="ml-2 text-xs font-normal text-zinc-500">
                      {[
                        h.regionIds.length ? h.regionIds.map((id) => regionName(id)).join(', ') : '',
                        h.religion ? `${h.religion} only` : '',
                        h.exemptEmployeeIds.length ? `${h.exemptEmployeeIds.length} working` : '',
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </span>
                  )}
                </Td>
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
  const lookups = useLookups();
  const people = useEmployeeLookup();
  const regions = (lookups.data ?? []).filter((l) => l.kind === 'Region');
  const [v, setV] = useState<SaveHolidayRequest>(
    item ? (({ id: _id, ...rest }) => rest)(item) : { date: `${year}-01-01`, name: '', kind: 'Public', regionIds: [], religion: null, exemptEmployeeIds: [] },
  );
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: SaveHolidayRequest) => (item ? setupApi.updateHoliday(item.id, b) : setupApi.createHoliday(b)),
    invalidate: [qk.holidaysAll, qk.dashboard, qk.attendance],
    successMessage: item ? 'Holiday updated' : 'Holiday added',
    onSuccess: onClose,
  });
  const toggle = (list: 'regionIds' | 'exemptEmployeeIds', id: string) =>
    setV((x) => ({ ...x, [list]: x[list].includes(id) ? x[list].filter((y) => y !== id) : [...x[list], id] }));
  return (
    <EditModal
      open
      title={item ? 'Edit holiday' : 'Add holiday'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!v.date) fe.date = 'Pick a date.';
        if (!v.name.trim()) fe.name = 'Name is required.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate({ ...v, name: v.name.trim(), religion: v.religion?.trim() || null });
      }}
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Date" required error={fieldErrors.date}>
          <Input type="date" value={v.date} onChange={(e) => setV({ ...v, date: e.target.value })} />
        </Field>
        <Field label="Type">
          <Select value={v.kind} onChange={(e) => setV({ ...v, kind: e.target.value as SaveHolidayRequest['kind'] })}>
            <option value="Public">Public / gazetted</option>
            <option value="Special">Special / company</option>
          </Select>
        </Field>
      </div>
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={150} onChange={(e) => setV({ ...v, name: e.target.value })} placeholder="Pakistan Day" />
      </Field>
      {regions.length > 0 && (
        <Field label="Only for regions" hint="None ticked: everywhere.">
          <div className="flex flex-wrap gap-x-4 gap-y-2">
            {regions.map((r) => (
              <Checkbox key={r.id} label={r.name} checked={v.regionIds.includes(r.id)} onChange={() => toggle('regionIds', r.id)} />
            ))}
          </div>
        </Field>
      )}
      <Field label="Only for religion" hint="e.g. Christian for Christmas, Hindu for Diwali. Empty: everyone.">
        <Input value={v.religion ?? ''} maxLength={100} onChange={(e) => setV({ ...v, religion: e.target.value || null })} />
      </Field>
      <Field label="People who work that day" hint="They are not given the holiday.">
        <div className="space-y-2">
          <div className="flex flex-wrap gap-1.5">
            {v.exemptEmployeeIds.map((id) => (
              <Badge key={id}>
                {people.data?.find((p) => p.id === id)?.name ?? '…'}
                <button type="button" className="ml-1" aria-label="Remove" onClick={() => toggle('exemptEmployeeIds', id)}>
                  ×
                </button>
              </Badge>
            ))}
          </div>
          <Select value="" aria-label="Add person" onChange={(e) => e.target.value && toggle('exemptEmployeeIds', e.target.value)}>
            <option value="">Add someone…</option>
            {(people.data ?? [])
              .filter((p) => !v.exemptEmployeeIds.includes(p.id))
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
          </Select>
        </div>
      </Field>
    </EditModal>
  );
}
