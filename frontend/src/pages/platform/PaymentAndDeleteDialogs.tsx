import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Trash2 } from 'lucide-react';
import { platformApi } from '../../api/platform';
import { ApiError, errorMessage } from '../../api/client';
import { Button, Field, FormError, Input, Modal, Select, Textarea, useToast } from '../../components/ui';
import { CYCLE_LABELS, extendSubscription, PAYMENT_METHODS } from '../../lib/billing';
import { formatCurrency, formatDate, todayIso } from '../../lib/format';
import type { Company } from '../../types';

/** Record a subscription payment; extends the end date by N billing cycles. */
export function RecordPaymentModal({ company, onClose }: { company: Company; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [amount, setAmount] = useState(company.amountPerCycle != null ? String(company.amountPerCycle) : '');
  const [paidOn, setPaidOn] = useState(todayIso());
  const [periods, setPeriods] = useState('1');
  const [method, setMethod] = useState('Bank transfer');
  const [reference, setReference] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const n = Number(periods);
  const validPeriods = Number.isInteger(n) && n >= 1 && n <= 36;
  const preview = company.billingCycle && validPeriods
    ? extendSubscription(todayIso(), company.subscriptionEndsOn, company.billingCycle, n)
    : null;

  const record = useMutation({
    mutationFn: () =>
      platformApi.recordPayment(company.id, {
        amount: Number(amount),
        paidOn,
        periods: n,
        method: method || null,
        reference: reference.trim() || null,
        notes: notes.trim() || null,
      }),
    onSuccess: (c) => {
      toast.success('Payment recorded', c.subscriptionEndsOn ? `${c.name} now runs until ${formatDate(c.subscriptionEndsOn)}` : c.name);
      qc.invalidateQueries({ queryKey: ['platform'] });
      onClose();
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  const submit = () => {
    setError(null);
    const fe: Record<string, string> = {};
    const a = Number(amount);
    if (!amount || !Number.isFinite(a) || a < 1) fe.amount = 'Enter the amount received.';
    if (!paidOn) fe.paidOn = 'Pick the payment date.';
    if (!validPeriods) fe.periods = 'Between 1 and 36 cycles.';
    setFieldErrors(fe);
    if (!Object.keys(fe).length) record.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Record payment"
      description={`${company.name}${company.billingCycle ? ` · ${CYCLE_LABELS[company.billingCycle]} plan · ${formatCurrency(company.amountPerCycle)} per cycle` : ''}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="record-payment-form" loading={record.isPending}>
            Record payment
          </Button>
        </>
      }
    >
      <form
        id="record-payment-form"
        noValidate
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <FormError message={error} />
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Amount (PKR)" required error={fieldErrors.amount} className="sm:col-span-1">
            <Input type="number" min={1} inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </Field>
          <Field label="Paid on" required error={fieldErrors.paidOn}>
            <Input type="date" value={paidOn} onChange={(e) => setPaidOn(e.target.value)} />
          </Field>
          <Field label="Cycles" required error={fieldErrors.periods}>
            <Input
              type="number"
              min={1}
              max={36}
              value={periods}
              onChange={(e) => {
                setPeriods(e.target.value);
                const k = Number(e.target.value);
                if (company.amountPerCycle != null && Number.isInteger(k) && k >= 1 && k <= 36) setAmount(String(company.amountPerCycle * k));
              }}
            />
          </Field>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Method">
            <Select value={method} onChange={(e) => setMethod(e.target.value)}>
              {PAYMENT_METHODS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Reference" hint="Transaction ID, cheque no., etc." error={fieldErrors.reference}>
            <Input value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} />
          </Field>
        </div>
        <Field label="Notes" error={fieldErrors.notes}>
          <Textarea rows={2} value={notes} maxLength={1000} onChange={(e) => setNotes(e.target.value)} />
        </Field>
        {preview && (
          <div className="rounded-lg bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200" aria-live="polite">
            Covers {formatDate(preview.from)} – {formatDate(preview.to)}.{' '}
            <strong className="font-semibold">Subscription will run until {formatDate(preview.to)}.</strong>
          </div>
        )}
      </form>
    </Modal>
  );
}

/** Permanent delete; the exact company name must be typed to enable the button. */
export function DeleteCompanyModal({ company, onClose, onDeleted }: { company: Company; onClose: () => void; onDeleted: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [typed, setTyped] = useState('');
  const [error, setError] = useState<string | null>(null);
  const matches = typed === company.name;

  const del = useMutation({
    mutationFn: () => platformApi.deleteCompany(company.id, typed),
    onSuccess: () => {
      toast.success(`${company.name} deleted permanently`);
      qc.removeQueries({ queryKey: ['platform', 'companies', company.id] });
      qc.invalidateQueries({ queryKey: ['platform'] });
      onDeleted();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  return (
    <Modal
      open
      onClose={del.isPending ? () => undefined : onClose}
      size="md"
      title="Delete company permanently?"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={del.isPending}>
            Cancel
          </Button>
          <Button variant="danger" icon={<Trash2 className="size-4" />} disabled={!matches} loading={del.isPending} onClick={() => del.mutate()}>
            Delete company
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <div className="flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-200">
          <AlertTriangle className="mt-0.5 size-5 shrink-0" />
          <div>
            <p className="font-semibold">This cannot be undone.</p>
            <p className="mt-1">
              <strong>{company.name}</strong> and ALL of its data will be permanently deleted: employees, attendance, leave, payroll and
              payslips, salaries, settings, payments and every login. Audit logs are kept.
            </p>
          </div>
        </div>
        <Field label={`Type the company name to confirm: ${company.name}`}>
          <Input value={typed} autoComplete="off" onChange={(e) => setTyped(e.target.value)} placeholder={company.name} />
        </Field>
      </div>
    </Modal>
  );
}
