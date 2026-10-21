import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { payrollApi } from '../../api/payroll';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { Button, Card, CardBody, CardHeader, ErrorState, Field, FormError, Input, PageHeader, PageSpinner, Switch, useToast } from '../../components/ui';
import type { PayrollSettings } from '../../types';

export function PayrollSettingsPage() {
  const settings = useQuery({ queryKey: qk.payrollSettings, queryFn: payrollApi.settings });
  return (
    <>
      <PageHeader title="Payroll settings" back={{ to: '/payroll', label: 'Payroll' }} description="Company-wide rules used when processing payroll." />
      {settings.isLoading ? (
        <PageSpinner />
      ) : settings.isError || !settings.data ? (
        <Card>
          <ErrorState error={settings.error} onRetry={() => settings.refetch()} />
        </Card>
      ) : (
        <SettingsForm key={settings.dataUpdatedAt} initial={settings.data} />
      )}
    </>
  );
}

function SettingsForm({ initial }: { initial: PayrollSettings }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [employee, setEmployee] = useState(String(initial.eobiEmployeeContribution));
  const [employer, setEmployer] = useState(String(initial.eobiEmployerContribution));
  const [deductAbsent, setDeductAbsent] = useState(initial.deductAbsentDays);
  const [overtime, setOvertime] = useState(String(initial.overtimeMultiplier ?? 1.5));
  const [latesPer, setLatesPer] = useState(String(initial.latesPerPenalty ?? 0));
  const [penaltyDays, setPenaltyDays] = useState(String(initial.latePenaltyDays ?? 1));
  const [halfDays, setHalfDays] = useState(initial.deductHalfDays ?? false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const save = useMutation({
    mutationFn: (b: PayrollSettings) => payrollApi.saveSettings(b),
    onSuccess: () => {
      toast.success('Payroll settings saved');
      qc.invalidateQueries({ queryKey: qk.payrollSettings });
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    const a = Number(employee);
    const b = Number(employer);
    const fe: Record<string, string> = {};
    if (employee === '' || !Number.isFinite(a) || a < 0) fe.eobiEmployeeContribution = 'Enter an amount of 0 or more.';
    if (employer === '' || !Number.isFinite(b) || b < 0) fe.eobiEmployerContribution = 'Enter an amount of 0 or more.';
    const ot = Number(overtime);
    if (overtime === '' || !Number.isFinite(ot) || ot < 0 || ot > 5) fe.overtimeMultiplier = 'Between 0 and 5 (e.g. 1.5 or 2).';
    const lp = Number(latesPer);
    const pd = Number(penaltyDays);
    if (!Number.isInteger(lp) || lp < 0 || lp > 31) fe.latesPerPenalty = 'A whole number from 0 to 31.';
    if (!Number.isFinite(pd) || pd < 0 || pd > 5) fe.latePenaltyDays = 'Between 0 and 5 (e.g. 0.5 or 1).';
    setFieldErrors(fe);
    if (Object.keys(fe).length) return;
    save.mutate({
      eobiEmployeeContribution: a,
      eobiEmployerContribution: b,
      deductAbsentDays: deductAbsent,
      overtimeMultiplier: ot,
      latesPerPenalty: lp,
      latePenaltyDays: pd,
      deductHalfDays: halfDays,
    });
  };

  return (
    <form onSubmit={submit} noValidate>
      <Card className="max-w-2xl">
        <CardHeader title="EOBI & attendance" description="EOBI applies to employees whose salary has EOBI turned on." />
        <CardBody className="space-y-5">
          <FormError message={error} />
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="EOBI employee share (PKR / month)" error={fieldErrors.eobiEmployeeContribution}>
              <Input type="number" min={0} inputMode="numeric" value={employee} onChange={(e) => setEmployee(e.target.value)} />
            </Field>
            <Field label="EOBI employer share (PKR / month)" error={fieldErrors.eobiEmployerContribution}>
              <Input type="number" min={0} inputMode="numeric" value={employer} onChange={(e) => setEmployer(e.target.value)} />
            </Field>
          </div>
          <Field
            label="Overtime rate (x hourly basic)"
            hint="Hourly basic = basic salary / 30 / 8. Common values: 1.5, or 2 under the Factories Act."
            error={fieldErrors.overtimeMultiplier}
          >
            <Input type="number" min={0} max={5} step="0.25" value={overtime} onChange={(e) => setOvertime(e.target.value)} className="max-w-32" />
          </Field>
          <Switch
            checked={deductAbsent}
            onChange={setDeductAbsent}
            label="Deduct absent days"
            description="Working days without attendance or approved paid leave are unpaid."
          />
          <div className="rounded-lg border border-zinc-200 p-4 dark:border-zinc-800">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Late arrival penalty</p>
            <p className="mb-3 text-xs text-zinc-500">Shown as its own deduction on the payslip. Set late arrivals to 0 to switch it off.</p>
            <div className="flex flex-wrap items-end gap-3 text-sm text-zinc-700 dark:text-zinc-300">
              <span className="pb-2">Every</span>
              <Field label="Late arrivals" error={fieldErrors.latesPerPenalty}>
                <Input type="number" min={0} max={31} value={latesPer} onChange={(e) => setLatesPer(e.target.value)} className="w-24" />
              </Field>
              <span className="pb-2">deduct</span>
              <Field label="Days of pay" error={fieldErrors.latePenaltyDays}>
                <Input type="number" min={0} max={5} step="0.5" value={penaltyDays} onChange={(e) => setPenaltyDays(e.target.value)} className="w-24" />
              </Field>
            </div>
          </div>
          <Switch
            checked={halfDays}
            onChange={setHalfDays}
            label="Deduct half days"
            description="A day where someone worked under half their shift costs half a day's pay."
          />
          <div className="flex justify-end">
            <Button type="submit" loading={save.isPending}>
              Save settings
            </Button>
          </div>
        </CardBody>
      </Card>
    </form>
  );
}
