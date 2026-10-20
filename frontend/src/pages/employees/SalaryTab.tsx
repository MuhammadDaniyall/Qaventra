import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2 } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  PageSpinner,
  Select,
  Switch,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, todayIso } from '../../lib/format';
import type { Salary, SalaryComponent, SalaryComponentKind } from '../../types';

interface Row extends Omit<SalaryComponent, 'amount'> {
  key: number;
  amount: string;
}

let rowKey = 1;
const toRow = (c: SalaryComponent): Row => ({ ...c, amount: String(c.amount), key: rowKey++ });

export function SalaryTab({ employeeId }: { employeeId: string }) {
  const salary = useQuery({ queryKey: qk.salary(employeeId), queryFn: () => employeesApi.salary(employeeId) });
  if (salary.isLoading) return <PageSpinner />;
  if (salary.isError) {
    return (
      <Card>
        <ErrorState error={salary.error} onRetry={() => salary.refetch()} />
      </Card>
    );
  }
  return <SalaryForm key={salary.dataUpdatedAt} employeeId={employeeId} current={salary.data ?? null} />;
}

function SalaryForm({ employeeId, current }: { employeeId: string; current: Salary | null }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [effectiveFrom, setEffectiveFrom] = useState(current?.effectiveFrom ?? todayIso());
  const [basic, setBasic] = useState(current ? String(current.basicSalary) : '');
  const [eobi, setEobi] = useState(current?.eobiApplicable ?? true);
  const [rows, setRows] = useState<Row[]>(() => (current?.components ?? []).map(toRow));
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const num = (s: string) => {
    const n = Number(s);
    return Number.isFinite(n) ? n : 0;
  };
  const basicN = num(basic);
  const earnings = rows.filter((r) => r.kind === 'Earning').reduce((t, r) => t + num(r.amount), 0);
  const deductions = rows.filter((r) => r.kind === 'Deduction').reduce((t, r) => t + num(r.amount), 0);
  const gross = basicN + earnings;

  const save = useMutation({
    mutationFn: (b: Salary) => employeesApi.saveSalary(employeeId, b),
    onSuccess: () => {
      toast.success('Salary saved', `Effective from ${formatDate(effectiveFrom)}`);
      qc.invalidateQueries({ queryKey: qk.salary(employeeId) });
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  const update = (key: number, patch: Partial<Row>) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    const fe: Record<string, string> = {};
    if (!effectiveFrom) fe.effectiveFrom = 'Pick the date this salary starts.';
    if (basic === '' || basicN < 0) fe.basicSalary = 'Enter the basic salary.';
    rows.forEach((r, i) => {
      if (!r.name.trim()) fe[`row${i}`] = 'Each component needs a name.';
      else if (r.amount === '' || num(r.amount) < 0) fe[`row${i}`] = 'Enter a positive amount.';
    });
    setFieldErrors(fe);
    if (Object.keys(fe).length) return;
    save.mutate({
      id: current?.id ?? null,
      effectiveFrom,
      basicSalary: basicN,
      eobiApplicable: eobi,
      components: rows.map((r) => ({ name: r.name.trim(), kind: r.kind, amount: num(r.amount), isTaxable: r.isTaxable })),
    });
  };

  return (
    <form onSubmit={submit} className="grid gap-6 lg:grid-cols-3" noValidate>
      <div className="space-y-6 lg:col-span-2">
        <FormError message={error} />
        <Card>
          <CardHeader
            title="Salary structure"
            description={
              current
                ? `Current structure is effective from ${formatDate(current.effectiveFrom)}. Saving with a new date keeps history.`
                : 'No salary set yet.'
            }
          />
          <CardBody className="grid gap-4 sm:grid-cols-2">
            <Field label="Basic salary (PKR / month)" required error={fieldErrors.basicSalary}>
              <Input type="number" min={0} step="1" inputMode="numeric" value={basic} onChange={(e) => setBasic(e.target.value)} />
            </Field>
            <Field label="Effective from" required error={fieldErrors.effectiveFrom}>
              <Input type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />
            </Field>
            <div className="sm:col-span-2">
              <Switch
                checked={eobi}
                onChange={setEobi}
                label="EOBI applicable"
                description="Deduct the employee's EOBI contribution and record the employer share."
              />
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Allowances & deductions"
            description="Fixed monthly components on top of basic salary."
            actions={
              <Button
                variant="secondary"
                size="sm"
                icon={<Plus className="size-3.5" />}
                onClick={() => setRows((rs) => [...rs, { key: rowKey++, name: '', kind: 'Earning', amount: '', isTaxable: true }])}
              >
                Add component
              </Button>
            }
          />
          {rows.length === 0 ? (
            <p className="px-5 py-8 text-center text-sm text-zinc-500 dark:text-zinc-400">
              No components. Add allowances such as House rent, Medical or Conveyance.
            </p>
          ) : (
            <div className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {rows.map((r, i) => (
                <div key={r.key} className="px-5 py-4">
                  <div className="grid grid-cols-2 items-end gap-3 sm:grid-cols-[1fr_140px_150px_auto_auto]">
                    <Field label="Name" className="col-span-2 sm:col-span-1">
                      <Input value={r.name} placeholder="House rent" onChange={(e) => update(r.key, { name: e.target.value })} />
                    </Field>
                    <Field label="Kind">
                      <Select value={r.kind} onChange={(e) => update(r.key, { kind: e.target.value as SalaryComponentKind })}>
                        <option value="Earning">Earning</option>
                        <option value="Deduction">Deduction</option>
                      </Select>
                    </Field>
                    <Field label="Amount">
                      <Input type="number" min={0} inputMode="numeric" value={r.amount} onChange={(e) => update(r.key, { amount: e.target.value })} />
                    </Field>
                    <label className="flex h-9 items-center gap-2 text-sm text-zinc-700 dark:text-zinc-300">
                      <input
                        type="checkbox"
                        className="size-4 accent-accent-600"
                        checked={r.isTaxable}
                        onChange={(e) => update(r.key, { isTaxable: e.target.checked })}
                      />
                      Taxable
                    </label>
                    <IconButton label={`Remove ${r.name || 'component'}`} onClick={() => setRows((rs) => rs.filter((x) => x.key !== r.key))}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                  {fieldErrors[`row${i}`] && <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{fieldErrors[`row${i}`]}</p>}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>

      <div>
        <Card className="lg:sticky lg:top-20">
          <CardHeader title="Monthly summary" description="Before income tax and EOBI." />
          <CardBody>
            <dl className="space-y-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-zinc-500 dark:text-zinc-400">Basic salary</dt>
                <dd className="tabular-nums">{formatCurrency(basicN)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-zinc-500 dark:text-zinc-400">Allowances</dt>
                <dd className="tabular-nums">{formatCurrency(earnings)}</dd>
              </div>
              <div className="flex justify-between border-t border-zinc-100 pt-3 font-medium dark:border-zinc-800">
                <dt>Gross</dt>
                <dd className="tabular-nums">{formatCurrency(gross)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-zinc-500 dark:text-zinc-400">Fixed deductions</dt>
                <dd className="text-red-600 tabular-nums dark:text-red-400">−{formatCurrency(deductions)}</dd>
              </div>
              <div className="flex items-baseline justify-between border-t border-zinc-100 pt-3 dark:border-zinc-800">
                <dt className="font-medium">Estimated net</dt>
                <dd className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(gross - deductions)}</dd>
              </div>
            </dl>
            <Button type="submit" className="mt-5 w-full" loading={save.isPending}>
              Save salary
            </Button>
          </CardBody>
        </Card>
      </div>
    </form>
  );
}
