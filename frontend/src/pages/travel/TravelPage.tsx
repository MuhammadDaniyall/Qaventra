import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { AlertTriangle, Plane, Plus, Search, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  MODE_LABELS,
  TRIP_LABELS,
  travelApi,
  type DeskFilter,
  type LegInput,
  type TravelMe,
  type TravelMode,
  type TravelReason,
  type TripListItem,
  type TripType,
} from '../../api/travel';
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
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
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
import { formatCurrency, formatDate, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { TripStatusBadge } from './shared';

type Tab = 'mine' | 'desk' | 'report' | 'settings';

/** Business travel: my trips for everyone; the travel desk queue and reports for the desk and HR; settings for HR. */
export function TravelPage() {
  const me = useQuery({ queryKey: qk.travelMe, queryFn: travelApi.me });
  const [params, setParams] = useSearchParams();
  const [creating, setCreating] = useState(false);
  if (me.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  const m = me.data!;
  const allowed: Tab[] = ['mine', ...(m.isDesk ? (['desk', 'report'] as Tab[]) : []), ...(m.isHr ? (['settings'] as Tab[]) : [])];
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && allowed.includes(raw) ? raw : m.isDesk && !raw ? 'desk' : 'mine';
  const labels: Record<Tab, string> = { mine: 'My trips', desk: 'Travel desk', report: 'Reports', settings: 'Settings' };
  return (
    <>
      <PageHeader
        title="Travel"
        description="Request business trips, get them booked, and settle expenses when you are back."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
            New trip
          </Button>
        }
      />
      {allowed.length > 1 && (
        <Tabs<Tab> className="mb-5" value={tab} onChange={(t) => setParams({ tab: t })} items={allowed.map((t) => ({ value: t, label: labels[t] }))} />
      )}
      {tab === 'mine' && <MyTrips onNew={() => setCreating(true)} />}
      {tab === 'desk' && <DeskTab />}
      {tab === 'report' && <ReportTab />}
      {tab === 'settings' && <SettingsTab />}
      {creating && <NewTripModal me={m} onClose={() => setCreating(false)} />}
    </>
  );
}

function TripTable({ trips, showPerson }: { trips: TripListItem[]; showPerson: boolean }) {
  const navigate = useNavigate();
  return (
    <Table>
      <THead>
        <Tr>
          {showPerson && <Th>Traveller</Th>}
          <Th>Trip</Th>
          <Th>Dates</Th>
          <Th>Status</Th>
          <Th className="text-end">Cost</Th>
        </Tr>
      </THead>
      <TBody>
        {trips.map((t) => (
          <Tr key={t.id} className="cursor-pointer" onClick={() => navigate(`/travel/${t.id}`)}>
            {showPerson && (
              <Td>
                <p className="font-medium text-zinc-900 dark:text-white">{t.employeeName}</p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">{[t.employeeCode, t.department].filter(Boolean).join(' · ')}</p>
              </Td>
            )}
            <Td>
              <Link to={`/travel/${t.id}`} onClick={(e) => e.stopPropagation()} className="font-medium text-zinc-900 hover:text-accent-600 dark:text-white">
                {t.route}
              </Link>
              <p className="line-clamp-1 text-xs text-zinc-500 dark:text-zinc-400">
                {[TRIP_LABELS[t.tripType], t.isInternational ? 'International' : null, t.reason, t.purpose].filter(Boolean).join(' · ')}
              </p>
              <div className="mt-1 flex flex-wrap gap-1">
                {t.urgent && <Badge tone="red">Urgent</Badge>}
                {t.needsHotel && <Badge>Hotel</Badge>}
                {t.advanceRequested ? (
                  <Badge tone={t.advancePaid ? 'green' : 'amber'}>
                    Advance {formatCurrency(t.advancePaid ?? t.advanceRequested)} {t.advancePaid ? 'paid' : 'asked'}
                  </Badge>
                ) : null}
              </div>
            </Td>
            <Td className="text-sm whitespace-nowrap">
              {formatDate(t.departOn)}
              {t.returnOn !== t.departOn && <span className="text-zinc-500 dark:text-zinc-400"> – {formatDate(t.returnOn)}</span>}
            </Td>
            <Td>
              <TripStatusBadge status={t.status} settlement={t.settlement} />
              {t.settlementOverdue && (
                <p className="mt-1 inline-flex items-center gap-1 text-xs text-red-600">
                  <AlertTriangle className="size-3" /> Settlement overdue
                </p>
              )}
            </Td>
            <Td className="text-end tabular-nums">{t.bookedCost ? formatCurrency(t.bookedCost) : '—'}</Td>
          </Tr>
        ))}
      </TBody>
    </Table>
  );
}

function MyTrips({ onNew }: { onNew: () => void }) {
  const trips = useQuery({ queryKey: qk.tripsMy, queryFn: travelApi.my });
  return (
    <Card>
      {trips.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : trips.isError ? (
        <ErrorState error={trips.error} onRetry={() => trips.refetch()} />
      ) : !trips.data?.length ? (
        <EmptyState
          icon={Plane}
          title="No trips yet"
          description="Request a trip; once approved, the travel desk books it."
          action={
            <Button icon={<Plus className="size-4" />} onClick={onNew}>
              New trip
            </Button>
          }
        />
      ) : (
        <TripTable trips={trips.data} showPerson={false} />
      )}
    </Card>
  );
}

function DeskTab() {
  const [filter, setFilter] = useState<DeskFilter>('book');
  const [search, setSearch] = useState('');
  const q = useDebounced(search.trim(), 300);
  const trips = useQuery({ queryKey: qk.tripsAll(filter, q), queryFn: () => travelApi.all(filter, q), placeholderData: keepPreviousData });
  const filters: Array<[DeskFilter, string]> = [
    ['book', 'To book'],
    ['advance', 'Advances to pay'],
    ['settle', 'Settlements to check'],
    ['unsettled', 'Not settled'],
    ['pending', 'Waiting for approval'],
    ['all', 'All trips'],
  ];
  return (
    <Card>
      <div className="flex flex-wrap items-center gap-2 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        {filters.map(([f, label]) => (
          <button
            key={f}
            type="button"
            onClick={() => setFilter(f)}
            className={
              filter === f
                ? 'rounded-full bg-accent-600 px-3 py-1 text-xs font-medium text-white'
                : 'rounded-full bg-zinc-100 px-3 py-1 text-xs font-medium text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300'
            }
          >
            {label}
          </button>
        ))}
        <div className="relative ms-auto w-full sm:w-64">
          <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" />
          <Input className="ps-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name, city, purpose…" />
        </div>
      </div>
      {trips.isLoading ? (
        <SkeletonRows rows={4} cols={5} />
      ) : trips.isError ? (
        <ErrorState error={trips.error} onRetry={() => trips.refetch()} />
      ) : !trips.data?.length ? (
        <EmptyState icon={Plane} title="Nothing here" description="Trips show up here as they need booking, advances or settlement." />
      ) : (
        <TripTable trips={trips.data} showPerson />
      )}
    </Card>
  );
}

function NewTripModal({ me, onClose }: { me: TravelMe; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const reasons = useQuery({ queryKey: qk.travelReasons, queryFn: travelApi.reasons });
  const blankLeg = (from = '', to = '', date = ''): LegInput => ({ from, to, date, mode: 'Air', preference: null });
  const [tripType, setTripType] = useState<TripType>('Return');
  const [legs, setLegs] = useState<LegInput[]>([blankLeg(), blankLeg()]);
  const [returnOn, setReturnOn] = useState('');
  const [f, setF] = useState({
    reasonId: '',
    purpose: '',
    international: false,
    urgentReason: '',
    needsHotel: false,
    hotelCity: '',
    checkIn: '',
    checkOut: '',
    needsPickDrop: false,
    pickDropNotes: '',
    estimatedCost: '',
    advance: '',
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const setLeg = (i: number, patch: Partial<LegInput>) =>
    setLegs((ls) => {
      const next = ls.map((l, j) => (j === i ? { ...l, ...patch } : l));
      // Keep a return trip's way back in step with the way out.
      if (tripType === 'Return' && i === 0 && (patch.from !== undefined || patch.to !== undefined))
        next[1] = { ...next[1], from: next[0].to, to: next[0].from };
      return next;
    });
  const changeType = (t: TripType) => {
    setTripType(t);
    setLegs((ls) =>
      t === 'OneWay' ? [ls[0]] : t === 'Return' ? [ls[0], blankLeg(ls[0].to, ls[0].from, ls[1]?.date ?? '')] : ls.length < 2 ? [ls[0], blankLeg(ls[0].to)] : ls,
    );
  };
  const depart = legs[0]?.date;
  const daysNotice = depart ? Math.round((Date.parse(depart) - Date.parse(todayIso())) / 86_400_000) : null;
  const late = daysNotice !== null && daysNotice < me.minNoticeDays;
  const n = (s: string) => (s.trim() === '' ? null : Number(s));
  const save = useMutation({
    mutationFn: () =>
      travelApi.create({
        reasonId: f.reasonId || null,
        purpose: f.purpose.trim(),
        tripType,
        isInternational: f.international,
        returnOn: tripType === 'OneWay' ? returnOn || null : null,
        legs: legs.map((l) => ({ ...l, from: l.from.trim(), to: l.to.trim(), preference: l.preference?.trim() || null })),
        urgentReason: late ? f.urgentReason.trim() || null : null,
        needsHotel: f.needsHotel,
        hotelCity: f.needsHotel ? f.hotelCity.trim() || null : null,
        checkIn: f.needsHotel ? f.checkIn || null : null,
        checkOut: f.needsHotel ? f.checkOut || null : null,
        needsPickDrop: f.needsPickDrop,
        pickDropNotes: f.needsPickDrop ? f.pickDropNotes.trim() || null : null,
        estimatedCost: n(f.estimatedCost),
        advanceRequested: n(f.advance),
      }),
    onSuccess: (t) => {
      toast.success(t.status === 'Pending' ? 'Trip sent for approval' : 'Trip approved', t.status === 'Pending' ? undefined : 'The travel desk will book it.');
      qc.invalidateQueries({ queryKey: qk.travel });
      navigate(`/travel/${t.id}`);
    },
  });
  const ready = f.purpose.trim() && legs.every((l) => l.from.trim() && l.to.trim() && l.date) && (!late || f.urgentReason.trim());
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="New trip"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!ready} onClick={() => save.mutate()}>
            Send for approval
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Purpose" required className="sm:col-span-2">
            <Textarea
              rows={2}
              value={f.purpose}
              maxLength={2000}
              onChange={(e) => set('purpose', e.target.value)}
              placeholder="Client workshop at Engro, Karachi"
            />
          </Field>
          <Field label="Reason">
            <Select value={f.reasonId} onChange={(e) => set('reasonId', e.target.value)}>
              <option value="">—</option>
              {reasons.data
                ?.filter((r) => r.isActive)
                .map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
            </Select>
          </Field>
          <div className="flex items-end pb-2">
            <Checkbox label="International trip" checked={f.international} onChange={(e) => set('international', e.target.checked)} />
          </div>
        </div>

        <div>
          <div className="mb-2 flex flex-wrap gap-2">
            {(Object.keys(TRIP_LABELS) as TripType[]).map((t) => (
              <button
                key={t}
                type="button"
                onClick={() => changeType(t)}
                className={
                  tripType === t
                    ? 'rounded-lg border border-accent-500 bg-accent-50 px-3 py-1.5 text-sm text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                    : 'rounded-lg border border-zinc-200 px-3 py-1.5 text-sm dark:border-zinc-700'
                }
              >
                {TRIP_LABELS[t]}
              </button>
            ))}
          </div>
          <div className="space-y-2">
            {legs.map((l, i) => (
              <div key={i} className="grid grid-cols-2 gap-2 rounded-lg border border-zinc-200 p-3 sm:grid-cols-[1fr_1fr_9rem_9rem_auto] dark:border-zinc-700">
                <Input aria-label="From" placeholder="From" value={l.from} maxLength={100} onChange={(e) => setLeg(i, { from: e.target.value })} />
                <Input aria-label="To" placeholder="To" value={l.to} maxLength={100} onChange={(e) => setLeg(i, { to: e.target.value })} />
                <Input aria-label="Date" type="date" min={todayIso()} value={l.date} onChange={(e) => setLeg(i, { date: e.target.value })} />
                <Select aria-label="Mode" value={l.mode} onChange={(e) => setLeg(i, { mode: e.target.value as TravelMode })}>
                  {(Object.keys(MODE_LABELS) as TravelMode[]).map((m) => (
                    <option key={m} value={m}>
                      {MODE_LABELS[m]}
                    </option>
                  ))}
                </Select>
                {tripType === 'MultiCity' && legs.length > 2 ? (
                  <IconButton label="Remove journey" onClick={() => setLegs((ls) => ls.filter((_, j) => j !== i))}>
                    <Trash2 className="size-4" />
                  </IconButton>
                ) : (
                  <span className="hidden sm:block" />
                )}
                <Input
                  className="col-span-2 sm:col-span-5"
                  aria-label="Preference"
                  placeholder={i === 0 ? 'Preference, e.g. morning flight' : 'Preference'}
                  value={l.preference ?? ''}
                  maxLength={150}
                  onChange={(e) => setLeg(i, { preference: e.target.value })}
                />
              </div>
            ))}
            {tripType === 'MultiCity' && legs.length < 10 && (
              <Button size="sm" variant="ghost" icon={<Plus className="size-3.5" />} onClick={() => setLegs((ls) => [...ls, blankLeg(ls[ls.length - 1].to)])}>
                Add journey
              </Button>
            )}
            {tripType === 'OneWay' && (
              <Field label="Back at work on" hint="Leave empty if you return the same day">
                <Input type="date" min={depart || todayIso()} value={returnOn} onChange={(e) => setReturnOn(e.target.value)} />
              </Field>
            )}
          </div>
        </div>

        {late && (
          <Field label={`This is less than ${me.minNoticeDays} days' notice. Why is it urgent?`} required>
            <Input value={f.urgentReason} maxLength={500} onChange={(e) => set('urgentReason', e.target.value)} />
          </Field>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2 sm:col-span-2">
            <Checkbox label="I need a hotel" checked={f.needsHotel} onChange={(e) => set('needsHotel', e.target.checked)} />
            {f.needsHotel && (
              <div className="grid gap-2 sm:grid-cols-3">
                <Input aria-label="Hotel city" placeholder="City" value={f.hotelCity} maxLength={100} onChange={(e) => set('hotelCity', e.target.value)} />
                <Input aria-label="Check-in" type="date" value={f.checkIn} onChange={(e) => set('checkIn', e.target.value)} />
                <Input aria-label="Check-out" type="date" value={f.checkOut} onChange={(e) => set('checkOut', e.target.value)} />
              </div>
            )}
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Checkbox label="I need pick and drop" checked={f.needsPickDrop} onChange={(e) => set('needsPickDrop', e.target.checked)} />
            {f.needsPickDrop && (
              <Input
                placeholder="E.g. home to airport at 6 am, airport to hotel"
                value={f.pickDropNotes}
                maxLength={500}
                onChange={(e) => set('pickDropNotes', e.target.value)}
              />
            )}
          </div>
          <Field label="Estimated cost (PKR)">
            <Input type="number" min={0} value={f.estimatedCost} onChange={(e) => set('estimatedCost', e.target.value)} />
          </Field>
          <Field label="Advance needed (PKR)" hint="Settled against your expenses after the trip">
            <Input type="number" min={0} value={f.advance} onChange={(e) => set('advance', e.target.value)} />
          </Field>
        </div>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function ReportTab() {
  const r = useQuery({ queryKey: qk.travelReport, queryFn: travelApi.report });
  if (r.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (r.isError) return <ErrorState error={r.error} onRetry={() => r.refetch()} />;
  const d = r.data!;
  const tile = (label: string, value: string | number, tone?: string) => (
    <Card className="p-4">
      <p className={tone ?? 'text-xl font-semibold text-zinc-900 tabular-nums dark:text-white'}>{value}</p>
      <p className="text-xs text-zinc-500">{label}</p>
    </Card>
  );
  const table = (title: string, rows: typeof d.byDepartment) => (
    <Card>
      <CardHeader title={title} />
      {rows.length ? (
        <Table>
          <THead>
            <Tr>
              <Th />
              <Th className="text-end">Trips</Th>
              <Th className="text-end">Booked</Th>
              <Th className="text-end">Claimed</Th>
            </Tr>
          </THead>
          <TBody>
            {rows.map((x) => (
              <Tr key={x.name}>
                <Td>{x.name}</Td>
                <Td className="text-end tabular-nums">{x.trips}</Td>
                <Td className="text-end tabular-nums">{formatCurrency(x.booked)}</Td>
                <Td className="text-end tabular-nums">{formatCurrency(x.claimed)}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      ) : (
        <p className="px-5 py-6 text-sm text-zinc-500">No trips this year.</p>
      )}
    </Card>
  );
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {tile('Trips this year', d.trips)}
        {tile('Coming up', d.upcoming)}
        {tile('To book', d.toBook)}
        {tile('Settlements to check', d.settlementsWaiting)}
        {tile('Settlements overdue', d.settlementsOverdue, d.settlementsOverdue ? 'text-xl font-semibold text-red-600 tabular-nums' : undefined)}
        {tile('Advances not settled', formatCurrency(d.advancesOutstanding))}
        {tile('Booked (tickets, hotels)', formatCurrency(d.bookedCost))}
        {tile('Claimed (settled)', formatCurrency(d.claimedCost))}
      </div>
      <div className="grid gap-5 lg:grid-cols-2">
        {table('By department', d.byDepartment)}
        {table('Top destinations', d.byDestination)}
      </div>
      {table('By month', d.byMonth)}
    </div>
  );
}

function SettingsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const settings = useQuery({ queryKey: qk.travelSettings, queryFn: travelApi.settings });
  const reasons = useQuery({ queryKey: qk.travelReasons, queryFn: travelApi.reasons });
  const people = useEmployeeLookup();
  const [form, setForm] = useState<{ notice: string; domestic: string; intl: string; settle: string; desk: string[] } | null>(null);
  const [editing, setEditing] = useState<TravelReason | 'new' | null>(null);
  const [deleting, setDeleting] = useState<TravelReason | null>(null);
  const s = settings.data;
  const f =
    form ??
    (s
      ? {
          notice: String(s.minNoticeDays),
          domestic: String(s.perDiemDomestic),
          intl: String(s.perDiemInternational),
          settle: String(s.settleWithinDays),
          desk: s.deskIds,
        }
      : null);
  const save = useMutation({
    mutationFn: () =>
      travelApi.saveSettings({
        minNoticeDays: Number(f!.notice) || 0,
        perDiemDomestic: Number(f!.domestic) || 0,
        perDiemInternational: Number(f!.intl) || 0,
        settleWithinDays: Number(f!.settle) || 15,
        deskIds: f!.desk,
      }),
    onSuccess: () => {
      toast.success('Travel settings saved');
      qc.invalidateQueries({ queryKey: qk.travel });
      setForm(null);
    },
    onError: (e) => toast.error(e),
  });
  if (settings.isLoading || !f) return <SkeletonRows rows={4} cols={2} />;
  const upd = (patch: Partial<NonNullable<typeof f>>) => setForm({ ...f, ...patch });
  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <Card>
        <CardHeader
          title="Rules"
          actions={
            <Button size="sm" loading={save.isPending} onClick={() => save.mutate()}>
              Save
            </Button>
          }
        />
        <div className="grid gap-4 px-5 py-4 sm:grid-cols-2">
          <Field label="Minimum notice (days)" hint="Shorter notice must give a reason">
            <Input type="number" min={0} max={90} value={f.notice} onChange={(e) => upd({ notice: e.target.value })} />
          </Field>
          <Field label="Settle within (days after return)">
            <Input type="number" min={1} max={180} value={f.settle} onChange={(e) => upd({ settle: e.target.value })} />
          </Field>
          <Field label="Daily allowance, domestic (PKR)">
            <Input type="number" min={0} value={f.domestic} onChange={(e) => upd({ domestic: e.target.value })} />
          </Field>
          <Field label="Daily allowance, international (PKR)">
            <Input type="number" min={0} value={f.intl} onChange={(e) => upd({ intl: e.target.value })} />
          </Field>
          <Field label="Travel desk" hint={f.desk.length ? `${f.desk.length} chosen` : 'Nobody chosen: HR books trips'} className="sm:col-span-2">
            <ul className="max-h-56 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
              {people.data?.map((p) => (
                <li key={p.id} className="px-3 py-1.5">
                  <Checkbox
                    label={p.name}
                    checked={f.desk.includes(p.id)}
                    onChange={(e) => upd({ desk: e.target.checked ? [...f.desk, p.id] : f.desk.filter((x) => x !== p.id) })}
                  />
                </li>
              ))}
            </ul>
          </Field>
        </div>
      </Card>
      <Card className="self-start">
        <CardHeader
          title="Reasons"
          description="Why people travel, e.g. client visit, training, conference"
          actions={
            <Button size="sm" variant="secondary" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
              Add
            </Button>
          }
        />
        {reasons.data?.length ? (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {reasons.data.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-2 px-5 py-2.5 text-sm">
                <span>
                  {r.name} {!r.isActive && <Badge>Off</Badge>}
                  <span className="ms-2 text-xs text-zinc-500 dark:text-zinc-400">{r.trips} trips</span>
                </span>
                <RowActions label={r.name} onEdit={() => setEditing(r)} onDelete={r.trips ? undefined : () => setDeleting(r)} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">No reasons yet.</p>
        )}
      </Card>
      {editing && <ReasonModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name={deleting?.name ?? ''}
        remove={travelApi.deleteReason}
        invalidate={[qk.travelReasons]}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}

function ReasonModal({ item, onClose }: { item: TravelReason | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [name, setName] = useState(item?.name ?? '');
  const [active, setActive] = useState(item?.isActive ?? true);
  const save = useMutation({
    mutationFn: () =>
      item ? travelApi.updateReason(item.id, { name: name.trim(), isActive: active }) : travelApi.addReason({ name: name.trim(), isActive: active }),
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: qk.travelReasons });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={item ? 'Edit reason' : 'New reason'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!name.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Name" required>
          <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Client visit" />
        </Field>
        <Checkbox label="In use" checked={active} onChange={(e) => setActive(e.target.checked)} />
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
