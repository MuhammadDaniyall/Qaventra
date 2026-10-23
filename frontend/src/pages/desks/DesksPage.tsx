import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { Armchair, Building, CalendarCheck, DoorOpen, LayoutGrid, Monitor, Plus, Users } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { desksApi, KIND_LABELS, type Floor, type MyBooking, type Space, type SpaceDay, type SpaceKind } from '../../api/desks';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { setupApi } from '../../api/organization';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
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
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, todayIso } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';

type Tab = 'book' | 'mine' | 'who' | 'setup' | 'usage';

const time = (t: string | null) => (t ? t.slice(0, 5) : '');

/** Book a desk for the day or a room by the hour; see who is in; HR sets up floors and spaces. */
export function DesksPage() {
  const hr = isHr(useUser());
  const [params, setParams] = useSearchParams();
  const allowed: Tab[] = ['book', 'mine', 'who', ...(hr ? (['setup', 'usage'] as Tab[]) : [])];
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && allowed.includes(raw) ? raw : 'book';
  const labels: Record<Tab, string> = { book: 'Book', mine: 'My bookings', who: 'Who is in', setup: 'Floors and spaces', usage: 'Usage' };
  return (
    <>
      <PageHeader title="Desks and rooms" description="Book a desk for the day or a meeting room by the hour." />
      <Tabs<Tab> className="mb-5" value={tab} onChange={(t) => setParams({ tab: t })} items={allowed.map((t) => ({ value: t, label: labels[t] }))} />
      {tab === 'book' && <BookTab hr={hr} />}
      {tab === 'mine' && <MineTab />}
      {tab === 'who' && <WhoTab />}
      {tab === 'setup' && <SetupTab />}
      {tab === 'usage' && <UsageTab />}
    </>
  );
}

// ---------- Book ----------

function BookTab({ hr }: { hr: boolean }) {
  const floors = useQuery({ queryKey: qk.floors, queryFn: desksApi.floors });
  const settings = useQuery({ queryKey: qk.deskSettings, queryFn: desksApi.settings });
  const [floorId, setFloorId] = useState('');
  const [date, setDate] = useState(todayIso());
  const [booking, setBooking] = useState<SpaceDay | null>(null);
  useEffect(() => {
    if (!floorId && floors.data?.length) setFloorId(floors.data[0].id);
  }, [floors.data, floorId]);
  const day = useQuery({ queryKey: qk.floorDay(floorId, date), queryFn: () => desksApi.day(floorId, date), enabled: !!floorId });
  const max = settings.data ? new Date(Date.parse(todayIso()) + settings.data.maxDaysAhead * 86_400_000).toISOString().slice(0, 10) : undefined;
  if (floors.isLoading) return <SkeletonRows rows={4} cols={4} />;
  if (!floors.data?.length)
    return (
      <Card>
        <EmptyState
          icon={Building}
          title="No floors set up yet"
          description={hr ? 'Add floors, desks and rooms under Floors and spaces.' : 'HR has not set up desks yet.'}
        />
      </Card>
    );
  const d = day.data;
  const desks = d?.spaces.filter((s) => s.kind === 'Desk') ?? [];
  const rooms = d?.spaces.filter((s) => s.kind !== 'Desk') ?? [];
  return (
    <div className="space-y-5">
      <Card className="flex flex-wrap items-end gap-3 px-5 py-4">
        <Field label="Floor">
          <Select className="w-64" value={floorId} onChange={(e) => setFloorId(e.target.value)}>
            {floors.data.map((f) => (
              <option key={f.id} value={f.id}>
                {f.office} · {f.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Day">
          <Input type="date" min={todayIso()} max={max} value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        {d && (
          <p className="pb-2 text-sm text-zinc-500">
            {d.desksFree} of {d.desksTotal} desks free
          </p>
        )}
      </Card>
      {day.isLoading ? (
        <SkeletonRows rows={4} cols={6} />
      ) : day.isError ? (
        <ErrorState error={day.error} onRetry={() => day.refetch()} />
      ) : (
        <>
          <Card>
            <CardHeader
              title="Desks"
              actions={
                <div className="flex gap-3 text-xs text-zinc-500">
                  <Legend className="border-emerald-300 bg-emerald-50 dark:bg-emerald-500/10" label="Free" />
                  <Legend className="border-accent-400 bg-accent-100 dark:bg-accent-500/20" label="Yours" />
                  <Legend className="border-zinc-200 bg-zinc-100 dark:bg-zinc-800" label="Taken" />
                </div>
              }
            />
            {!desks.length ? (
              <p className="px-5 py-6 text-sm text-zinc-500">No desks on this floor.</p>
            ) : (
              <div className="grid grid-cols-2 gap-2 p-4 sm:grid-cols-4 lg:grid-cols-6">
                {desks.map((s) => {
                  const b = s.bookings[0];
                  const mine = b?.mine;
                  return (
                    <button
                      key={s.id}
                      type="button"
                      disabled={!s.free}
                      onClick={() => setBooking(s)}
                      title={s.features ?? undefined}
                      className={cn(
                        'rounded-lg border p-3 text-left text-sm transition',
                        s.free && 'border-emerald-300 bg-emerald-50 hover:border-emerald-500 dark:bg-emerald-500/10',
                        mine && 'border-accent-400 bg-accent-100 dark:bg-accent-500/20',
                        !s.free && !mine && 'cursor-default border-zinc-200 bg-zinc-100 dark:border-zinc-700 dark:bg-zinc-800',
                      )}
                    >
                      <span className="flex items-center gap-1.5 font-medium text-zinc-900 dark:text-white">
                        <Monitor className="size-3.5 text-zinc-400" />
                        {s.name}
                      </span>
                      <span className="mt-0.5 block truncate text-xs text-zinc-500">
                        {s.assignedTo ? `${s.assignedTo} (permanent)` : b ? (mine ? 'You' : b.employeeName) : (s.features ?? 'Free')}
                      </span>
                    </button>
                  );
                })}
              </div>
            )}
          </Card>
          <Card>
            <CardHeader title="Rooms" />
            {!rooms.length ? (
              <p className="px-5 py-6 text-sm text-zinc-500">No rooms on this floor.</p>
            ) : (
              <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {rooms.map((s) => (
                  <li key={s.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                    <DoorOpen className="size-4 text-zinc-400" />
                    <div className="min-w-0 flex-1">
                      <p className="font-medium text-zinc-900 dark:text-white">
                        {s.name}{' '}
                        <span className="text-xs font-normal text-zinc-500">
                          · {KIND_LABELS[s.kind]}, {s.seats} seats{s.features ? ` · ${s.features}` : ''}
                        </span>
                      </p>
                      <div className="mt-1 flex flex-wrap gap-1">
                        {s.bookings.length ? (
                          s.bookings.map((b) => (
                            <Badge key={b.bookingId} tone={b.mine ? 'accent' : 'gray'}>
                              {time(b.from)}–{time(b.to)} {b.title ?? b.employeeName}
                            </Badge>
                          ))
                        ) : (
                          <span className="text-xs text-emerald-600">Free all day</span>
                        )}
                      </div>
                    </div>
                    {!s.assignedEmployeeId && (
                      <Button size="sm" variant="secondary" onClick={() => setBooking(s)}>
                        Book
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </>
      )}
      {booking && <BookModal space={booking} date={date} onClose={() => setBooking(null)} />}
    </div>
  );
}

function Legend({ className, label }: { className: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1">
      <span className={cn('size-3 rounded border', className)} />
      {label}
    </span>
  );
}

function BookModal({ space, date, onClose }: { space: SpaceDay; date: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const desk = space.kind === 'Desk';
  const [from, setFrom] = useState('10:00');
  const [to, setTo] = useState('11:00');
  const [title, setTitle] = useState('');
  const [attendees, setAttendees] = useState('2');
  const book = useMutation({
    mutationFn: () =>
      desksApi.book({
        spaceId: space.id,
        date,
        from: desk ? null : from,
        to: desk ? null : to,
        title: desk ? null : title.trim() || null,
        attendees: desk ? 1 : Number(attendees) || 1,
      }),
    onSuccess: () => {
      toast.success(`${space.name} booked`, formatDate(date));
      qc.invalidateQueries({ queryKey: qk.desks });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={`Book ${space.name}`}
      description={`${formatDate(date)}${space.features ? ` · ${space.features}` : ''}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={book.isPending} onClick={() => book.mutate()}>
            Book
          </Button>
        </>
      }
    >
      {desk ? (
        <p className="text-sm text-zinc-600 dark:text-zinc-300">The desk is yours for the whole day.</p>
      ) : (
        <div className="grid grid-cols-2 gap-3">
          <Field label="From">
            <Input type="time" step={900} value={from} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field label="To">
            <Input type="time" step={900} value={to} onChange={(e) => setTo(e.target.value)} />
          </Field>
          <Field label="Meeting" className="col-span-2">
            <Input value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} placeholder="Sprint review" />
          </Field>
          <Field label="People" hint={`Seats ${space.seats}`}>
            <Input type="number" min={1} max={space.seats} value={attendees} onChange={(e) => setAttendees(e.target.value)} />
          </Field>
        </div>
      )}
      <FormError message={book.isError ? errorMessage(book.error) : null} />
    </Modal>
  );
}

// ---------- Mine ----------

function MineTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const my = useQuery({ queryKey: qk.myBookings, queryFn: desksApi.my });
  const cancel = useMutation({ mutationFn: desksApi.cancel, onSuccess: () => qc.invalidateQueries({ queryKey: qk.desks }), onError: (e) => toast.error(e) });
  const checkIn = useMutation({
    mutationFn: desksApi.checkIn,
    onSuccess: () => {
      toast.success('Checked in');
      qc.invalidateQueries({ queryKey: qk.desks });
    },
    onError: (e) => toast.error(e),
  });
  const today = todayIso();
  const upcoming = my.data?.filter((b) => b.date >= today && (b.status === 'Booked' || b.status === 'CheckedIn')) ?? [];
  const past = my.data?.filter((b) => !upcoming.includes(b)).reverse() ?? [];
  const row = (b: MyBooking, actions: boolean) => (
    <Tr key={b.id}>
      <Td className="whitespace-nowrap">{formatDate(b.date)}</Td>
      <Td>
        <p className="font-medium">
          {b.space} <span className="text-xs font-normal text-zinc-500">{KIND_LABELS[b.kind]}</span>
        </p>
        <p className="text-xs text-zinc-500">
          {b.office} · {b.floor}
          {b.title ? ` · ${b.title}` : ''}
        </p>
      </Td>
      <Td className="text-sm">{b.from ? `${time(b.from)}–${time(b.to)}` : 'All day'}</Td>
      <Td>
        <StatusBadge status={b.status} label={b.status === 'CheckedIn' ? 'Checked in' : b.status === 'Released' ? 'Released (no check-in)' : undefined} />
      </Td>
      <Td className="text-right">
        {actions && (
          <div className="flex justify-end gap-2">
            {b.canCheckIn && (
              <Button size="sm" loading={checkIn.isPending && checkIn.variables === b.id} onClick={() => checkIn.mutate(b.id)}>
                Check in
              </Button>
            )}
            <Button size="sm" variant="ghost" loading={cancel.isPending && cancel.variables === b.id} onClick={() => cancel.mutate(b.id)}>
              Cancel
            </Button>
          </div>
        )}
      </Td>
    </Tr>
  );
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Coming up" />
        {my.isLoading ? (
          <SkeletonRows rows={3} cols={4} />
        ) : !upcoming.length ? (
          <EmptyState icon={CalendarCheck} title="No bookings" description="Pick a desk or room on the Book tab." />
        ) : (
          <Table>
            <TBody>{upcoming.map((b) => row(b, true))}</TBody>
          </Table>
        )}
      </Card>
      {past.length > 0 && (
        <Card>
          <CardHeader title="Earlier" />
          <Table>
            <TBody>{past.map((b) => row(b, false))}</TBody>
          </Table>
        </Card>
      )}
    </div>
  );
}

// ---------- Who is in ----------

function WhoTab() {
  const [date, setDate] = useState(todayIso());
  const who = useQuery({ queryKey: qk.inOffice(date), queryFn: () => desksApi.inOffice(date) });
  return (
    <Card>
      <CardHeader title="Who is in the office" actions={<Input type="date" className="w-44" value={date} onChange={(e) => setDate(e.target.value)} />} />
      {who.isLoading ? (
        <SkeletonRows rows={4} cols={3} />
      ) : !who.data?.length ? (
        <EmptyState icon={Users} title="Nobody has a desk that day" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Name</Th>
              <Th>Department</Th>
              <Th>Desk</Th>
            </Tr>
          </THead>
          <TBody>
            {who.data.map((p) => (
              <Tr key={p.employeeId}>
                <Td className="font-medium">{p.name}</Td>
                <Td className="text-sm">{p.department ?? '—'}</Td>
                <Td className="text-sm">
                  {p.space} <span className="text-xs text-zinc-500">· {p.floor}</span>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}

// ---------- Setup (HR) ----------

function SetupTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const floors = useQuery({ queryKey: qk.floors, queryFn: desksApi.floors });
  const offices = useQuery({ queryKey: qk.offices, queryFn: setupApi.offices });
  const settings = useQuery({ queryKey: qk.deskSettings, queryFn: desksApi.settings });
  const [floor, setFloor] = useState<Floor | null>(null);
  const [editingFloor, setEditingFloor] = useState<Floor | 'new' | null>(null);
  const [deletingFloor, setDeletingFloor] = useState<Floor | null>(null);
  const [s, setS] = useState<{ days: string; check: boolean; release: string; start: string } | null>(null);
  const f =
    s ??
    (settings.data
      ? {
          days: String(settings.data.maxDaysAhead),
          check: settings.data.requireCheckIn,
          release: String(settings.data.releaseAfterMinutes),
          start: String(settings.data.dayStartsAt),
        }
      : null);
  const saveSettings = useMutation({
    mutationFn: () =>
      desksApi.saveSettings({
        maxDaysAhead: Number(f!.days),
        requireCheckIn: f!.check,
        releaseAfterMinutes: Number(f!.release),
        dayStartsAt: Number(f!.start),
      }),
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: qk.deskSettings });
      setS(null);
    },
    onError: (e) => toast.error(e),
  });
  const current = floor ?? floors.data?.[0] ?? null;
  return (
    <div className="grid gap-5 lg:grid-cols-[20rem_1fr]">
      <div className="space-y-5">
        <Card>
          <CardHeader
            title="Floors"
            actions={
              <Button
                size="sm"
                variant="secondary"
                icon={<Plus className="size-3.5" />}
                onClick={() => setEditingFloor('new')}
                disabled={!offices.data?.length}
              >
                Add
              </Button>
            }
          />
          {!offices.data?.length ? (
            <p className="px-5 py-4 text-sm text-zinc-500">Add an office location under Organization first.</p>
          ) : !floors.data?.length ? (
            <p className="px-5 py-4 text-sm text-zinc-500">No floors yet.</p>
          ) : (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {floors.data.map((x) => (
                <li key={x.id} className={cn('flex items-center gap-2 px-5 py-2.5', current?.id === x.id && 'bg-accent-50 dark:bg-accent-500/10')}>
                  <button type="button" className="min-w-0 flex-1 text-left" onClick={() => setFloor(x)}>
                    <p className="font-medium">{x.name}</p>
                    <p className="text-xs text-zinc-500">
                      {x.office} · {x.desks} desks, {x.rooms} rooms
                    </p>
                  </button>
                  <RowActions label={x.name} onEdit={() => setEditingFloor(x)} onDelete={x.desks + x.rooms ? undefined : () => setDeletingFloor(x)} />
                </li>
              ))}
            </ul>
          )}
        </Card>
        {f && (
          <Card>
            <CardHeader
              title="Rules"
              actions={
                <Button size="sm" loading={saveSettings.isPending} onClick={() => saveSettings.mutate()}>
                  Save
                </Button>
              }
            />
            <div className="space-y-3 px-5 py-4">
              <Field label="Book up to (days ahead)">
                <Input type="number" min={0} max={90} value={f.days} onChange={(e) => setS({ ...f, days: e.target.value })} />
              </Field>
              <Checkbox
                label="People must check in"
                description="Bookings nobody checks into are freed"
                checked={f.check}
                onChange={(e) => setS({ ...f, check: e.target.checked })}
              />
              {f.check && (
                <div className="grid grid-cols-2 gap-2">
                  <Field label="Free after (minutes)">
                    <Input type="number" min={10} max={480} value={f.release} onChange={(e) => setS({ ...f, release: e.target.value })} />
                  </Field>
                  <Field label="Day starts at (hour)">
                    <Input type="number" min={0} max={23} value={f.start} onChange={(e) => setS({ ...f, start: e.target.value })} />
                  </Field>
                </div>
              )}
            </div>
          </Card>
        )}
      </div>
      {current ? (
        <SpacesCard floor={current} />
      ) : (
        <Card>
          <EmptyState icon={LayoutGrid} title="Add a floor to set up desks and rooms" />
        </Card>
      )}
      {editingFloor && <FloorModal item={editingFloor === 'new' ? null : editingFloor} onClose={() => setEditingFloor(null)} />}
      <DeleteConfirm
        item={deletingFloor}
        name={deletingFloor?.name ?? ''}
        remove={desksApi.deleteFloor}
        invalidate={[qk.floors]}
        onClose={() => setDeletingFloor(null)}
      />
    </div>
  );
}

function FloorModal({ item, onClose }: { item: Floor | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const offices = useQuery({ queryKey: qk.offices, queryFn: setupApi.offices });
  const [office, setOffice] = useState(item?.officeLocationId ?? offices.data?.[0]?.id ?? '');
  const [name, setName] = useState(item?.name ?? '');
  const [order, setOrder] = useState(String(item?.order ?? 0));
  const save = useMutation({
    mutationFn: () => {
      const b = { officeLocationId: office, name: name.trim(), order: Number(order) || 0 };
      return item ? desksApi.updateFloor(item.id, b) : desksApi.addFloor(b);
    },
    onSuccess: () => {
      toast.success('Floor saved');
      qc.invalidateQueries({ queryKey: qk.desks });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={item ? 'Edit floor' : 'New floor'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!name.trim() || !office} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Office">
          <Select value={office} onChange={(e) => setOffice(e.target.value)}>
            {offices.data?.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Name" required>
          <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Ground floor" />
        </Field>
        <Field label="Order">
          <Input type="number" value={order} onChange={(e) => setOrder(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function SpacesCard({ floor }: { floor: Floor }) {
  const toast = useToast();
  const qc = useQueryClient();
  const spaces = useQuery({ queryKey: qk.floorSpaces(floor.id), queryFn: () => desksApi.spaces(floor.id) });
  const [editing, setEditing] = useState<Space | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Space | null>(null);
  const [bulk, setBulk] = useState({ prefix: 'A-', from: '1', to: '10', features: '' });
  const addBulk = useMutation({
    mutationFn: () =>
      desksApi.bulk({ floorId: floor.id, prefix: bulk.prefix, from: Number(bulk.from), to: Number(bulk.to), features: bulk.features.trim() || null }),
    onSuccess: (r) => {
      toast.success(`${r.added} desks added`, r.skipped ? `${r.skipped} already existed` : undefined);
      qc.invalidateQueries({ queryKey: qk.desks });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader
        title={`${floor.office} · ${floor.name}`}
        actions={
          <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            Add desk or room
          </Button>
        }
      />
      <div className="flex flex-wrap items-end gap-2 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <Field label="Add numbered desks">
          <Input className="w-20" value={bulk.prefix} maxLength={20} onChange={(e) => setBulk({ ...bulk, prefix: e.target.value })} />
        </Field>
        <Field label="From">
          <Input className="w-20" type="number" min={0} value={bulk.from} onChange={(e) => setBulk({ ...bulk, from: e.target.value })} />
        </Field>
        <Field label="To">
          <Input className="w-20" type="number" min={0} value={bulk.to} onChange={(e) => setBulk({ ...bulk, to: e.target.value })} />
        </Field>
        <Field label="Features">
          <Input
            className="w-48"
            value={bulk.features}
            maxLength={300}
            onChange={(e) => setBulk({ ...bulk, features: e.target.value })}
            placeholder="Monitor"
          />
        </Field>
        <Button variant="secondary" loading={addBulk.isPending} onClick={() => addBulk.mutate()}>
          Add
        </Button>
      </div>
      {spaces.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : !spaces.data?.length ? (
        <EmptyState icon={Armchair} title="No desks or rooms yet" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Name</Th>
              <Th>Type</Th>
              <Th>Features</Th>
              <Th>Permanent for</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {spaces.data.map((s) => (
              <Tr key={s.id}>
                <Td className="font-medium">
                  {s.name} {!s.isActive && <Badge>Off</Badge>}
                </Td>
                <Td className="text-sm">
                  {KIND_LABELS[s.kind]}
                  {s.kind !== 'Desk' && <span className="text-zinc-500"> · {s.seats} seats</span>}
                </Td>
                <Td className="text-sm">{s.features ?? '—'}</Td>
                <Td className="text-sm">{s.assignedTo ?? '—'}</Td>
                <Td>
                  <RowActions label={s.name} onEdit={() => setEditing(s)} onDelete={() => setDeleting(s)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <SpaceModal floorId={floor.id} item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name={deleting?.name ?? ''} remove={desksApi.deleteSpace} invalidate={[qk.desks]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function SpaceModal({ floorId, item, onClose }: { floorId: string; item: Space | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [f, setF] = useState({
    name: item?.name ?? '',
    kind: (item?.kind ?? 'Desk') as SpaceKind,
    seats: String(item?.seats ?? 6),
    features: item?.features ?? '',
    assigned: item?.assignedEmployeeId ?? '',
    active: item?.isActive ?? true,
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () => {
      const b = {
        floorId,
        name: f.name.trim(),
        kind: f.kind,
        seats: Number(f.seats) || 1,
        features: f.features.trim() || null,
        assignedEmployeeId: f.assigned || null,
        isActive: f.active,
      };
      return item ? desksApi.updateSpace(item.id, b) : desksApi.addSpace(b);
    },
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: qk.desks });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? `Edit ${item.name}` : 'New desk or room'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.name.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Type">
          <Select value={f.kind} onChange={(e) => set('kind', e.target.value as SpaceKind)}>
            {(Object.keys(KIND_LABELS) as SpaceKind[]).map((k) => (
              <option key={k} value={k}>
                {KIND_LABELS[k]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Name" required>
          <Input value={f.name} maxLength={100} onChange={(e) => set('name', e.target.value)} placeholder={f.kind === 'Desk' ? 'A-01' : 'Boardroom'} />
        </Field>
        {f.kind !== 'Desk' && (
          <Field label="Seats">
            <Input type="number" min={1} max={500} value={f.seats} onChange={(e) => set('seats', e.target.value)} />
          </Field>
        )}
        <Field label="Features" className={f.kind === 'Desk' ? 'sm:col-span-2' : ''}>
          <Input
            value={f.features}
            maxLength={300}
            onChange={(e) => set('features', e.target.value)}
            placeholder={f.kind === 'Desk' ? 'Dual monitor' : 'Projector, video call'}
          />
        </Field>
        <Field label="Permanent for" hint="Only this person sits here" className="sm:col-span-2">
          <Select value={f.assigned} onChange={(e) => set('assigned', e.target.value)}>
            <option value="">Anyone can book</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        {item && <Checkbox label="In use" checked={f.active} onChange={(e) => set('active', e.target.checked)} />}
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Usage ----------

function UsageTab() {
  const u = useQuery({ queryKey: qk.deskUsage, queryFn: desksApi.utilisation });
  if (u.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (u.isError) return <ErrorState error={u.error} onRetry={() => u.refetch()} />;
  const d = u.data!;
  const tile = (label: string, value: string | number) => (
    <Card className="p-4">
      <p className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value}</p>
      <p className="text-xs text-zinc-500">{label}</p>
    </Card>
  );
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {tile('Bookings (last 30 days)', d.bookings)}
        {tile('Checked in', d.checkedIn)}
        {tile('No-shows', d.noShows)}
        {tile('Desks used on working days', `${d.deskUsePercent}%`)}
      </div>
      <Card>
        <CardHeader title="By desk and room" description={`${d.workingDays} working days`} />
        <Table>
          <THead>
            <Tr>
              <Th>Space</Th>
              <Th className="text-right">Bookings</Th>
              <Th className="text-right">Checked in</Th>
              <Th className="text-right">No-shows</Th>
              <Th className="text-right">Days used</Th>
            </Tr>
          </THead>
          <TBody>
            {d.spaces.map((s) => (
              <Tr key={`${s.floor}-${s.name}`}>
                <Td>
                  {s.name}{' '}
                  <span className="text-xs text-zinc-500">
                    · {s.floor} · {KIND_LABELS[s.kind]}
                  </span>
                </Td>
                <Td className="text-right tabular-nums">{s.bookings}</Td>
                <Td className="text-right tabular-nums">{s.checkedIn}</Td>
                <Td className="text-right tabular-nums">{s.released}</Td>
                <Td className="text-right tabular-nums">{s.usedPercent}%</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      </Card>
    </div>
  );
}
