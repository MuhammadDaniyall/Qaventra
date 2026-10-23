import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { exemptionsApi } from '../../api/leaveRules';
import { Button, Card, CardHeader, Field, FormError, Input, Modal, StatusBadge, Table, TBody, Td, Textarea, Tr, useToast } from '../../components/ui';
import { formatDate } from '../../lib/format';

/** "Excuse" button for a late or half day, with the month's quota. */
export function ExcuseButton({ date, flag, early = 0 }: { date: string; flag: string; early?: number }) {
  const [open, setOpen] = useState(false);
  const mine = useQuery({ queryKey: qk.exemptions, queryFn: exemptionsApi.my });
  const asked = mine.data?.find((x) => x.date === date && (x.status === 'Pending' || x.status === 'Approved'));
  if (flag !== 'Late' && flag !== 'HalfDay' && flag !== 'ShortDay' && !early) return null;
  if (asked) return <StatusBadge status={asked.status} label={asked.status === 'Pending' ? 'Excuse asked' : 'Excused'} />;
  return (
    <>
      <Button size="sm" variant="ghost" onClick={() => setOpen(true)}>
        Excuse
      </Button>
      {open && <ExcuseModal date={date} flag={early && flag === 'Present' ? 'EarlyLeave' : flag} onClose={() => setOpen(false)} />}
    </>
  );
}

function ExcuseModal({ date, flag, onClose }: { date: string; flag: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const quota = useQuery({ queryKey: qk.exemptionQuota, queryFn: exemptionsApi.quota });
  const [reason, setReason] = useState('');
  const save = useMutation({
    mutationFn: () => exemptionsApi.request(date, reason.trim()),
    onSuccess: () => {
      toast.success('Sent for approval');
      qc.invalidateQueries({ queryKey: qk.exemptions });
      qc.invalidateQueries({ queryKey: qk.exemptionQuota });
      qc.invalidateQueries({ queryKey: qk.attendance });
      onClose();
    },
  });
  const q = quota.data;
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={`Excuse ${({ Late: 'late arrival', HalfDay: 'half day', ShortDay: 'short day', EarlyLeave: 'early leave' } as Record<string, string>)[flag] ?? 'day'} on ${formatDate(date)}`}
      description={q ? `${Math.max(0, q.allowed - q.used)} of ${q.allowed} excuses left this month` : undefined}
      footer={
        <Button loading={save.isPending} disabled={!reason.trim()} onClick={() => save.mutate()}>
          Send
        </Button>
      }
    >
      <Field label="What happened?" required hint="Once approved, the day counts as present: no late penalty or half-day deduction.">
        <Textarea rows={3} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
      </Field>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

/** The signed-in user's exemption requests. */
export function MyExemptions() {
  const toast = useToast();
  const qc = useQueryClient();
  const mine = useQuery({ queryKey: qk.exemptions, queryFn: exemptionsApi.my });
  const cancel = useMutation({
    mutationFn: exemptionsApi.cancel,
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.exemptions }),
    onError: (e) => toast.error(e),
  });
  if (!mine.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Excuses" description="Late arrivals and half days you asked to excuse" />
      <Table>
        <TBody>
          {mine.data.map((x) => (
            <Tr key={x.id}>
              <Td className="whitespace-nowrap">{formatDate(x.date)}</Td>
              <Td>{x.flag === 'Late' ? `Late${x.checkIn ? ` (in ${x.checkIn})` : ''}` : x.flag === 'HalfDay' ? 'Half day' : x.flag === 'ShortDay' ? 'Short day' : 'Early leave'}</Td>
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

/** HR: how many excuses each person may ask for a month, and how far back. */
export function ExemptionSettingsCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const s = useQuery({ queryKey: qk.exemptionSettings, queryFn: exemptionsApi.settings });
  const [f, setF] = useState<{ perMonth: string; within: string } | null>(null);
  const v = f ?? (s.data ? { perMonth: String(s.data.exemptionsPerMonth), within: String(s.data.exemptionWithinDays) } : null);
  const save = useMutation({
    mutationFn: () => exemptionsApi.saveSettings({ exemptionsPerMonth: Number(v!.perMonth) || 0, exemptionWithinDays: Number(v!.within) || 7 }),
    onSuccess: () => {
      toast.success('Saved');
      setF(null);
      qc.invalidateQueries({ queryKey: qk.exemptionSettings });
    },
    onError: (e) => toast.error(e),
  });
  if (!v) return null;
  return (
    <Card className="mt-5">
      <CardHeader
        title="Excusing late arrivals and half days"
        description="Employees ask from My attendance; approved excuses count the day as present (no late penalty or half-day deduction)."
        actions={
          <Button size="sm" loading={save.isPending} onClick={() => save.mutate()}>
            Save
          </Button>
        }
      />
      <div className="grid gap-4 px-5 py-4 sm:grid-cols-2">
        <Field label="Excuses per person per month" hint="0 turns excuses off">
          <Input type="number" min={0} max={31} value={v.perMonth} onChange={(e) => setF({ ...v, perMonth: e.target.value })} />
        </Field>
        <Field label="Can be asked within (days)">
          <Input type="number" min={1} max={60} value={v.within} onChange={(e) => setF({ ...v, within: e.target.value })} />
        </Field>
      </div>
    </Card>
  );
}
