import { useMemo, useState } from 'react';
import { GroupFilter } from '../employees/EmployeeFile';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import {
  CalendarCheck,
  Download,
  FileBarChart,
  FolderKanban,
  GraduationCap,
  Inbox,
  Palmtree,
  Plane,
  Play,
  Printer,
  Receipt,
  Search,
  Settings2,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useDepartments, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { reportsApi, type ReportDefinition, type ReportRequest, type ReportResult } from '../../api/reports';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';
import { monthLabel, todayIso } from '../../lib/format';

/** One place for every report: pick, filter, view, export to Excel or print with signature lines. */
export function ReportsPage() {
  const user = useUser();
  const hr = isHr(user);
  const list = useQuery({ queryKey: qk.reports, queryFn: reportsApi.list });
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState('');
  const [settings, setSettings] = useState(false);
  const key = params.get('report');
  const selected = list.data?.find((r) => r.key === key) ?? null;
  const groups = useMemo(() => {
    const q = search.trim().toLowerCase();
    const map = new Map<string, ReportDefinition[]>();
    for (const r of list.data ?? []) {
      if (q && !`${r.name} ${r.description} ${r.category}`.toLowerCase().includes(q)) continue;
      map.set(r.category, [...(map.get(r.category) ?? []), r]);
    }
    return [...map.entries()];
  }, [list.data, search]);

  return (
    <>
      <PageHeader
        title="Reports"
        description={hr ? 'Every report in one place. Filter, view, export to Excel or print with signatures.' : 'Reports about your team.'}
        actions={
          hr && (
            <Button variant="secondary" icon={<Settings2 className="size-4" />} onClick={() => setSettings(true)}>
              Signatures
            </Button>
          )
        }
      />
      <div className="grid gap-5 lg:grid-cols-[18rem_1fr]">
        <Card className="self-start">
          <div className="border-b border-zinc-100 p-3 dark:border-zinc-800">
            <div className="relative">
              <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" />
              <Input className="ps-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Find a report" />
            </div>
          </div>
          {list.isLoading ? (
            <SkeletonRows rows={8} cols={1} />
          ) : (
            <nav className="max-h-[70vh] overflow-y-auto px-2 py-2">
              {groups.map(([category, reports]) => {
                const Icon = categoryIcon(category);
                return (
                  <div key={category} className="mb-3">
                    <p className="flex items-center gap-2 px-2 py-1.5 text-[11px] font-semibold tracking-wide text-zinc-500 uppercase dark:text-zinc-400">
                      <Icon className="size-3.5 text-indigo-500 dark:text-indigo-400" aria-hidden="true" />
                      {category}
                      <span className="ms-auto rounded-full bg-zinc-100 px-1.5 text-[10px] leading-4 font-medium text-zinc-500 tabular-nums dark:bg-white/[0.06] dark:text-zinc-400">
                        {reports.length}
                      </span>
                    </p>
                    {reports.map((r) => {
                      const active = r.key === key;
                      return (
                        <button
                          key={r.key}
                          type="button"
                          onClick={() => setParams({ report: r.key })}
                          aria-current={active ? 'true' : undefined}
                          className={cn(
                            'relative block w-full rounded-lg px-3 py-1.5 text-start text-sm transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent-500',
                            active
                              ? 'bg-gradient-to-r from-indigo-50 to-fuchsia-50 font-medium text-indigo-700 ring-1 ring-indigo-200/60 rtl:bg-gradient-to-l dark:from-indigo-500/15 dark:to-fuchsia-500/10 dark:text-indigo-200 dark:ring-indigo-400/20'
                              : 'text-zinc-700 hover:bg-zinc-100/70 dark:text-zinc-300 dark:hover:bg-white/[0.04]',
                          )}
                        >
                          {active && (
                            <span className="absolute inset-y-1.5 start-0 w-0.5 rounded-full bg-gradient-to-b from-indigo-500 to-fuchsia-500" aria-hidden="true" />
                          )}
                          {r.name}
                        </button>
                      );
                    })}
                  </div>
                );
              })}
              {!groups.length && <p className="px-4 py-3 text-sm text-zinc-500 dark:text-zinc-400">No report matches.</p>}
            </nav>
          )}
        </Card>
        {selected ? (
          <ReportRunner key={selected.key} def={selected} company={user.companyName ?? ''} />
        ) : (
          <ReportPicker groups={groups} onPick={(k) => setParams({ report: k })} />
        )}
      </div>
      {settings && <SignaturesModal onClose={() => setSettings(false)} />}
    </>
  );
}

const categoryIcons: Record<string, LucideIcon> = {
  Employees: Users,
  Attendance: CalendarCheck,
  Leave: Palmtree,
  Payroll: Wallet,
  Projects: FolderKanban,
  Expenses: Receipt,
  Approvals: Inbox,
  Training: GraduationCap,
  Travel: Plane,
};
const categoryIcon = (category: string) => categoryIcons[category] ?? FileBarChart;

/** Before a report is chosen: each category as a card with its reports as quick picks. */
function ReportPicker({ groups, onPick }: { groups: [string, ReportDefinition[]][]; onPick: (key: string) => void }) {
  return (
    <div className="min-w-0">
      <Card className="relative mb-5 overflow-hidden p-5 sm:p-6">
        <div className="pointer-events-none absolute -end-16 -top-20 size-56 rounded-full bg-fuchsia-400/15 blur-3xl" aria-hidden="true" />
        <div className="pointer-events-none absolute -bottom-24 start-1/3 size-56 rounded-full bg-indigo-400/15 blur-3xl" aria-hidden="true" />
        <div className="relative flex items-center gap-4">
          <span className="flex size-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 to-fuchsia-500 text-white shadow-lg shadow-indigo-500/30">
            <FileBarChart className="size-5" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h2 className="text-base font-semibold text-zinc-900 dark:text-white">{tx('Pick a report')}</h2>
            <p className="text-sm text-zinc-500 dark:text-zinc-400">{tx('Choose a report on the left, set the filters and run it.')}</p>
          </div>
        </div>
      </Card>
      <div className="grid gap-4 sm:grid-cols-2 2xl:grid-cols-3">
        {groups.map(([category, reports]) => {
          const Icon = categoryIcon(category);
          return (
            <Card key={category} className="p-4">
              <div className="mb-3 flex items-center gap-2.5">
                <span className="flex size-8 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600 ring-1 ring-indigo-100 dark:bg-indigo-500/10 dark:text-indigo-300 dark:ring-indigo-400/15">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                <p className="font-semibold text-zinc-900 dark:text-white">{category}</p>
                <span className="ms-auto text-xs text-zinc-500 tabular-nums dark:text-zinc-400">
                  {txf(reports.length === 1 ? '{n} report' : '{n} reports', { n: reports.length })}
                </span>
              </div>
              <div className="flex flex-wrap gap-1.5">
                {reports.slice(0, 6).map((r) => (
                  <button
                    key={r.key}
                    type="button"
                    onClick={() => onPick(r.key)}
                    title={r.description}
                    className="rounded-lg border border-zinc-200/80 bg-white/70 px-2.5 py-1 text-xs text-zinc-700 transition-colors hover:border-indigo-300 hover:bg-indigo-50 hover:text-indigo-700 focus-visible:outline-2 focus-visible:outline-accent-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-zinc-300 dark:hover:border-indigo-400/30 dark:hover:bg-indigo-500/10 dark:hover:text-indigo-200"
                  >
                    {r.name}
                  </button>
                ))}
                {reports.length > 6 && (
                  <span className="px-1.5 py-1 text-xs text-zinc-500 dark:text-zinc-400">
                    {txf('+{n} more', { n: reports.length - 6 })}
                  </span>
                )}
              </div>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

function ReportRunner({ def, company }: { def: ReportDefinition; company: string }) {
  const toast = useToast();
  const departments = useDepartments();
  const people = useEmployeeLookup(def.filters.includes('employee'));
  const signatures = useQuery({ queryKey: qk.reportSettings, queryFn: reportsApi.settings });
  const now = new Date();
  const [f, setF] = useState({
    from: new Date(now.getFullYear(), now.getMonth(), 1).toLocaleDateString('en-CA'),
    to: todayIso(),
    date: todayIso(),
    month: `${now.getFullYear()}-${now.getMonth() + 1}`,
    // The tax certificate's year is the one the tax year (July to June) ends in.
    year: String(def.key === 'payroll.taxcertificate' && now.getMonth() >= 6 ? now.getFullYear() + 1 : now.getFullYear()),
    department: '',
    group: '',
    employee: '',
    includeLeft: false,
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const has = (x: string) => def.filters.includes(x as never);
  const request = (): ReportRequest => {
    const [y, m] = f.month.split('-').map(Number);
    return {
      from: has('period') ? f.from : null,
      to: has('period') ? f.to : null,
      date: has('date') ? f.date : null,
      year: has('month') ? y : has('year') && /^\d{4}$/.test(f.year) ? Number(f.year) : null,
      month: has('month') ? m : null,
      departmentIds: f.department ? [f.department] : [],
      employeeId: f.employee || null,
      includeLeft: f.includeLeft,
      groupIds: f.group ? [f.group] : [],
    };
  };
  const run = useMutation({ mutationFn: () => reportsApi.run(def.key, request()) });
  const excel = useMutation({
    mutationFn: () => reportsApi.excel(def.key, request(), `${def.name}.xlsx`),
    onError: (e) => toast.error(e),
  });
  const months = Array.from({ length: 12 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    return { value: `${d.getFullYear()}-${d.getMonth() + 1}`, label: monthLabel(d.getFullYear(), d.getMonth() + 1) };
  });
  const r = run.data;
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title={def.name} description={def.description} />
        <div className="flex flex-wrap items-end gap-3 px-5 py-4">
          {has('period') && (
            <>
              <Field label="From">
                <Input type="date" value={f.from} onChange={(e) => set('from', e.target.value)} />
              </Field>
              <Field label="To">
                <Input type="date" min={f.from} value={f.to} onChange={(e) => set('to', e.target.value)} />
              </Field>
            </>
          )}
          {has('date') && (
            <Field label="Date">
              <Input type="date" value={f.date} onChange={(e) => set('date', e.target.value)} />
            </Field>
          )}
          {has('month') && (
            <Field label="Month">
              <Select className="w-44" value={f.month} onChange={(e) => set('month', e.target.value)}>
                {months.map((m) => (
                  <option key={m.value} value={m.value}>
                    {m.label}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {has('year') && (
            <Field label="Year">
              <Input className="w-28" type="number" min={2000} max={2100} value={f.year} onChange={(e) => set('year', e.target.value)} />
            </Field>
          )}
          {has('department') && (
            <Field label="Department">
              <Select className="w-52" value={f.department} onChange={(e) => set('department', e.target.value)}>
                <option value="">All</option>
                {departments.data?.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {has('department') && (
            <Field label="Group">
              <GroupFilter className="w-52" value={f.group} onChange={(v) => set('group', v)} />
            </Field>
          )}
          {has('employee') && (
            <Field label="Employee">
              <Select className="w-56" value={f.employee} onChange={(e) => set('employee', e.target.value)}>
                <option value="">Everyone</option>
                {people.data?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {has('includeLeft') && (
            <Checkbox className="pb-2" label="Include people who left" checked={f.includeLeft} onChange={(e) => set('includeLeft', e.target.checked)} />
          )}
          <div className="flex gap-2 pb-0.5">
            <Button icon={<Play className="size-4" />} loading={run.isPending} onClick={() => run.mutate()}>
              Run
            </Button>
            <Button variant="secondary" icon={<Download className="size-4" />} loading={excel.isPending} onClick={() => excel.mutate()}>
              Excel
            </Button>
            <Button
              variant="secondary"
              icon={<Printer className="size-4" />}
              disabled={!r}
              onClick={() => r && printReport(r, company, signatures.data?.signatures ?? [])}
            >
              Print / PDF
            </Button>
          </div>
        </div>
        <FormError message={run.isError ? errorMessage(run.error) : null} />
      </Card>
      {run.isError ? null : r ? (
        <ResultTable r={r} />
      ) : run.isPending ? (
        <SkeletonRows rows={6} cols={5} />
      ) : (
        <Card>
          <EmptyState icon={Play} title="Set the filters and run the report" />
        </Card>
      )}
    </div>
  );
}

function ResultTable({ r }: { r: ReportResult }) {
  return (
    <Card>
      <CardHeader title={r.title} description={`${r.subtitle ?? ''}${r.subtitle ? ' · ' : ''}${r.rows.length} rows`} />
      {!r.rows.length ? (
        <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">Nothing to show for these filters.</p>
      ) : (
        <div className="max-h-[65vh] overflow-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-zinc-50 dark:bg-zinc-900">
              <tr>
                {r.headers.map((h, i) => (
                  <th
                    key={i}
                    className={cn(
                      'border-b border-zinc-200 px-3 py-2 text-xs font-semibold whitespace-nowrap text-zinc-600 dark:border-zinc-700 dark:text-zinc-300',
                      r.numeric[i] ? 'text-end' : 'text-start',
                    )}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {r.rows.map((row, i) => (
                <tr key={i} className="border-b border-zinc-100 dark:border-zinc-800">
                  {row.map((c, j) => (
                    <td key={j} className={cn('px-3 py-1.5', r.numeric[j] && 'text-end tabular-nums')}>
                      {c}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
            {r.totals && (
              <tfoot className="sticky bottom-0 bg-zinc-50 font-semibold dark:bg-zinc-900">
                <tr>
                  {r.totals.map((c, j) => (
                    <td key={j} className={cn('border-t border-zinc-300 px-3 py-2 dark:border-zinc-600', r.numeric[j] && 'text-end tabular-nums')}>
                      {c}
                    </td>
                  ))}
                </tr>
              </tfoot>
            )}
          </table>
        </div>
      )}
    </Card>
  );
}

/** Opens the report in a print-ready window (save as PDF from the print dialog), with the company's signature lines. */
function printReport(r: ReportResult, company: string, signatures: string[]) {
  const e = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const cell = (c: string, i: number, tag: 'td' | 'th') => `<${tag} class="${r.numeric[i] ? 'n' : ''}">${e(c)}</${tag}>`;
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${e(r.title)}</title>
<style>
  body{font-family:Segoe UI,Arial,sans-serif;color:#18181b;margin:24px;font-size:11px}
  h1{font-size:16px;margin:2px 0}.muted{color:#71717a}
  table{border-collapse:collapse;width:100%;margin-top:12px}
  th,td{border:1px solid #d4d4d8;padding:4px 6px;text-align:left}th{background:#f4f4f5}
  .n{text-align:right;font-variant-numeric:tabular-nums} tfoot td{font-weight:600;background:#fafafa}
  .sig{display:flex;gap:32px;margin-top:56px}.sig div{flex:1;border-top:1px solid #18181b;padding-top:4px;text-align:center}
  @page{size:${r.headers.length > 7 ? 'A4 landscape' : 'A4'};margin:12mm}
</style></head><body>
<div class="muted">${e(company)}</div><h1>${e(r.title)}</h1><div class="muted">${e(r.subtitle ?? '')} · printed ${new Date().toLocaleString()}</div>
<table><thead><tr>${r.headers.map((h, i) => cell(h, i, 'th')).join('')}</tr></thead>
<tbody>${r.rows.map((row) => `<tr>${row.map((c, i) => cell(c, i, 'td')).join('')}</tr>`).join('')}</tbody>
${r.totals ? `<tfoot><tr>${r.totals.map((c, i) => cell(c, i, 'td')).join('')}</tr></tfoot>` : ''}</table>
${signatures.length ? `<div class="sig">${signatures.map((s) => `<div>${e(s)}</div>`).join('')}</div>` : ''}
<script>window.onload=()=>{window.print()}</script></body></html>`;
  const w = window.open('', '_blank');
  if (!w) return;
  w.document.write(html);
  w.document.close();
}

function SignaturesModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const s = useQuery({ queryKey: qk.reportSettings, queryFn: reportsApi.settings });
  const [lines, setLines] = useState<string[] | null>(null);
  const value = lines ?? [...(s.data?.signatures ?? []), '', '', '', '', ''].slice(0, 5);
  const save = useMutation({
    mutationFn: () => reportsApi.saveSettings(value.map((x) => x.trim()).filter(Boolean)),
    onSuccess: () => {
      toast.success('Signatures saved');
      qc.invalidateQueries({ queryKey: qk.reportSettings });
      onClose();
    },
  });
  if (s.isError) return <ErrorState error={s.error} />;
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Signature lines"
      description="Printed at the bottom of every report, e.g. Prepared by, HR Manager, Finance."
      footer={
        <Button loading={save.isPending} onClick={() => save.mutate()}>
          Save
        </Button>
      }
    >
      <div className="space-y-2">
        {value.map((v, i) => (
          <Input
            key={i}
            aria-label={`Signature ${i + 1}`}
            value={v}
            maxLength={60}
            onChange={(e) => setLines(value.map((x, j) => (j === i ? e.target.value : x)))}
          />
        ))}
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
