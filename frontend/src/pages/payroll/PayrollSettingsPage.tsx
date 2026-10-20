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
    setFieldErrors(fe);
    if (Object.keys(fe).length) return;
    save.mutate({ eobiEmployeeContribution: a, eobiEmployerContribution: b, deductAbsentDays: deductAbsent });
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
          <Switch
            checked={deductAbsent}
            onChange={setDeductAbsent}
            label="Deduct absent days"
            description="Working days without attendance or approved paid leave are unpaid."
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
