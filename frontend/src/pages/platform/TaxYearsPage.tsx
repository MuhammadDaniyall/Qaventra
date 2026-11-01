import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Percent, Plus, Trash2, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { taxYearsApi, type SaveTaxYearRequest, type TaxYear } from '../../api/platform';
import {
  Button,
  Card,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate } from '../../lib/format';

/** Fraction to a percent string without float noise (0.11 → "11"). */
const pct = (rate: number) => String(Math.round(rate * 10000) / 100);

/**
 * Platform owner: the salaried income tax years and slabs every company's payroll uses. Add each year after the
 * federal budget; a payroll for a month with no tax year uses the latest slabs and warns.
 */
export function TaxYearsPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const years = useQuery({ queryKey: qk.taxYears, queryFn: taxYearsApi.list });
  const [editing, setEditing] = useState<TaxYear | 'new' | null>(null);
  const [deleting, setDeleting] = useState<TaxYear | null>(null);
  const remove = useMutation({
    mutationFn: (id: string) => taxYearsApi.remove(id),
    onSuccess: () => {
      toast.success('Tax year deleted', deleting?.code);
      qc.invalidateQueries({ queryKey: qk.taxYears });
      setDeleting(null);
    },
    onError: (err) => toast.error('Could not delete', errorMessage(err)),
  });

  return (
    <>
      <PageHeader
        title="Tax years"
        description="Salaried income tax slabs used by every company's payroll. Add each new year after the federal budget."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            Add tax year
          </Button>
        }
      />
      {years.isLoading ? (
        <Card>
          <SkeletonRows rows={4} cols={4} />
        </Card>
      ) : years.isError ? (
        <Card>
          <ErrorState error={years.error} onRetry={() => years.refetch()} />
        </Card>
      ) : !years.data?.length ? (
        <Card>
          <EmptyState icon={Percent} title="No tax years yet" description="Add the current tax year and its slabs." />
        </Card>
      ) : (
        <div className="space-y-4">
          {years.data.map((y) => (
            <Card key={y.id}>
              <CardHeader
                title={`Tax year ${y.code}`}
                description={`${formatDate(y.startDate)} – ${formatDate(y.endDate)} · surcharge ${pct(y.surchargeRate)}% above ${formatCurrency(
                  y.surchargeThreshold,
                )} · used by ${y.payrolls} payroll${y.payrolls === 1 ? '' : 's'}`}
                actions={
                  <>
                    <IconButton label={`Edit ${y.code}`} onClick={() => setEditing(y)}>
                      <Pencil className="size-4" />
                    </IconButton>
                    {y.payrolls === 0 && (
                      <IconButton label={`Delete ${y.code}`} onClick={() => setDeleting(y)}>
                        <Trash2 className="size-4" />
                      </IconButton>
                    )}
                  </>
                }
              />
              <Table>
                <THead>
                  <tr>
                    <Th>Annual taxable income</Th>
                    <Th className="text-end">Fixed tax</Th>
                    <Th className="text-end">Rate on the excess</Th>
                  </tr>
                </THead>
                <TBody>
                  {y.slabs.map((s) => (
                    <Tr key={s.fromAmount}>
                      <Td>
                        {s.toAmount == null
                          ? `Above ${formatCurrency(s.fromAmount)}`
                          : `${formatCurrency(s.fromAmount)} – ${formatCurrency(s.toAmount)}`}
                      </Td>
                      <Td className="text-end tabular-nums">{formatCurrency(s.fixedTax)}</Td>
                      <Td className="text-end tabular-nums">{pct(s.rate)}%</Td>
                    </Tr>
                  ))}
                </TBody>
              </Table>
            </Card>
          ))}
        </div>
      )}
      {editing && <TaxYearModal year={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!deleting}
        title="Delete this tax year?"
        message={<>Tax year {deleting?.code} and its slabs will be removed. No payroll has used it yet.</>}
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}

interface SlabRow {
  from: string;
  to: string;
  fixed: string;
  rate: string;
}

/** The next July–June year after the latest one, as a starting point. */
function nextYear(latest: TaxYear | undefined): {
  code: string;
  start: string;
  end: string;
} {
  const y = latest ? Number(latest.startDate.slice(0, 4)) + 1 : new Date().getFullYear();
  return {
    code: `${y}-${String((y + 1) % 100).padStart(2, '0')}`,
    start: `${y}-07-01`,
    end: `${y + 1}-06-30`,
  };
}

function TaxYearModal({ year, onClose }: { year: TaxYear | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const latest = qc.getQueryData<TaxYear[]>(qk.taxYears)?.[0];
  const suggested = nextYear(latest);
  const [code, setCode] = useState(year?.code ?? suggested.code);
  const [start, setStart] = useState(year?.startDate ?? suggested.start);
  const [end, setEnd] = useState(year?.endDate ?? suggested.end);
  const [threshold, setThreshold] = useState(String(year?.surchargeThreshold ?? latest?.surchargeThreshold ?? 0));
  const [surcharge, setSurcharge] = useState(pct(year?.surchargeRate ?? latest?.surchargeRate ?? 0));
  // A new year starts from the latest year's slabs, to edit to the new budget.
  const [slabs, setSlabs] = useState<SlabRow[]>(
    (year ?? latest)?.slabs.map((s) => ({
      from: String(s.fromAmount),
      to: s.toAmount == null ? '' : String(s.toAmount),
      fixed: String(s.fixedTax),
      rate: pct(s.rate),
    })) ?? [{ from: '0', to: '', fixed: '0', rate: '0' }],
  );
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: (b: SaveTaxYearRequest) => (year ? taxYearsApi.update(year.id, b) : taxYearsApi.create(b)),
    onSuccess: (y) => {
      toast.success(year ? 'Tax year updated' : 'Tax year added', y.code);
      qc.invalidateQueries({ queryKey: qk.taxYears });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const setSlab = (i: number, p: Partial<SlabRow>) => setSlabs((rows) => rows.map((r, j) => (j === i ? { ...r, ...p } : r)));
  const num = (v: string) => Number(v.replace(/,/g, ''));

  const submit = () => {
    setError(null);
    const parsed = slabs.map((s) => ({
      fromAmount: num(s.from),
      toAmount: s.to.trim() === '' ? null : num(s.to),
      fixedTax: num(s.fixed || '0'),
      rate: num(s.rate || '0') / 100,
    }));
    if (
      parsed.some((s) => [s.fromAmount, s.toAmount ?? 0, s.fixedTax, s.rate].some((n) => !Number.isFinite(n))) ||
      !Number.isFinite(num(threshold)) ||
      !Number.isFinite(num(surcharge))
    ) {
      setError('Check the numbers.');
      return;
    }
    save.mutate({
      code: code.trim(),
      startDate: start,
      endDate: end,
      surchargeThreshold: num(threshold),
      surchargeRate: num(surcharge) / 100,
      slabs: parsed,
    });
  };

  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={year ? `Edit tax year ${year.code}` : 'Add tax year'}
      description="Slabs run from 0 upwards; each ends where the next starts, and only the top one has no upper limit. Finalized payslips keep their tax."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="tax-year-form" loading={save.isPending}>
            {year ? 'Save' : 'Add tax year'}
          </Button>
        </>
      }
    >
      <form
        id="tax-year-form"
        noValidate
        className="space-y-5"
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <FormError message={error} />
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Code" hint="e.g. 2026-27" required>
            <Input value={code} onChange={(e) => setCode(e.target.value)} maxLength={10} />
          </Field>
          <Field label="Starts" required>
            <Input type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </Field>
          <Field label="Ends" required>
            <Input type="date" value={end} onChange={(e) => setEnd(e.target.value)} />
          </Field>
          <Field label="Surcharge above (Rs a year)">
            <Input inputMode="numeric" value={threshold} onChange={(e) => setThreshold(e.target.value)} />
          </Field>
          <Field label="Surcharge (% of tax)">
            <Input inputMode="decimal" value={surcharge} onChange={(e) => setSurcharge(e.target.value)} />
          </Field>
        </div>
        <div>
          <h3 className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Slabs</h3>
          <div className="space-y-2">
            <div className="hidden grid-cols-[1fr_1fr_1fr_1fr_2rem] gap-2 text-xs font-medium text-zinc-500 sm:grid dark:text-zinc-400">
              <span>From (Rs a year)</span>
              <span>To (blank for the top)</span>
              <span>Fixed tax (Rs)</span>
              <span>Rate on the excess (%)</span>
            </div>
            {slabs.map((s, i) => (
              <div key={i} className="grid grid-cols-2 gap-2 sm:grid-cols-[1fr_1fr_1fr_1fr_2rem]">
                <Input
                  aria-label={`Slab ${i + 1} from`}
                  inputMode="numeric"
                  value={s.from}
                  onChange={(e) => setSlab(i, { from: e.target.value })}
                />
                <Input
                  aria-label={`Slab ${i + 1} to`}
                  inputMode="numeric"
                  value={s.to}
                  onChange={(e) => setSlab(i, { to: e.target.value })}
                />
                <Input
                  aria-label={`Slab ${i + 1} fixed tax`}
                  inputMode="numeric"
                  value={s.fixed}
                  onChange={(e) => setSlab(i, { fixed: e.target.value })}
                />
                <Input
                  aria-label={`Slab ${i + 1} rate`}
                  inputMode="decimal"
                  value={s.rate}
                  onChange={(e) => setSlab(i, { rate: e.target.value })}
                />
                <IconButton
                  label={`Remove slab ${i + 1}`}
                  disabled={slabs.length === 1}
                  onClick={() => setSlabs((rows) => rows.filter((_, j) => j !== i))}
                >
                  <X className="size-4" />
                </IconButton>
              </div>
            ))}
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="mt-2"
            icon={<Plus className="size-4" />}
            onClick={() =>
              setSlabs((rows) => [
                ...rows,
                {
                  from: rows[rows.length - 1]?.to ?? '',
                  to: '',
                  fixed: '',
                  rate: '',
                },
              ])
            }
          >
            Add slab
          </Button>
        </div>
      </form>
    </Modal>
  );
}
