import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { payApi, PAY_KIND_LABELS, type PayRequestKind } from '../../api/pay';
import { Button, Checkbox, Field, FormError, Input, Modal, Select, Spinner, Textarea, useToast } from '../../components/ui';
import { formatCurrency, monthLabel, toIsoDate, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';

/** The next 4 payroll months starting with this one, as select options. */
export function payrollMonths(count = 4) {
  const now = new Date();
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() + i, 1);
    return { year: d.getFullYear(), month: d.getMonth() + 1, label: monthLabel(d.getFullYear(), d.getMonth() + 1) };
  });
}

/**
 * Pay item form. Employees: advance salary or overtime for themselves. HR (forEmployee): allowance, bonus or deduction
 * for anyone, approved as entered.
 */
export function PayRequestModal({ kind, hr = false, onClose }: { kind: PayRequestKind; hr?: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const employees = useEmployeeLookup(hr);
  const months = payrollMonths();
  const [employeeId, setEmployeeId] = useState('');
  const [itemKind, setItemKind] = useState<PayRequestKind>(kind);
  const [title, setTitle] = useState('');
  const [amount, setAmount] = useState('');
  const [taxable, setTaxable] = useState(true);
  const [period, setPeriod] = useState(`${months[0].year}-${months[0].month}`);
  const [workDate, setWorkDate] = useState(toIsoDate(new Date(Date.now() - 86_400_000)));
  const [hours, setHours] = useState('2');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const isOvertime = itemKind === 'Overtime';
  const [year, month] = period.split('-').map(Number);

  const quoteInput = useDebounced({ workDate, hours }, 300);
  const quote = useQuery({
    queryKey: ['pay', 'quote', quoteInput.workDate, quoteInput.hours],
    queryFn: () => payApi.overtimeQuote(quoteInput.workDate, Number(quoteInput.hours)),
    enabled: isOvertime && !hr && !!quoteInput.workDate && Number(quoteInput.hours) > 0,
    retry: false,
  });

  const submit = useMutation({
    mutationFn: () =>
      payApi.submit({
        employeeId: hr ? employeeId : null,
        kind: itemKind,
        title: title.trim() || null,
        amount: isOvertime ? null : Number(amount),
        isTaxable: taxable,
        year,
        month,
        workDate: isOvertime ? workDate : null,
        hours: isOvertime ? Number(hours) : null,
        reason: reason.trim() || null,
      }),
    onSuccess: (r) => {
      toast.success(
        r.status === 'Approved' ? `${PAY_KIND_LABELS[r.kind]} added` : 'Request sent',
        `${formatCurrency(r.amount)} · ${monthLabel(r.year, r.month)} payroll`,
      );
      qc.invalidateQueries({ queryKey: qk.pay });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (hr && !employeeId) return setError('Choose the employee.');
    if (isOvertime) {
      if (!workDate || !(Number(hours) > 0)) return setError('Enter the date and the hours worked.');
    } else if (!(Number(amount) > 0)) return setError('Enter an amount.');
    submit.mutate();
  };

  const titles: Record<PayRequestKind, string> = {
    AdvanceSalary: 'Request advance salary',
    Overtime: 'Claim overtime',
    Allowance: 'Add allowance, bonus or deduction',
    Bonus: 'Add allowance, bonus or deduction',
    Deduction: 'Add allowance, bonus or deduction',
  };

  return (
    <Modal
      open
      onClose={onClose}
      title={titles[kind]}
      description={
        hr
          ? 'Added to the chosen month’s payroll straight away.'
          : itemKind === 'AdvanceSalary'
            ? 'Paid to you now and recovered in full from the chosen month’s salary.'
            : 'Paid with the chosen month’s salary once approved.'
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="pay-request-form" loading={submit.isPending}>
            {hr ? 'Add' : 'Send request'}
          </Button>
        </>
      }
    >
      <form id="pay-request-form" onSubmit={onSubmit} className="space-y-4">
        <FormError message={error} />
        {hr && (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Employee" required>
              <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
                <option value="">Choose…</option>
                {employees.data?.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Type">
              <Select value={itemKind} onChange={(e) => setItemKind(e.target.value as PayRequestKind)}>
                <option value="Allowance">Allowance</option>
                <option value="Bonus">Bonus</option>
                <option value="Deduction">Deduction</option>
              </Select>
            </Field>
          </div>
        )}
        {isOvertime ? (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Date worked" required>
              <Input type="date" value={workDate} max={todayIso()} onChange={(e) => setWorkDate(e.target.value)} />
            </Field>
            <Field label="Extra hours" required>
              <Input type="number" min={0.5} max={16} step="0.5" value={hours} onChange={(e) => setHours(e.target.value)} />
            </Field>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3">
            <Field label={hr ? 'Title' : 'Purpose'} hint={hr ? 'Shown on the payslip' : undefined}>
              <Input value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} placeholder={hr ? 'e.g. Eid bonus' : 'e.g. Medical emergency'} />
            </Field>
            <Field label="Amount (Rs)" required>
              <Input type="number" min={1} value={amount} onChange={(e) => setAmount(e.target.value)} className="tabular-nums" />
            </Field>
          </div>
        )}
        {isOvertime && !hr && (
          <div className="flex items-center justify-between rounded-lg bg-zinc-50 px-3 py-2.5 text-sm dark:bg-zinc-800/50" aria-live="polite">
            <span className="text-zinc-600 dark:text-zinc-300">
              You will be paid{quote.data ? ` (${quote.data.multiplier}× hourly basic)` : ''}
            </span>
            <span className="font-semibold text-zinc-900 tabular-nums dark:text-white">
              {quote.isFetching ? <Spinner className="size-4" /> : quote.isError ? '—' : formatCurrency(quote.data?.amount)}
            </span>
          </div>
        )}
        {quote.isError && <p className="text-xs text-red-600">{errorMessage(quote.error)}</p>}
        <Field label="Payroll month">
          <Select value={period} onChange={(e) => setPeriod(e.target.value)}>
            {months.map((m) => (
              <option key={m.label} value={`${m.year}-${m.month}`}>
                {m.label}
              </option>
            ))}
          </Select>
        </Field>
        {hr && itemKind !== 'Deduction' && (
          <Checkbox label="Taxable" description="Counts towards income tax (most allowances and bonuses are)." checked={taxable} onChange={(e) => setTaxable(e.target.checked)} />
        )}
        <Field label="Reason">
          <Textarea value={reason} rows={2} maxLength={1000} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </form>
    </Modal>
  );
}

export function LoanModal({ hr = false, onClose }: { hr?: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const employees = useEmployeeLookup(hr);
  const months = payrollMonths(6);
  const [employeeId, setEmployeeId] = useState('');
  const [title, setTitle] = useState('Personal loan');
  const [amount, setAmount] = useState('');
  const [installments, setInstallments] = useState('6');
  const [start, setStart] = useState(`${months[1].year}-${months[1].month}`);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const n = Number(installments);
  const per = Number(amount) > 0 && n > 0 ? Math.ceil(Number(amount) / n) : null;
  const [sy, sm] = start.split('-').map(Number);

  const submit = useMutation({
    mutationFn: () =>
      payApi.requestLoan({ employeeId: hr ? employeeId : null, title: title.trim(), amount: Number(amount), installments: n, startYear: sy, startMonth: sm, reason: reason.trim() || null }),
    onSuccess: () => {
      toast.success(hr ? 'Loan recorded' : 'Loan request sent', hr ? 'Installments start with the chosen payroll month.' : 'Your approver has been notified.');
      qc.invalidateQueries({ queryKey: qk.pay });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={hr ? 'Record a loan' : 'Apply for a loan'}
      description={
        hr
          ? 'Approved as recorded. One installment is deducted from each payroll until it is repaid.'
          : 'Once approved, one installment is deducted from each payroll until it is repaid.'
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={submit.isPending}
            onClick={() => {
              setError(null);
              if (hr && !employeeId) return setError('Choose the employee.');
              if (!(Number(amount) > 0)) return setError('Enter the loan amount.');
              if (!Number.isInteger(n) || n < 1 || n > 60) return setError('Installments must be between 1 and 60.');
              submit.mutate();
            }}
          >
            {hr ? 'Record loan' : 'Send request'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        {hr && (
          <Field label="Employee" required>
            <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
              <option value="">Choose…</option>
              {employees.data?.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field label="Purpose">
            <Input value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} />
          </Field>
          <Field label="Amount (Rs)" required>
            <Input type="number" min={1} value={amount} onChange={(e) => setAmount(e.target.value)} />
          </Field>
          <Field label="Installments" hint={per ? `${formatCurrency(per)} a month` : undefined}>
            <Input type="number" min={1} max={60} value={installments} onChange={(e) => setInstallments(e.target.value)} />
          </Field>
          <Field label="First deduction">
            <Select value={start} onChange={(e) => setStart(e.target.value)}>
              {months.map((m) => (
                <option key={m.label} value={`${m.year}-${m.month}`}>
                  {m.label}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Field label="Reason">
          <Textarea value={reason} rows={2} maxLength={1000} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
