import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PiggyBank, Plus, Trash2 } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { errorMessage } from '../../api/client';
import { fundsApi, TAX_ADJUSTMENT_LABELS, type FundEntryKind, type FundKind, type FundSummary, type TaxAdjustmentKind } from '../../api/funds';
import { useEmployeeLookup } from '../../api/hooks';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  Field,
  FormError,
  IconButton,
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
  Tabs,
  useToast,
} from '../../components/ui';
import { formatDate, formatNumber, todayIso } from '../../lib/format';

const KEY = ['funds'] as const;
type Tab = 'pf' | 'gratuity' | 'withdrawals' | 'tax';

/** HR: provident fund and gratuity balances, withdrawals and tax adjustments. */
export function FundsPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'pf';
  return (
    <>
      <PageHeader
        title="Funds and tax"
        description="Provident fund and gratuity balances, withdrawals, and tax from previous employers or credits."
        back={{ to: '/payroll', label: 'Payroll' }}
      />
      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'pf', label: 'Provident fund' },
          { value: 'gratuity', label: 'Gratuity' },
          { value: 'withdrawals', label: 'Withdrawals' },
          { value: 'tax', label: 'Tax adjustments' },
        ]}
      />
      {tab === 'pf' && <ProvidentFund />}
      {tab === 'gratuity' && <Gratuity />}
      {tab === 'withdrawals' && <Withdrawals />}
      {tab === 'tax' && <TaxAdjustments />}
    </>
  );
}

function useSummary() {
  return useQuery({ queryKey: [...KEY, 'summary'], queryFn: () => fundsApi.summary(false) });
}

function ProvidentFund() {
  const summary = useSummary();
  const [entry, setEntry] = useState(false);
  const [profit, setProfit] = useState(false);
  const rows = (summary.data ?? []).filter((x) => x.pfMember || x.pfBalance !== 0);
  return (
    <Card>
      <CardHeader
        title="Provident fund"
        description="Contributions are added each time payroll is finalized. Members are ticked on each person's file; rates are in payroll settings."
        actions={
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onClick={() => setProfit(true)}>
              Share profit
            </Button>
            <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEntry(true)}>
              Opening balance
            </Button>
          </div>
        }
      />
      {summary.isLoading ? (
        <SkeletonRows rows={5} cols={7} />
      ) : !rows.length ? (
        <EmptyState icon={PiggyBank} title="No members yet" />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Employee</Th>
              <Th className="text-end">Own share</Th>
              <Th className="text-end">Company share</Th>
              <Th className="text-end">Profit</Th>
              <Th className="text-end">Withdrawn</Th>
              <Th className="text-end">Balance</Th>
              <Th className="text-end">Vested</Th>
            </tr>
          </THead>
          <TBody>
            {rows.map((x) => (
              <Tr key={x.employeeId}>
                <Td>
                  {x.name} <span className="text-xs text-zinc-500 dark:text-zinc-400">{x.code}</span>
                </Td>
                <Td className="text-end tabular-nums">{formatNumber(x.pfEmployeeShare)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.pfEmployerShare)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.pfProfit)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.pfWithdrawn)}</Td>
                <Td className="text-end font-medium tabular-nums">{formatNumber(x.pfBalance)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.pfVested)}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {entry && <EntryModal fund="ProvidentFund" onClose={() => setEntry(false)} />}
      {profit && <ProfitModal onClose={() => setProfit(false)} />}
    </Card>
  );
}

function Gratuity() {
  const summary = useSummary();
  const [entry, setEntry] = useState(false);
  return (
    <Card>
      <CardHeader
        title="Gratuity"
        description="Worked out from years of service and current salary, as set in payroll settings; less anything already paid."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEntry(true)}>
            Opening balance or payment
          </Button>
        }
      />
      {summary.isLoading ? (
        <SkeletonRows rows={5} cols={5} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Employee</Th>
              <Th className="text-end">Years</Th>
              <Th className="text-end">Accrued</Th>
              <Th className="text-end">Paid</Th>
              <Th className="text-end">Due</Th>
            </tr>
          </THead>
          <TBody>
            {(summary.data ?? []).map((x: FundSummary) => (
              <Tr key={x.employeeId}>
                <Td>
                  {x.name} <span className="text-xs text-zinc-500 dark:text-zinc-400">{x.code}</span>
                </Td>
                <Td className="text-end tabular-nums">{x.serviceYears.toFixed(1)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.gratuityAccrued)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(x.gratuityWithdrawn)}</Td>
                <Td className="text-end font-medium tabular-nums">{formatNumber(x.gratuityBalance)}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {entry && <EntryModal fund="Gratuity" onClose={() => setEntry(false)} />}
    </Card>
  );
}

function EntryModal({ fund, onClose }: { fund: FundKind; onClose: () => void }) {
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [f, setF] = useState({ employeeId: '', kind: 'Opening' as FundEntryKind, date: todayIso(), employee: '', employer: '', note: '' });
  const save = useMutation({
    mutationFn: () =>
      fundsApi.addEntry({
        employeeId: f.employeeId,
        fund,
        kind: f.kind,
        date: f.date,
        employeeAmount: Number(f.employee) || 0,
        employerAmount: Number(f.employer) || 0,
        note: f.note.trim() || null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KEY });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={fund === 'ProvidentFund' ? 'Provident fund entry' : 'Gratuity entry'}
      description="Opening balances when moving from another system, or corrections (negative to take out)."
      footer={
        <Button loading={save.isPending} disabled={!f.employeeId} onClick={() => save.mutate()}>
          Save
        </Button>
      }
    >
      <div className="space-y-4">
        <Field label="Employee" required>
          <Select value={f.employeeId} onChange={(e) => setF({ ...f, employeeId: e.target.value })}>
            <option value="">Choose…</option>
            {(people.data ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Kind">
            <Select value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value as FundEntryKind })}>
              <option value="Opening">Opening balance</option>
              <option value="Adjustment">Correction / payment</option>
              {fund === 'ProvidentFund' && <option value="Profit">Profit</option>}
            </Select>
          </Field>
          <Field label="Date">
            <Input type="date" value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} />
          </Field>
          <Field label={fund === 'ProvidentFund' ? 'Own share' : 'Amount'}>
            <Input type="number" value={f.employee} onChange={(e) => setF({ ...f, employee: e.target.value })} />
          </Field>
          {fund === 'ProvidentFund' && (
            <Field label="Company share">
              <Input type="number" value={f.employer} onChange={(e) => setF({ ...f, employer: e.target.value })} />
            </Field>
          )}
        </div>
        <Field label="Note">
          <Input value={f.note} maxLength={300} onChange={(e) => setF({ ...f, note: e.target.value })} />
        </Field>
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

function ProfitModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [date, setDate] = useState(todayIso());
  const [rate, setRate] = useState('');
  const [note, setNote] = useState('');
  const save = useMutation({
    mutationFn: () => fundsApi.profit({ date, ratePercent: Number(rate), note: note.trim() || null }),
    onSuccess: (r) => {
      toast.success('Profit shared', `${formatNumber(r.total)} to ${r.members} members`);
      qc.invalidateQueries({ queryKey: KEY });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Share the fund's profit"
      description="Each member gets the rate on their balance on that date."
      footer={
        <Button loading={save.isPending} disabled={!(Number(rate) > 0)} onClick={() => save.mutate()}>
          Share
        </Button>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Field label="As of">
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label="Rate (%)">
            <Input type="number" min={0.01} max={100} step={0.01} value={rate} onChange={(e) => setRate(e.target.value)} />
          </Field>
        </div>
        <Field label="Note">
          <Input value={note} maxLength={300} placeholder="Profit 2025-26" onChange={(e) => setNote(e.target.value)} />
        </Field>
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

function Withdrawals() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: [...KEY, 'withdrawals'], queryFn: () => fundsApi.withdrawals() });
  const decide = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) => (approve ? fundsApi.approve(id, null) : fundsApi.reject(id, null)),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  if (list.isLoading) return <SkeletonRows rows={4} cols={5} />;
  if (!list.data?.length) return <EmptyState icon={PiggyBank} title="No withdrawals" />;
  return (
    <Card>
      <Table>
        <THead>
          <tr>
            <Th>Employee</Th>
            <Th>Fund</Th>
            <Th className="text-end">Amount</Th>
            <Th>Reason</Th>
            <Th>Status</Th>
            <Th />
          </tr>
        </THead>
        <TBody>
          {list.data.map((w) => (
            <Tr key={w.id}>
              <Td>
                {w.employeeName} <span className="text-xs text-zinc-500 dark:text-zinc-400">{w.employeeCode}</span>
              </Td>
              <Td>{w.fund === 'ProvidentFund' ? 'Provident fund' : 'Gratuity'}</Td>
              <Td className="text-end tabular-nums">{formatNumber(w.amount)}</Td>
              <Td className="text-sm">
                {w.reason}
                {w.payWithSalary && <Badge className="ms-2">With salary</Badge>}
              </Td>
              <Td>
                <StatusBadge status={w.status} />
              </Td>
              <Td className="text-end">
                {w.status === 'Pending' && (
                  <div className="flex justify-end gap-1">
                    <Button size="sm" variant="ghost" onClick={() => decide.mutate({ id: w.id, approve: false })}>
                      Reject
                    </Button>
                    <Button size="sm" onClick={() => decide.mutate({ id: w.id, approve: true })}>
                      Approve
                    </Button>
                  </div>
                )}
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}

function TaxAdjustments() {
  const qc = useQueryClient();
  const toast = useToast();
  const years = useQuery({ queryKey: [...KEY, 'tax-years'], queryFn: fundsApi.taxYears });
  const [year, setYear] = useState('');
  const current = year || years.data?.[0] || '';
  const list = useQuery({ queryKey: [...KEY, 'tax', current], queryFn: () => fundsApi.taxAdjustments(current), enabled: !!current });
  const people = useEmployeeLookup();
  const [f, setF] = useState({ employeeId: '', kind: 'PreviousIncome' as TaxAdjustmentKind, amount: '', note: '' });
  const add = useMutation({
    mutationFn: () =>
      fundsApi.addTaxAdjustment({ employeeId: f.employeeId, taxYearCode: current, kind: f.kind, amount: Number(f.amount), note: f.note.trim() || null }),
    onSuccess: () => {
      setF({ ...f, amount: '', note: '' });
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({ mutationFn: fundsApi.deleteTaxAdjustment, onSuccess: () => qc.invalidateQueries({ queryKey: KEY }) });
  return (
    <Card>
      <CardHeader
        title="Tax adjustments"
        description="When someone has any, their monthly tax is worked out on the whole year: tax on the year's income, less credits and tax already paid, spread over the months left."
        actions={
          <Select className="w-36" value={current} onChange={(e) => setYear(e.target.value)} aria-label="Tax year">
            {(years.data ?? []).map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </Select>
        }
      />
      <div className="grid gap-2 border-b border-zinc-100 p-4 sm:grid-cols-[1fr_14rem_9rem_1fr_auto] dark:border-zinc-800">
        <Select aria-label="Employee" value={f.employeeId} onChange={(e) => setF({ ...f, employeeId: e.target.value })}>
          <option value="">Employee…</option>
          {(people.data ?? []).map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </Select>
        <Select aria-label="Kind" value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value as TaxAdjustmentKind })}>
          {Object.entries(TAX_ADJUSTMENT_LABELS).map(([k, v]) => (
            <option key={k} value={k}>
              {v}
            </option>
          ))}
        </Select>
        <Input type="number" min={0} placeholder="Amount" aria-label="Amount" value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} />
        <Input placeholder="Note" aria-label="Note" value={f.note} maxLength={300} onChange={(e) => setF({ ...f, note: e.target.value })} />
        <Button disabled={!f.employeeId || !(Number(f.amount) > 0)} loading={add.isPending} onClick={() => add.mutate()}>
          Add
        </Button>
      </div>
      <Table>
        <TBody>
          {(list.data ?? []).map((t) => (
            <Tr key={t.id}>
              <Td>
                {t.employeeName} <span className="text-xs text-zinc-500 dark:text-zinc-400">{t.employeeCode}</span>
              </Td>
              <Td>{TAX_ADJUSTMENT_LABELS[t.kind]}</Td>
              <Td className="text-end tabular-nums">{formatNumber(t.amount)}</Td>
              <Td className="text-sm text-zinc-500 dark:text-zinc-400">{t.note}</Td>
              <Td className="text-end">
                <IconButton label="Delete" onClick={() => remove.mutate(t.id)}>
                  <Trash2 className="size-4" />
                </IconButton>
              </Td>
            </Tr>
          ))}
          {!list.data?.length && (
            <Tr>
              <Td className="text-sm text-zinc-500 dark:text-zinc-400">No adjustments for {current}.</Td>
            </Tr>
          )}
        </TBody>
      </Table>
    </Card>
  );
}

/** Employee self-service: fund balances and withdrawal requests. */
export function MyFundsCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const mine = useQuery({ queryKey: [...KEY, 'me'], queryFn: fundsApi.mine });
  const withdrawals = useQuery({ queryKey: [...KEY, 'my-withdrawals'], queryFn: fundsApi.myWithdrawals });
  const [asking, setAsking] = useState<FundKind | null>(null);
  const cancel = useMutation({
    mutationFn: fundsApi.cancelWithdrawal,
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  const m = mine.data;
  if (!m || (!m.pfMember && m.pfBalance === 0 && m.gratuityBalance === 0)) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Provident fund and gratuity" description={`${m.serviceYears.toFixed(1)} years of service`} />
      <div className="grid gap-4 px-5 pb-5 sm:grid-cols-2">
        {(m.pfMember || m.pfBalance !== 0) && (
          <div className="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
            <p className="text-sm text-zinc-500 dark:text-zinc-400">Provident fund</p>
            <p className="text-2xl font-semibold tabular-nums">{formatNumber(m.pfBalance)}</p>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              Yours {formatNumber(m.pfEmployeeShare)} · company {formatNumber(m.pfEmployerShare)} · profit {formatNumber(m.pfProfit)} · can withdraw{' '}
              {formatNumber(m.pfVested)}
            </p>
            <Button size="sm" variant="secondary" className="mt-3" disabled={m.pfVested <= 0} onClick={() => setAsking('ProvidentFund')}>
              Ask to withdraw
            </Button>
          </div>
        )}
        {m.gratuityBalance > 0 && (
          <div className="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
            <p className="text-sm text-zinc-500 dark:text-zinc-400">Gratuity due today</p>
            <p className="text-2xl font-semibold tabular-nums">{formatNumber(m.gratuityBalance)}</p>
            <Button size="sm" variant="secondary" className="mt-3" onClick={() => setAsking('Gratuity')}>
              Ask to withdraw
            </Button>
          </div>
        )}
      </div>
      {!!withdrawals.data?.length && (
        <Table>
          <TBody>
            {withdrawals.data.map((w) => (
              <Tr key={w.id}>
                <Td>{formatDate(w.createdAtUtc.slice(0, 10))}</Td>
                <Td>{w.fund === 'ProvidentFund' ? 'Provident fund' : 'Gratuity'}</Td>
                <Td className="text-end tabular-nums">{formatNumber(w.amount)}</Td>
                <Td>
                  <StatusBadge status={w.status} />
                </Td>
                <Td className="text-end">
                  {w.status === 'Pending' && (
                    <Button size="sm" variant="ghost" onClick={() => cancel.mutate(w.id)}>
                      Cancel
                    </Button>
                  )}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {asking && <WithdrawModal fund={asking} max={asking === 'ProvidentFund' ? m.pfVested : m.gratuityBalance} onClose={() => setAsking(null)} />}
    </Card>
  );
}

function WithdrawModal({ fund, max, onClose }: { fund: FundKind; max: number; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const save = useMutation({
    mutationFn: () => fundsApi.withdraw({ fund, amount: Number(amount), reason: reason.trim(), payWithSalary: true }),
    onSuccess: () => {
      toast.success('Sent for approval');
      qc.invalidateQueries({ queryKey: KEY });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={fund === 'ProvidentFund' ? 'Withdraw from provident fund' : 'Withdraw gratuity'}
      description={`Up to ${formatNumber(max)}. Paid with your salary once approved.`}
      footer={
        <Button loading={save.isPending} disabled={!(Number(amount) > 0) || !reason.trim()} onClick={() => save.mutate()}>
          Send
        </Button>
      }
    >
      <div className="space-y-4">
        <Field label="Amount">
          <Input type="number" min={1} max={max} value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <Field label="Why">
          <Input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </Field>
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}
