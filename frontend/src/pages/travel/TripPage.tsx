import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { AlertTriangle, Banknote, Check, Plus, Receipt, Ticket, Trash2, Undo2, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { CATEGORY_LABELS, MODE_LABELS, TRAVEL_EXPENSE_CATEGORIES, TRIP_LABELS, travelApi, type ExpenseCategory, type Trip } from '../../api/travel';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Button,
  Card,
  CardHeader,
  ConfirmDialog,
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
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { TripStatusBadge } from './shared';

/** One trip: route, booking, advance, settlement and files, with the actions the viewer may take. */
export function TripPage() {
  const { id = '' } = useParams();
  const trip = useQuery({ queryKey: qk.trip(id), queryFn: () => travelApi.get(id) });
  if (trip.isLoading) return <SkeletonRows rows={8} cols={3} />;
  if (trip.isError) return <ErrorState error={trip.error} onRetry={() => trip.refetch()} />;
  return <TripView t={trip.data!} />;
}

function TripView({ t }: { t: Trip }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [booking, setBooking] = useState(false);
  const [advancing, setAdvancing] = useState(false);
  const [settling, setSettling] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [returning, setReturning] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: qk.travel });
  const cancel = useMutation({
    mutationFn: () => travelApi.cancel(t.id, null),
    onSuccess: () => {
      toast.success('Trip cancelled');
      refresh();
      setCancelling(false);
    },
    onError: (e) => toast.error(e),
  });
  const approveSettlement = useMutation({
    mutationFn: () => travelApi.approveSettlement(t.id, null),
    onSuccess: () => {
      toast.success('Settlement approved', t.net ? 'Sent to this month’s payroll.' : undefined);
      refresh();
    },
    onError: (e) => toast.error(e),
  });

  const active = t.status === 'Approved' || t.status === 'Booked';
  const started = t.departOn <= todayIso();
  const canSettle = t.isMine && active && started && (t.settlement === 'None' || t.settlement === 'Returned');
  const route = [t.legs[0]?.from, ...t.legs.map((l) => l.to)].filter(Boolean).join(' → ');
  const overdue = active && (t.settlement === 'None' || t.settlement === 'Returned') && t.settleBy < todayIso();

  return (
    <>
      <PageHeader
        back={{ to: t.isMine ? '/travel' : '/travel?tab=desk', label: 'Travel' }}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {route} <TripStatusBadge status={t.status} settlement={t.settlement} />
          </span>
        }
        description={`${t.employeeName} (${t.employeeCode}) · ${formatDate(t.departOn)}${t.returnOn !== t.departOn ? ` – ${formatDate(t.returnOn)}` : ''} · ${TRIP_LABELS[t.tripType]}${t.isInternational ? ' · International' : ''}`}
        actions={
          <div className="flex flex-wrap gap-2">
            {t.isDesk && active && t.settlement !== 'Settled' && (
              <Button
                size="sm"
                variant={t.status === 'Approved' ? 'primary' : 'secondary'}
                icon={<Ticket className="size-3.5" />}
                onClick={() => setBooking(true)}
              >
                {t.status === 'Booked' ? 'Edit booking' : 'Book'}
              </Button>
            )}
            {t.isDesk && active && (t.settlement === 'None' || t.settlement === 'Returned') && (
              <Button size="sm" variant="secondary" icon={<Banknote className="size-3.5" />} onClick={() => setAdvancing(true)}>
                {t.advancePaid ? 'Change advance' : 'Pay advance'}
              </Button>
            )}
            {canSettle && (
              <Button size="sm" icon={<Receipt className="size-3.5" />} onClick={() => setSettling(true)}>
                Settle expenses
              </Button>
            )}
            {(t.isMine || t.isHr) && (t.status === 'Pending' || (active && !started)) && (
              <Button size="sm" variant="ghost" icon={<X className="size-3.5" />} onClick={() => setCancelling(true)}>
                Cancel trip
              </Button>
            )}
          </div>
        }
      />

      {t.urgentReason && (
        <div className="mb-4 flex items-start gap-2 rounded-lg bg-amber-50 px-4 py-2.5 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" /> Short notice: {t.urgentReason}
        </div>
      )}
      {overdue && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-2.5 text-sm text-red-800 dark:bg-red-500/10 dark:text-red-200">
          The settlement was due by {formatDate(t.settleBy)}.
        </div>
      )}
      {t.settlement === 'Returned' && t.settlementDecisionNote && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-2.5 text-sm text-red-800 dark:bg-red-500/10 dark:text-red-200">
          Settlement sent back: {t.settlementDecisionNote}
        </div>
      )}

      <div className="grid gap-5 lg:grid-cols-[1fr_22rem]">
        <div className="space-y-5">
          <Card>
            <CardHeader title="Journeys" description={t.purpose} />
            <Table>
              <THead>
                <Tr>
                  <Th>Date</Th>
                  <Th>From → to</Th>
                  <Th>How</Th>
                  <Th>Booking</Th>
                  <Th className="text-end">Cost</Th>
                </Tr>
              </THead>
              <TBody>
                {t.legs.map((l) => (
                  <Tr key={l.id}>
                    <Td className="whitespace-nowrap">{formatDate(l.date)}</Td>
                    <Td>
                      {l.from} → {l.to}
                      {l.preference && <p className="text-xs text-zinc-500 dark:text-zinc-400">{l.preference}</p>}
                    </Td>
                    <Td>{MODE_LABELS[l.mode]}</Td>
                    <Td className="font-mono text-xs">{l.bookingRef ?? '—'}</Td>
                    <Td className="text-end tabular-nums">{l.bookingCost != null ? formatCurrency(l.bookingCost) : '—'}</Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
          </Card>

          {(t.needsHotel || t.needsPickDrop || t.hotelName) && (
            <Card>
              <CardHeader title="Stay and transport" />
              <dl className="grid gap-4 px-5 py-4 text-sm sm:grid-cols-2">
                {t.needsHotel && (
                  <div>
                    <dt className="text-xs text-zinc-500 dark:text-zinc-400">Hotel</dt>
                    <dd>
                      {t.hotelName ? (
                        <>
                          <span className="font-medium">{t.hotelName}</span>
                          {t.hotelRef && <span className="ms-1 font-mono text-xs text-zinc-500 dark:text-zinc-400">{t.hotelRef}</span>}
                          {t.hotelCost != null && <span className="ms-1 text-zinc-500 dark:text-zinc-400">· {formatCurrency(t.hotelCost)}</span>}
                        </>
                      ) : (
                        <span className="text-zinc-500 dark:text-zinc-400">Not booked yet</span>
                      )}
                      <p className="text-xs text-zinc-500 dark:text-zinc-400">
                        {t.hotelCity} · {formatDate(t.checkIn)} – {formatDate(t.checkOut)}
                      </p>
                    </dd>
                  </div>
                )}
                {t.needsPickDrop && (
                  <div>
                    <dt className="text-xs text-zinc-500 dark:text-zinc-400">Pick and drop</dt>
                    <dd>{t.pickDropNotes ?? 'Needed'}</dd>
                  </div>
                )}
                {t.bookingNotes && (
                  <div className="sm:col-span-2">
                    <dt className="text-xs text-zinc-500 dark:text-zinc-400">Notes from the travel desk</dt>
                    <dd className="whitespace-pre-line">{t.bookingNotes}</dd>
                  </div>
                )}
              </dl>
            </Card>
          )}

          {(t.settlement !== 'None' || t.expenses.length > 0) && (
            <Card>
              <CardHeader
                title="Settlement"
                description={
                  t.settlement === 'Settled' ? `Settled ${formatDateTimeUtc(t.settledAtUtc)}` : t.settlement === 'Submitted' ? 'Waiting for HR' : undefined
                }
                actions={
                  t.isHr &&
                  !t.isMine &&
                  t.settlement === 'Submitted' && (
                    <>
                      <Button
                        size="sm"
                        variant="success"
                        icon={<Check className="size-3.5" />}
                        loading={approveSettlement.isPending}
                        onClick={() => approveSettlement.mutate()}
                      >
                        Approve
                      </Button>
                      <Button size="sm" variant="secondary" icon={<Undo2 className="size-3.5" />} onClick={() => setReturning(true)}>
                        Send back
                      </Button>
                    </>
                  )
                }
              />
              <Table>
                <TBody>
                  {t.expenses.map((e) => (
                    <Tr key={e.id}>
                      <Td className="whitespace-nowrap">{formatDate(e.date)}</Td>
                      <Td>
                        {e.description}
                        <span className="ms-1 text-xs text-zinc-500 dark:text-zinc-400">{CATEGORY_LABELS[e.category]}</span>
                      </Td>
                      <Td className="text-end tabular-nums">{formatCurrency(e.amount)}</Td>
                    </Tr>
                  ))}
                  <Tr>
                    <Td />
                    <Td>
                      Daily allowance: {t.perDiemDays ?? 0} days × {formatCurrency(t.perDiemRate ?? 0)}
                    </Td>
                    <Td className="text-end tabular-nums">{formatCurrency((t.perDiemDays ?? 0) * (t.perDiemRate ?? 0))}</Td>
                  </Tr>
                  <Tr>
                    <Td />
                    <Td className="font-medium">Claimed</Td>
                    <Td className="text-end font-medium tabular-nums">{formatCurrency(t.claimed)}</Td>
                  </Tr>
                  <Tr>
                    <Td />
                    <Td>Less advance</Td>
                    <Td className="text-end tabular-nums">−{formatCurrency(t.advancePaid ?? 0)}</Td>
                  </Tr>
                  <Tr>
                    <Td />
                    <Td className="font-semibold">{t.net >= 0 ? 'To pay the traveller' : 'To recover from salary'}</Td>
                    <Td className="text-end font-semibold tabular-nums">{formatCurrency(Math.abs(t.net))}</Td>
                  </Tr>
                </TBody>
              </Table>
              {t.settlementNote && <p className="border-t border-zinc-100 px-5 py-3 text-sm dark:border-zinc-800">{t.settlementNote}</p>}
            </Card>
          )}

          <Card>
            <CardHeader
              title="Tickets and receipts"
              description="The desk attaches tickets and vouchers; the traveller attaches receipts for the settlement."
            />
            <div className="px-5 py-4">
              <AttachmentList owner="Travel" ownerId={t.id} emptyText="No files yet." />
            </div>
          </Card>
        </div>

        <div className="space-y-5">
          <Card>
            <CardHeader title="Money" />
            <dl className="space-y-2.5 px-5 py-4 text-sm">
              {(
                [
                  ['Estimated cost', t.estimatedCost != null ? formatCurrency(t.estimatedCost) : '—'],
                  ['Booked (tickets, hotel)', formatCurrency(t.bookedCost)],
                  ['Advance asked', t.advanceRequested != null ? formatCurrency(t.advanceRequested) : '—'],
                  [
                    'Advance paid',
                    t.advancePaid != null
                      ? `${formatCurrency(t.advancePaid)} on ${formatDate(t.advancePaidOn)}${t.advancePaidVia ? ` (${t.advancePaidVia})` : ''}`
                      : '—',
                  ],
                  ['Daily allowance', `${formatCurrency(t.perDiemRate ?? t.perDiemRateNow)} a day`],
                  ['Settle by', formatDate(t.settleBy)],
                ] as Array<[string, string]>
              ).map(([k, v]) => (
                <div key={k} className="flex justify-between gap-3">
                  <dt className="text-zinc-500 dark:text-zinc-400">{k}</dt>
                  <dd className="text-end">{v}</dd>
                </div>
              ))}
            </dl>
          </Card>
          <Card>
            <CardHeader title="Approval" />
            <div className="px-5 py-4">
              <ApprovalTimeline type="Travel" subjectId={t.id} />
              {t.decisionNote && <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">“{t.decisionNote}”</p>}
              {t.bookedByName && (
                <p className="mt-3 text-xs text-zinc-500 dark:text-zinc-400">
                  Booked by {t.bookedByName}, {formatDateTimeUtc(t.bookedAtUtc)}
                </p>
              )}
            </div>
          </Card>
        </div>
      </div>

      {booking && <BookingModal t={t} onClose={() => setBooking(false)} />}
      {advancing && <AdvanceModal t={t} onClose={() => setAdvancing(false)} />}
      {settling && <SettleModal t={t} onClose={() => setSettling(false)} />}
      {returning && <ReturnModal t={t} onClose={() => setReturning(false)} />}
      <ConfirmDialog
        open={cancelling}
        title="Cancel this trip?"
        message={t.status === 'Booked' ? 'The travel desk will be told to cancel the tickets and hotel.' : 'The request will be withdrawn.'}
        confirmLabel="Cancel trip"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setCancelling(false)}
      />
    </>
  );
}

const num = (s: string) => (s.trim() === '' ? null : Number(s));

function BookingModal({ t, onClose }: { t: Trip; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [legs, setLegs] = useState(t.legs.map((l) => ({ legId: l.id, ref: l.bookingRef ?? '', cost: l.bookingCost?.toString() ?? '' })));
  const [hotel, setHotel] = useState({ name: t.hotelName ?? '', ref: t.hotelRef ?? '', cost: t.hotelCost?.toString() ?? '' });
  const [notes, setNotes] = useState(t.bookingNotes ?? '');
  const save = useMutation({
    mutationFn: () =>
      travelApi.book(t.id, {
        legs: legs.map((l) => ({ legId: l.legId, bookingRef: l.ref.trim() || null, bookingCost: num(l.cost) })),
        hotelName: hotel.name.trim() || null,
        hotelRef: hotel.ref.trim() || null,
        hotelCost: num(hotel.cost),
        notes: notes.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Booking saved', `${t.employeeName} has been told.`);
      qc.invalidateQueries({ queryKey: qk.travel });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Booking"
      description="Attach the e-tickets and hotel voucher on the trip page after saving."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Save and tell the traveller
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {t.legs.map((l, i) => (
          <div key={l.id} className="grid gap-2 sm:grid-cols-[1fr_10rem_9rem] sm:items-end">
            <p className="text-sm">
              <span className="font-medium">
                {l.from} → {l.to}
              </span>
              <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                {formatDate(l.date)} · {MODE_LABELS[l.mode]}
                {l.preference ? ` · ${l.preference}` : ''}
              </span>
            </p>
            <Input
              aria-label="Ticket or PNR"
              placeholder="Ticket / PNR"
              value={legs[i].ref}
              maxLength={100}
              onChange={(e) => setLegs((x) => x.map((y, j) => (j === i ? { ...y, ref: e.target.value } : y)))}
            />
            <Input
              aria-label="Cost"
              type="number"
              min={0}
              placeholder="Cost"
              value={legs[i].cost}
              onChange={(e) => setLegs((x) => x.map((y, j) => (j === i ? { ...y, cost: e.target.value } : y)))}
            />
          </div>
        ))}
        {t.needsHotel && (
          <div className="grid gap-2 border-t border-zinc-100 pt-4 sm:grid-cols-[1fr_10rem_9rem] dark:border-zinc-800">
            <Input
              aria-label="Hotel"
              placeholder={`Hotel in ${t.hotelCity}`}
              value={hotel.name}
              maxLength={150}
              onChange={(e) => setHotel({ ...hotel, name: e.target.value })}
            />
            <Input
              aria-label="Hotel reference"
              placeholder="Reference"
              value={hotel.ref}
              maxLength={100}
              onChange={(e) => setHotel({ ...hotel, ref: e.target.value })}
            />
            <Input
              aria-label="Hotel cost"
              type="number"
              min={0}
              placeholder="Cost"
              value={hotel.cost}
              onChange={(e) => setHotel({ ...hotel, cost: e.target.value })}
            />
          </div>
        )}
        <Field label="Notes for the traveller" hint={t.needsPickDrop ? `Pick and drop asked: ${t.pickDropNotes ?? 'yes'}` : undefined}>
          <Textarea rows={3} value={notes} maxLength={2000} onChange={(e) => setNotes(e.target.value)} placeholder="Driver’s number, check-in time…" />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function AdvanceModal({ t, onClose }: { t: Trip; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [amount, setAmount] = useState((t.advancePaid ?? t.advanceRequested ?? '').toString());
  const [on, setOn] = useState(t.advancePaidOn ?? todayIso());
  const [via, setVia] = useState(t.advancePaidVia ?? 'Cash');
  const save = useMutation({
    mutationFn: () => travelApi.advance(t.id, { amount: Number(amount), paidOn: on, via: via.trim() || null }),
    onSuccess: () => {
      toast.success('Advance recorded');
      qc.invalidateQueries({ queryKey: qk.travel });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Advance paid"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!(Number(amount) > 0) || !on} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Amount (PKR)" required hint={t.advanceRequested ? `Asked: ${formatCurrency(t.advanceRequested)}` : undefined}>
          <Input type="number" min={1} value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <Field label="Paid on" required>
          <Input type="date" max={todayIso()} value={on} onChange={(e) => setOn(e.target.value)} />
        </Field>
        <Field label="Paid by">
          <Select value={via} onChange={(e) => setVia(e.target.value)}>
            {['Cash', 'Bank transfer', 'Cheque', 'Company card'].map((v) => (
              <option key={v}>{v}</option>
            ))}
          </Select>
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function SettleModal({ t, onClose }: { t: Trip; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const maxDays = Math.round((Date.parse(t.returnOn) - Date.parse(t.departOn)) / 86_400_000) + 1;
  const [lines, setLines] = useState(
    t.expenses.length
      ? t.expenses.map((e) => ({ date: e.date, category: e.category, description: e.description, amount: String(e.amount) }))
      : [{ date: t.departOn, category: 'Travel' as ExpenseCategory, description: '', amount: '' }],
  );
  const [days, setDays] = useState(String(t.perDiemDays ?? maxDays));
  const [note, setNote] = useState(t.settlementNote ?? '');
  const filled = lines.filter((l) => l.description.trim() || l.amount);
  const expenses = filled.reduce((a, l) => a + (Number(l.amount) || 0), 0);
  const perDiem = (Number(days) || 0) * t.perDiemRateNow;
  const net = expenses + perDiem - (t.advancePaid ?? 0);
  const valid = filled.every((l) => l.description.trim() && Number(l.amount) > 0 && l.date) && Number(days) >= 0 && Number(days) <= maxDays;
  const save = useMutation({
    mutationFn: () =>
      travelApi.settle(t.id, {
        expenses: filled.map((l) => ({ date: l.date, category: l.category, description: l.description.trim(), amount: Number(l.amount) })),
        perDiemDays: Number(days) || 0,
        note: note.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Settlement sent to HR', 'Attach your receipts on the trip page.');
      qc.invalidateQueries({ queryKey: qk.travel });
      onClose();
    },
  });
  const upd = (i: number, patch: Partial<(typeof lines)[number]>) => setLines((x) => x.map((y, j) => (j === i ? { ...y, ...patch } : y)));
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="Settle expenses"
      description="Costs you paid yourself. Tickets and hotel booked by the company are not claimed here."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>
            Send to HR
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        {lines.map((l, i) => (
          <div key={i} className="grid grid-cols-2 gap-2 sm:grid-cols-[9rem_10rem_1fr_8rem_auto]">
            <Input aria-label="Date" type="date" value={l.date} onChange={(e) => upd(i, { date: e.target.value })} />
            <Select aria-label="Category" value={l.category} onChange={(e) => upd(i, { category: e.target.value as ExpenseCategory })}>
              {TRAVEL_EXPENSE_CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {CATEGORY_LABELS[c]}
                </option>
              ))}
            </Select>
            <Input
              aria-label="Description"
              placeholder="What for"
              value={l.description}
              maxLength={500}
              onChange={(e) => upd(i, { description: e.target.value })}
            />
            <Input aria-label="Amount" type="number" min={0} placeholder="PKR" value={l.amount} onChange={(e) => upd(i, { amount: e.target.value })} />
            <IconButton label="Remove line" onClick={() => setLines((x) => x.filter((_, j) => j !== i))}>
              <Trash2 className="size-4" />
            </IconButton>
          </div>
        ))}
        <Button
          size="sm"
          variant="ghost"
          icon={<Plus className="size-3.5" />}
          onClick={() => setLines((x) => [...x, { date: x[x.length - 1]?.date ?? t.departOn, category: 'Meals', description: '', amount: '' }])}
        >
          Add expense
        </Button>
      </div>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <Field label="Days of daily allowance" hint={`Up to ${maxDays} days at ${formatCurrency(t.perDiemRateNow)} a day`}>
          <Input type="number" min={0} max={maxDays} value={days} onChange={(e) => setDays(e.target.value)} />
        </Field>
        <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-800/50">
          <p className="flex justify-between">
            <span>Expenses</span>
            <span className="tabular-nums">{formatCurrency(expenses)}</span>
          </p>
          <p className="flex justify-between">
            <span>Daily allowance</span>
            <span className="tabular-nums">{formatCurrency(perDiem)}</span>
          </p>
          <p className="flex justify-between">
            <span>Less advance</span>
            <span className="tabular-nums">−{formatCurrency(t.advancePaid ?? 0)}</span>
          </p>
          <p className="mt-1 flex justify-between border-t border-zinc-200 pt-1 font-semibold dark:border-zinc-700">
            <span>{net >= 0 ? 'You get' : 'You return'}</span>
            <span className="tabular-nums">{formatCurrency(Math.abs(net))}</span>
          </p>
        </div>
        <Field label="Note for HR" className="sm:col-span-2">
          <Textarea rows={2} value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function ReturnModal({ t, onClose }: { t: Trip; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const save = useMutation({
    mutationFn: () => travelApi.returnSettlement(t.id, note.trim()),
    onSuccess: () => {
      toast.success('Sent back to the traveller');
      qc.invalidateQueries({ queryKey: qk.travel });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Send the settlement back"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!note.trim()} onClick={() => save.mutate()}>
            Send back
          </Button>
        </>
      }
    >
      <Field label="What needs fixing?" required>
        <Textarea rows={3} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
