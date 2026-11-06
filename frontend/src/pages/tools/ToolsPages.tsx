import { useEffect, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, useParams } from 'react-router-dom';
import { ArrowRight, Calculator, Coins, HandCoins, Landmark, PalmtreeIcon } from 'lucide-react';
import { publicApi } from '../../api/publicTools';
import { Card, Field, Input, Select } from '../../components/ui';
import { formatCurrency } from '../../lib/format';

interface Tool {
  slug: string;
  title: string;
  /** Page title and meta description, for search engines. */
  seoTitle: string;
  description: string;
  icon: typeof Calculator;
  render: () => ReactNode;
}

const TOOLS: Tool[] = [
  {
    slug: 'salary-tax-calculator',
    title: 'Salary income tax calculator',
    seoTitle: 'Pakistan Salary Tax Calculator 2025-26 (FBR slabs)',
    description: 'Monthly and yearly income tax on your salary with the latest FBR slabs, and your take-home pay.',
    icon: Landmark,
    render: () => <SalaryTax />,
  },
  {
    slug: 'eobi-calculator',
    title: 'EOBI contribution calculator',
    seoTitle: 'EOBI Contribution Calculator Pakistan',
    description: 'What the employee (1%) and employer (5%) pay to EOBI each month on the minimum wage.',
    icon: Coins,
    render: () => <EobiTool />,
  },
  {
    slug: 'gratuity-calculator',
    title: 'Gratuity calculator',
    seoTitle: 'Gratuity Calculator Pakistan',
    description: 'Gratuity on leaving: last salary ÷ 30 × days per year × years of service.',
    icon: HandCoins,
    render: () => <GratuityTool />,
  },
  {
    slug: 'leave-encashment-calculator',
    title: 'Leave encashment calculator',
    seoTitle: 'Leave Encashment Calculator Pakistan',
    description: 'What your unused leave is worth when paid out: monthly salary ÷ 30 × days.',
    icon: PalmtreeIcon,
    render: () => <LeaveTool />,
  },
];

function useSeo(title: string, description: string) {
  useEffect(() => {
    document.title = `${title} · Qaventra`;
    let meta = document.querySelector<HTMLMetaElement>('meta[name="description"]');
    if (!meta) {
      meta = document.createElement('meta');
      meta.name = 'description';
      document.head.appendChild(meta);
    }
    meta.content = description;
  }, [title, description]);
}

function Shell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <Link to="/tools" className="font-semibold text-zinc-900 dark:text-white">
            Qaventra <span className="font-normal text-zinc-500">· Free HR tools</span>
          </Link>
          <Link to="/login" className="text-sm font-medium text-accent-600 hover:underline">
            Sign in
          </Link>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-8">{children}</main>
      <footer className="mx-auto max-w-5xl px-4 pb-8 text-xs text-zinc-500">
        Estimates for guidance only. Rates follow the Finance Act and EOBI rules as set up in Qaventra; check with your tax advisor for your case.
      </footer>
    </div>
  );
}

function Cta() {
  return (
    <Card className="mt-8 flex flex-wrap items-center justify-between gap-4 bg-gradient-to-r from-accent-600 to-violet-600 p-6 text-white">
      <div>
        <p className="text-lg font-semibold">Run payroll with these numbers worked out for you</p>
        <p className="text-sm text-white/80">Tax, EOBI, gratuity and final settlements in one modern HR system, with AI helpers.</p>
      </div>
      <Link
        to="/login?signup=1"
        className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-semibold text-accent-700 hover:bg-white/90"
      >
        Start free trial <ArrowRight className="size-4" />
      </Link>
    </Card>
  );
}

/** /tools: the list of free calculators. */
export function ToolsIndexPage() {
  useSeo('Free HR and payroll calculators for Pakistan', 'Free salary tax, EOBI, gratuity and leave encashment calculators for Pakistan. No sign-up needed.');
  return (
    <Shell>
      <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">Free HR and payroll calculators</h1>
      <p className="mt-1 text-zinc-600 dark:text-zinc-300">For employees and HR teams in Pakistan. No sign-up needed.</p>
      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        {TOOLS.map((t) => (
          <Link key={t.slug} to={`/tools/${t.slug}`}>
            <Card className="h-full p-5 transition hover:shadow-md">
              <t.icon className="size-6 text-accent-600" />
              <h2 className="mt-3 font-semibold text-zinc-900 dark:text-white">{t.title}</h2>
              <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-300">{t.description}</p>
            </Card>
          </Link>
        ))}
      </div>
      <Cta />
    </Shell>
  );
}

/** /tools/:slug: one calculator. */
export function ToolPage() {
  const { slug } = useParams();
  const tool = TOOLS.find((t) => t.slug === slug);
  useSeo(tool?.seoTitle ?? 'Free HR tools', tool?.description ?? '');
  if (!tool) return <Navigate to="/tools" replace />;
  return (
    <Shell>
      <Link to="/tools" className="text-sm text-accent-600 hover:underline">
        ← All tools
      </Link>
      <h1 className="mt-2 text-2xl font-bold text-zinc-900 dark:text-white">{tool.title}</h1>
      <p className="mt-1 text-zinc-600 dark:text-zinc-300">{tool.description}</p>
      <div className="mt-6">{tool.render()}</div>
      <Cta />
    </Shell>
  );
}

/** Debounces typing so the public API is not called on every key press. */
function useDebounced<T>(value: T, ms = 400): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = window.setTimeout(() => setV(value), ms);
    return () => window.clearTimeout(t);
  }, [value, ms]);
  return v;
}

function Result({ rows }: { rows: [string, ReactNode, boolean?][] }) {
  return (
    <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
      {rows.map(([label, value, strong]) => (
        <div key={label} className="flex justify-between gap-4 py-2 text-sm">
          <dt className="text-zinc-500 dark:text-zinc-400">{label}</dt>
          <dd className={strong ? 'text-base font-semibold text-zinc-900 dark:text-white' : 'text-zinc-900 dark:text-white'}>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function Layout({ form, result }: { form: ReactNode; result: ReactNode }) {
  return (
    <div className="grid gap-5 md:grid-cols-2">
      <Card className="space-y-4 p-5">{form}</Card>
      <Card className="p-5">{result}</Card>
    </div>
  );
}

const num = (s: string) => (s === '' ? 0 : Math.max(0, Number(s) || 0));

function SalaryTax() {
  const [salary, setSalary] = useState('150000');
  const [year, setYear] = useState('');
  const q = useDebounced({ salary: num(salary), year });
  const res = useQuery({ queryKey: ['public', 'tax', q], queryFn: () => publicApi.salaryTax(q.salary, q.year || undefined), placeholderData: (p) => p });
  const r = res.data;
  return (
    <Layout
      form={
        <>
          <Field label="Monthly salary (PKR)">
            <Input type="number" min={0} inputMode="numeric" value={salary} onChange={(e) => setSalary(e.target.value)} />
          </Field>
          <Field label="Tax year">
            <Select value={year || r?.taxYear || ''} onChange={(e) => setYear(e.target.value)}>
              {r?.taxYears.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </Select>
          </Field>
        </>
      }
      result={
        r && (
          <Result
            rows={[
              ['Monthly income tax', formatCurrency(r.monthlyTax), true],
              ['Monthly take-home', formatCurrency(r.monthlyTakeHome), true],
              ['Yearly salary', formatCurrency(r.annualSalary)],
              ['Yearly income tax', formatCurrency(r.annualTax)],
              ['Effective tax rate', `${r.effectiveRate}%`],
              ['Your slab', r.slab ? `${Math.round(r.slab.rate * 100)}% above ${formatCurrency(r.slab.fromAmount)}` : '—'],
            ]}
          />
        )
      }
    />
  );
}

function EobiTool() {
  const [wage, setWage] = useState('');
  const w = useDebounced(num(wage));
  const res = useQuery({ queryKey: ['public', 'eobi', w], queryFn: () => publicApi.eobi(w || undefined), placeholderData: (p) => p });
  const r = res.data;
  return (
    <Layout
      form={
        <Field label="Minimum wage per month (PKR)" hint={r ? `Leave empty to use ${formatCurrency(r.minimumWage)}.` : undefined}>
          <Input type="number" min={0} value={wage} onChange={(e) => setWage(e.target.value)} />
        </Field>
      }
      result={
        r && (
          <Result
            rows={[
              ['Employee pays (1%)', formatCurrency(r.employeeMonthly), true],
              ['Employer pays (5%)', formatCurrency(r.employerMonthly), true],
              ['Total each month', formatCurrency(r.totalMonthly)],
              ['Employee per year', formatCurrency(r.employeeYearly)],
              ['Employer per year', formatCurrency(r.employerYearly)],
            ]}
          />
        )
      }
    />
  );
}

function GratuityTool() {
  const [salary, setSalary] = useState('100000');
  const [years, setYears] = useState('5');
  const [days, setDays] = useState('30');
  const q = useDebounced({ s: num(salary), y: num(years), d: num(days) });
  const res = useQuery({
    queryKey: ['public', 'gratuity', q],
    queryFn: () => publicApi.gratuity({ lastMonthlySalary: q.s, serviceYears: q.y, daysPerYear: q.d }),
    placeholderData: (p) => p,
  });
  const r = res.data;
  return (
    <Layout
      form={
        <>
          <Field label="Last monthly salary (PKR)" hint="Basic salary, or gross if your company pays gratuity on gross.">
            <Input type="number" min={0} value={salary} onChange={(e) => setSalary(e.target.value)} />
          </Field>
          <Field label="Years of service">
            <Input type="number" min={0} step="0.5" value={years} onChange={(e) => setYears(e.target.value)} />
          </Field>
          <Field label="Days of salary per year of service">
            <Input type="number" min={0} max={60} value={days} onChange={(e) => setDays(e.target.value)} />
          </Field>
        </>
      }
      result={
        r && (
          <Result
            rows={[
              ['Gratuity', formatCurrency(r.gratuity), true],
              ['Eligible', r.eligible ? 'Yes' : `No (less than ${r.minYears} year of service)`],
              ['Years counted', String(r.serviceYears)],
            ]}
          />
        )
      }
    />
  );
}

function LeaveTool() {
  const [salary, setSalary] = useState('90000');
  const [days, setDays] = useState('10');
  const q = useDebounced({ s: num(salary), d: num(days) });
  const res = useQuery({ queryKey: ['public', 'leave', q], queryFn: () => publicApi.leaveEncashment(q.s, q.d), placeholderData: (p) => p });
  const r = res.data;
  return (
    <Layout
      form={
        <>
          <Field label="Monthly salary (PKR)">
            <Input type="number" min={0} value={salary} onChange={(e) => setSalary(e.target.value)} />
          </Field>
          <Field label="Unused leave days">
            <Input type="number" min={0} max={365} value={days} onChange={(e) => setDays(e.target.value)} />
          </Field>
        </>
      }
      result={
        r && (
          <Result
            rows={[
              ['Leave encashment', formatCurrency(r.amount), true],
              ['Pay per day', formatCurrency(r.dailyRate)],
            ]}
          />
        )
      }
    />
  );
}
