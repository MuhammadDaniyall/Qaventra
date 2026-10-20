import { CalendarPlus } from 'lucide-react';
import { Button, Field, Input, Select, Textarea } from '../../components/ui';
import { billPerCycle, CYCLE_LABELS, CYCLE_MONTHS, oneCycleFrom } from '../../lib/billing';
import { formatCurrency, formatDate, todayIso } from '../../lib/format';
import { BILLING_CYCLES, type BillingCycle, type Company, type CompanyPlan } from '../../types';

/** Form state for company details + plan (all strings, parsed on submit). */
export interface CompanyFields {
  name: string;
  phone: string;
  city: string;
  address: string;
  maxEmployees: string;
  billingCycle: BillingCycle | '';
  pricePerEmployee: string;
  planPrice: string;
  subscriptionEndsOn: string;
}

export const emptyCompanyFields: CompanyFields = {
  name: '',
  phone: '',
  city: '',
  address: '',
  maxEmployees: '',
  billingCycle: '',
  pricePerEmployee: '',
  planPrice: '',
  subscriptionEndsOn: '',
};

export function fieldsFromCompany(c: Company): CompanyFields {
  const str = (n: number | null) => (n != null ? String(n) : '');
  return {
    name: c.name,
    phone: c.phone ?? '',
    city: c.city ?? '',
    address: c.address ?? '',
    maxEmployees: str(c.maxEmployees),
    billingCycle: c.billingCycle ?? '',
    pricePerEmployee: str(c.pricePerEmployee),
    planPrice: str(c.planPrice),
    subscriptionEndsOn: c.subscriptionEndsOn ?? '',
  };
}

export const blank = (s: string) => (s.trim() ? s.trim() : null);

function parseInt1(s: string): number | null | 'invalid' {
  if (!s.trim()) return null;
  const n = Number(s);
  return Number.isInteger(n) && n >= 1 && n <= 1_000_000 ? n : 'invalid';
}

function parseMoney(s: string, max: number): number | null | 'invalid' {
  if (!s.trim()) return null;
  const n = Number(s);
  return Number.isFinite(n) && n >= 0 && n <= max ? n : 'invalid';
}

export function validateCompany(v: CompanyFields): Record<string, string> {
  const fe: Record<string, string> = {};
  if (v.name.trim().length < 2) fe.name = 'Enter the company name (at least 2 characters).';
  if (parseInt1(v.maxEmployees) === 'invalid') fe.maxEmployees = 'Enter a whole number of 1 or more, or leave blank for unlimited.';
  if (parseMoney(v.pricePerEmployee, 1_000_000) === 'invalid') fe.pricePerEmployee = 'Enter an amount of 0 or more.';
  if (parseMoney(v.planPrice, 100_000_000) === 'invalid') fe.planPrice = 'Enter an amount of 0 or more.';
  return fe;
}

/** Call only after validateCompany passed. */
export function planFromFields(v: CompanyFields): CompanyPlan {
  return {
    maxEmployees: parseInt1(v.maxEmployees) as number | null,
    billingCycle: v.billingCycle || null,
    pricePerEmployee: parseMoney(v.pricePerEmployee, 1_000_000) as number | null,
    planPrice: parseMoney(v.planPrice, 100_000_000) as number | null,
    subscriptionEndsOn: v.subscriptionEndsOn || null,
  };
}

export function CompanyInfoFields({
  v,
  set,
  errors,
}: {
  v: CompanyFields;
  set: (patch: Partial<CompanyFields>) => void;
  errors: Record<string, string>;
}) {
  return (
    <>
      <Field label="Company name" required error={errors.name}>
        <Input value={v.name} maxLength={200} onChange={(e) => set({ name: e.target.value })} placeholder="Acme Textiles (Pvt) Ltd" />
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Phone" error={errors.phone}>
          <Input type="tel" value={v.phone} maxLength={30} onChange={(e) => set({ phone: e.target.value })} placeholder="042-1234567" />
        </Field>
        <Field label="City" error={errors.city}>
          <Input value={v.city} maxLength={100} onChange={(e) => set({ city: e.target.value })} placeholder="Lahore" />
        </Field>
      </div>
      <Field label="Address" error={errors.address}>
        <Textarea rows={2} value={v.address} maxLength={500} onChange={(e) => set({ address: e.target.value })} />
      </Field>
    </>
  );
}

export function PlanFields({
  v,
  set,
  errors,
  headcount,
  showStartHelper,
}: {
  v: CompanyFields;
  set: (patch: Partial<CompanyFields>) => void;
  errors: Record<string, string>;
  /** Current active employees (used when seats are unlimited). */
  headcount: number;
  showStartHelper?: boolean;
}) {
  const num = (s: string) => (s.trim() && Number.isFinite(Number(s)) ? Number(s) : null);
  const cycle = v.billingCycle || null;
  const seats = num(v.maxEmployees);
  const bill = billPerCycle({ cycle, basePrice: num(v.planPrice), pricePerEmployee: num(v.pricePerEmployee), seats, headcount });

  return (
    <>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Billing cycle" hint={cycle ? undefined : 'No cycle = no bill and payments cannot be recorded.'} error={errors.billingCycle}>
          <Select value={v.billingCycle} onChange={(e) => set({ billingCycle: e.target.value as BillingCycle | '' })}>
            <option value="">None</option>
            {BILLING_CYCLES.map((c) => (
              <option key={c} value={c}>
                {CYCLE_LABELS[c]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Licensed employees" hint="Active-employee limit. Blank = unlimited (billed on headcount)." error={errors.maxEmployees}>
          <Input type="number" min={1} inputMode="numeric" value={v.maxEmployees} onChange={(e) => set({ maxEmployees: e.target.value })} placeholder="Unlimited" />
        </Field>
        <Field label="Price per employee per month (PKR)" error={errors.pricePerEmployee}>
          <Input type="number" min={0} inputMode="decimal" value={v.pricePerEmployee} onChange={(e) => set({ pricePerEmployee: e.target.value })} placeholder="0" />
        </Field>
        <Field label="Base fee per cycle (PKR)" hint="Optional fixed amount." error={errors.planPrice}>
          <Input type="number" min={0} inputMode="decimal" value={v.planPrice} onChange={(e) => set({ planPrice: e.target.value })} placeholder="0" />
        </Field>
      </div>
      <Field label="Subscription ends on" hint="Last day users can sign in (inclusive). Blank = no expiry." error={errors.subscriptionEndsOn}>
        <div className="flex flex-wrap gap-2">
          <Input type="date" className="max-w-48" value={v.subscriptionEndsOn} onChange={(e) => set({ subscriptionEndsOn: e.target.value })} />
          {showStartHelper && cycle && (
            <Button
              variant="secondary"
              icon={<CalendarPlus className="size-4" />}
              onClick={() => set({ subscriptionEndsOn: oneCycleFrom(todayIso(), cycle) })}
            >
              Starts today for one cycle
            </Button>
          )}
        </div>
      </Field>
      <div className="flex flex-wrap items-baseline justify-between gap-2 rounded-lg bg-zinc-50 px-4 py-3 dark:bg-zinc-800/50" aria-live="polite">
        <div className="text-sm text-zinc-600 dark:text-zinc-300">
          <p className="font-medium text-zinc-900 dark:text-zinc-100">Bill per cycle</p>
          {cycle ? (
            <p className="text-xs text-zinc-500 dark:text-zinc-400">
              {formatCurrency(num(v.planPrice) ?? 0)} base + {seats ?? `${headcount} (headcount)`} seats × {formatCurrency(num(v.pricePerEmployee) ?? 0)} × {CYCLE_MONTHS[cycle]} month
              {CYCLE_MONTHS[cycle] === 1 ? '' : 's'}
            </p>
          ) : (
            <p className="text-xs text-zinc-500 dark:text-zinc-400">Choose a billing cycle to calculate.</p>
          )}
        </div>
        <p className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{bill == null ? '—' : formatCurrency(bill)}</p>
      </div>
      {v.subscriptionEndsOn && (
        <p className="text-xs text-zinc-500 dark:text-zinc-400">Users can sign in until {formatDate(v.subscriptionEndsOn)}.</p>
      )}
    </>
  );
}
