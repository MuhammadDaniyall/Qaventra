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
  const [requireApproval, setRequireApproval] = useState(initial.requireApproval ?? false);
  const [encashOnGross, setEncashOnGross] = useState(initial.encashOnGross ?? false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [x, setX] = useState({
    providentFundEnabled: initial.providentFundEnabled ?? false,
    pfEmployeePercent: String(initial.pfEmployeePercent ?? 8.33),
    pfEmployerPercent: String(initial.pfEmployerPercent ?? 8.33),
    pfOnGross: initial.pfOnGross ?? false,
    pfVestingYears: String(initial.pfVestingYears ?? 0),
    vpsEnabled: initial.vpsEnabled ?? false,
    vpsCreditCapPercent: String(initial.vpsCreditCapPercent ?? 20),
    gratuityEnabled: initial.gratuityEnabled ?? false,
    gratuityDaysPerYear: String(initial.gratuityDaysPerYear ?? 30),
    gratuityMinYears: String(initial.gratuityMinYears ?? 1),
    gratuityOnGross: initial.gratuityOnGross ?? false,
    socialSecurityPercent: String(initial.socialSecurityPercent ?? 0),
    socialSecurityWageCeiling: initial.socialSecurityWageCeiling != null ? String(initial.socialSecurityWageCeiling) : '',
    holidayOvertimeMultiplier: String(initial.holidayOvertimeMultiplier ?? 2),
    offDayOvertimeMultiplier: String(initial.offDayOvertimeMultiplier ?? 2),
    overtimeMinMinutes: String(initial.overtimeMinMinutes ?? 0),
    overtimeRoundMinutes: String(initial.overtimeRoundMinutes ?? 0),
  });
  const setXv = (p: Partial<typeof x>) => setX((v) => ({ ...v, ...p }));

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
      requireApproval,
      encashOnGross,
      providentFundEnabled: x.providentFundEnabled,
      pfEmployeePercent: Number(x.pfEmployeePercent) || 0,
      pfEmployerPercent: Number(x.pfEmployerPercent) || 0,
      pfOnGross: x.pfOnGross,
      pfVestingYears: Number(x.pfVestingYears) || 0,
      vpsEnabled: x.vpsEnabled,
      vpsCreditCapPercent: Number(x.vpsCreditCapPercent) || 0,
      gratuityEnabled: x.gratuityEnabled,
      gratuityDaysPerYear: Number(x.gratuityDaysPerYear) || 0,
      gratuityMinYears: Number(x.gratuityMinYears) || 0,
      gratuityOnGross: x.gratuityOnGross,
      socialSecurityPercent: Number(x.socialSecurityPercent) || 0,
      socialSecurityWageCeiling: x.socialSecurityWageCeiling ? Number(x.socialSecurityWageCeiling) : null,
      holidayOvertimeMultiplier: Number(x.holidayOvertimeMultiplier) || 0,
      offDayOvertimeMultiplier: Number(x.offDayOvertimeMultiplier) || 0,
      overtimeMinMinutes: Number(x.overtimeMinMinutes) || 0,
      overtimeRoundMinutes: Number(x.overtimeRoundMinutes) || 0,
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
            <p className="mb-3 text-xs text-zinc-500 dark:text-zinc-400">Shown as its own deduction on the payslip. Set late arrivals to 0 to switch it off.</p>
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
          <Switch
            checked={requireApproval}
            onChange={setRequireApproval}
            label="Company admin approves payroll"
            description="HR sends each month for approval; it can be finalized only once approved."
          />
          <Switch
            checked={encashOnGross}
            onChange={setEncashOnGross}
            label="Encash leave on gross salary"
            description="Off: a day of leave is paid at basic ÷ 30. On: at gross ÷ 30."
          />
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader title="Overtime by day" description="Rates for overtime on holidays and days off; small claims and rounding." />
        <CardBody className="grid gap-4 sm:grid-cols-2">
          <Field label="Holiday rate (x hourly basic)">
            <Input
              type="number"
              min={0}
              max={5}
              step="0.25"
              value={x.holidayOvertimeMultiplier}
              onChange={(e) => setXv({ holidayOvertimeMultiplier: e.target.value })}
            />
          </Field>
          <Field label="Day-off rate (x hourly basic)">
            <Input
              type="number"
              min={0}
              max={5}
              step="0.25"
              value={x.offDayOvertimeMultiplier}
              onChange={(e) => setXv({ offDayOvertimeMultiplier: e.target.value })}
            />
          </Field>
          <Field label="Counts from (minutes)" hint="0 = any">
            <Input type="number" min={0} max={480} value={x.overtimeMinMinutes} onChange={(e) => setXv({ overtimeMinMinutes: e.target.value })} />
          </Field>
          <Field label="Round down to (minutes)" hint="e.g. 30; 0 = exact">
            <Input type="number" min={0} max={120} value={x.overtimeRoundMinutes} onChange={(e) => setXv({ overtimeRoundMinutes: e.target.value })} />
          </Field>
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader
          title="Provident fund"
          description="Members are ticked on each person's file. Contributions go into the fund each time payroll is finalized."
        />
        <CardBody className="space-y-4">
          <Switch checked={x.providentFundEnabled} onChange={(v) => setXv({ providentFundEnabled: v })} label="Run a provident fund" />
          {x.providentFundEnabled && (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Member share (%)">
                <Input type="number" min={0} max={50} step="0.01" value={x.pfEmployeePercent} onChange={(e) => setXv({ pfEmployeePercent: e.target.value })} />
              </Field>
              <Field label="Company share (%)">
                <Input type="number" min={0} max={50} step="0.01" value={x.pfEmployerPercent} onChange={(e) => setXv({ pfEmployerPercent: e.target.value })} />
              </Field>
              <Field label="Company share vests after (years)" hint="0 = at once">
                <Input type="number" min={0} max={30} value={x.pfVestingYears} onChange={(e) => setXv({ pfVestingYears: e.target.value })} />
              </Field>
              <div className="sm:col-span-3">
                <Switch checked={x.pfOnGross} onChange={(v) => setXv({ pfOnGross: v })} label="On gross salary" description="Off: on basic salary." />
              </div>
            </div>
          )}
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader
          title="Voluntary pension (VPS)"
          description="Members and their shares are set on each person's file. The member's share is deducted and lowers their income tax (section 63); the company's share is a cost."
        />
        <CardBody className="space-y-4">
          <Switch checked={x.vpsEnabled} onChange={(v) => setXv({ vpsEnabled: v })} label="Deduct voluntary pension" />
          {x.vpsEnabled && (
            <Field label="Tax credit on up to (% of taxable pay)" hint="The law allows 20%" className="max-w-xs">
              <Input type="number" min={0} max={100} step="0.5" value={x.vpsCreditCapPercent} onChange={(e) => setXv({ vpsCreditCapPercent: e.target.value })} />
            </Field>
          )}
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader title="Gratuity" description="Due = salary ÷ 30 × days per year × years of service, once the minimum service is reached." />
        <CardBody className="space-y-4">
          <Switch checked={x.gratuityEnabled} onChange={(v) => setXv({ gratuityEnabled: v })} label="Pay gratuity" />
          {x.gratuityEnabled && (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Days of salary per year">
                <Input type="number" min={0} max={90} value={x.gratuityDaysPerYear} onChange={(e) => setXv({ gratuityDaysPerYear: e.target.value })} />
              </Field>
              <Field label="Minimum service (years)">
                <Input type="number" min={0} max={30} step="0.5" value={x.gratuityMinYears} onChange={(e) => setXv({ gratuityMinYears: e.target.value })} />
              </Field>
              <div className="sm:col-span-2">
                <Switch
                  checked={x.gratuityOnGross}
                  onChange={(v) => setXv({ gratuityOnGross: v })}
                  label="On gross salary"
                  description="Off: on basic salary."
                />
              </div>
            </div>
          )}
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader
          title="Social security (PESSI / SESSI)"
          description="Employer contribution for people with a social security number on their file. 0 turns it off."
        />
        <CardBody className="grid gap-4 sm:grid-cols-2">
          <Field label="Employer contribution (% of wages)">
            <Input
              type="number"
              min={0}
              max={50}
              step="0.01"
              value={x.socialSecurityPercent}
              onChange={(e) => setXv({ socialSecurityPercent: e.target.value })}
            />
          </Field>
          <Field label="Wage ceiling (PKR)" hint="Wages above this are not counted">
            <Input type="number" min={0} value={x.socialSecurityWageCeiling} onChange={(e) => setXv({ socialSecurityWageCeiling: e.target.value })} />
          </Field>
        </CardBody>
      </Card>
      <div className="mt-6 flex max-w-2xl justify-end">
        <Button type="submit" loading={save.isPending}>
          Save settings
        </Button>
      </div>
    </form>
  );
}
