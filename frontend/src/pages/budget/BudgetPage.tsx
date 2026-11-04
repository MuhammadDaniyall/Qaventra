import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { Calculator, Check, FileText, Plus, Send, Trash2, Undo2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { budgetApi, type BudgetPlan, type BudgetStatus, type PositionInput } from '../../api/budget';
import { useDepartments, useDesignations } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useLookups } from '../../api/employeeFile';
import { RecordStamp } from '../../components/RecordStamp';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import type { Tone } from '../../components/ui/Badge';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';

const TONES: Record<BudgetStatus, Tone> = { Draft: 'gray', Submitted: 'amber', Approved: 'green', Rejected: 'red', Closed: 'blue' };

export function BudgetStatusBadge({ status }: { status: BudgetStatus }) {
  return (
    <Badge tone={TONES[status]} dot>
      {status === 'Submitted' ? 'Waiting for approval' : status === 'Rejected' ? 'Sent back' : status}
    </Badge>
  );
}

/** HR: headcount and salary plans, approved by the company admin, against who is on the roll and being hired. */
export function BudgetPage() {
  const plans = useQuery({ queryKey: qk.budgetPlans, queryFn: budgetApi.plans });
  const [params, setParams] = useSearchParams();
  const [creating, setCreating] = useState(false);
  const selected = params.get('plan') ?? plans.data?.find((p) => p.status === 'Approved')?.id ?? plans.data?.[0]?.id ?? null;
  return (
    <>
      <PageHeader
        title="Manpower budget"
        description="Approved headcount and salary cost by department and designation, against who is on the roll."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
            New budget
          </Button>
        }
      />
      {plans.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : plans.isError ? (
        <ErrorState error={plans.error} onRetry={() => plans.refetch()} />
      ) : !plans.data?.length ? (
        <Card>
          <EmptyState icon={Calculator} title="No budget yet" description="Start from your current staff and adjust the numbers for the year." />
        </Card>
      ) : (
        <div className="grid gap-5 lg:grid-cols-[16rem_1fr]">
          <Card className="self-start">
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {plans.data.map((p) => (
                <li key={p.id}>
                  <button
                    type="button"
                    onClick={() => setParams({ plan: p.id })}
                    className={cn(
                      'w-full px-4 py-3 text-start hover:bg-zinc-50 dark:hover:bg-zinc-800/40',
                      selected === p.id && 'bg-accent-50 dark:bg-accent-500/10',
                    )}
                  >
                    <p className="font-medium text-zinc-900 dark:text-white">{p.name}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {formatDate(p.fromDate)} – {formatDate(p.toDate)}
                    </p>
                    <div className="mt-1 flex items-center gap-2">
                      <BudgetStatusBadge status={p.status} />
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">{p.headcount} people</span>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          </Card>
          {selected && <PlanView key={selected} id={selected} onDeleted={() => setParams({})} />}
        </div>
      )}
      {creating && <NewPlanModal onClose={() => setCreating(false)} onCreated={(id) => setParams({ plan: id })} />}
    </>
  );
}

function NewPlanModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const year = new Date().getFullYear();
  const [name, setName] = useState(`FY ${year}-${String(year + 1).slice(2)}`);
  const [from, setFrom] = useState(`${year}-07-01`);
  const [to, setTo] = useState(`${year + 1}-06-30`);
  const [fromStaff, setFromStaff] = useState(true);
  const save = useMutation({
    mutationFn: () => budgetApi.create({ name: name.trim(), fromDate: from, toDate: to, notes: null }, fromStaff),
    onSuccess: (p) => {
      toast.success('Budget created');
      qc.invalidateQueries({ queryKey: qk.budget });
      onCreated(p.id);
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title="New budget"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!name.trim() || !from || !to} onClick={() => save.mutate()}>
            Create
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2">
          <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="From">
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="To">
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </Field>
        <Checkbox
          className="sm:col-span-2"
          label="Start from current staff"
          description="One line per department and designation, with today's headcount and average salary"
          checked={fromStaff}
          onChange={(e) => setFromStaff(e.target.checked)}
        />
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function PlanView({ id, onDeleted }: { id: string; onDeleted: () => void }) {
  const plan = useQuery({ queryKey: qk.budgetPlan(id), queryFn: () => budgetApi.plan(id) });
  if (plan.isLoading) return <SkeletonRows rows={6} cols={6} />;
  if (plan.isError) return <ErrorState error={plan.error} onRetry={() => plan.refetch()} />;
  return <Plan p={plan.data!} onDeleted={onDeleted} />;
}

type Row = {
  departmentId: string;
  designationId: string;
  headcount: string;
  monthlyCost: string;
  filledFrom: string;
  notes: string;
  code: string;
  gradeId: string;
  costCenterId: string;
  reportsToCode: string;
  salaryMin: string;
  salaryMax: string;
  jobDescription: string;
};

const NO_DETAILS = { code: '', gradeId: '', costCenterId: '', reportsToCode: '', salaryMin: '', salaryMax: '', jobDescription: '' };

function Plan({ p, onDeleted }: { p: BudgetPlan; onDeleted: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const departments = useDepartments();
  const designations = useDesignations();
  const toRows = (): Row[] =>
    p.lines.map((l) => ({
      departmentId: l.departmentId ?? '',
      designationId: l.designationId ?? '',
      headcount: String(l.headcount),
      monthlyCost: String(l.monthlyCost),
      filledFrom: l.filledFrom ?? '',
      notes: l.notes ?? '',
      code: l.code ?? '',
      gradeId: l.gradeId ?? '',
      costCenterId: l.costCenterId ?? '',
      reportsToCode: l.reportsToCode ?? '',
      salaryMin: l.salaryMin != null ? String(l.salaryMin) : '',
      salaryMax: l.salaryMax != null ? String(l.salaryMax) : '',
      jobDescription: l.jobDescription ?? '',
    }));
  const [rows, setRows] = useState<Row[]>(toRows);
  const [dirty, setDirty] = useState(false);
  const [deciding, setDeciding] = useState<boolean | null>(null);
  const [note, setNote] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [detail, setDetail] = useState<number | null>(null);
  useEffect(() => {
    setRows(toRows());
    setDirty(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p]);
  const refresh = () => qc.invalidateQueries({ queryKey: qk.budget });
  const save = useMutation({
    mutationFn: () =>
      budgetApi.savePositions(
        p.id,
        rows
          .filter((r) => Number(r.headcount) > 0)
          .map<PositionInput>((r) => ({
            departmentId: r.departmentId || null,
            designationId: r.designationId || null,
            headcount: Number(r.headcount),
            monthlyCost: Number(r.monthlyCost) || 0,
            filledFrom: r.filledFrom || null,
            notes: r.notes.trim() || null,
            code: r.code.trim() || null,
            gradeId: r.gradeId || null,
            costCenterId: r.costCenterId || null,
            reportsToCode: r.reportsToCode.trim() || null,
            salaryMin: r.salaryMin ? Number(r.salaryMin) : null,
            salaryMax: r.salaryMax ? Number(r.salaryMax) : null,
            jobDescription: r.jobDescription.trim() || null,
          })),
      ),
    onSuccess: () => {
      toast.success('Budget saved');
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const submit = useMutation({
    mutationFn: () => budgetApi.submit(p.id),
    onSuccess: () => {
      toast.success('Sent for approval');
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const decide = useMutation({
    mutationFn: (approve: boolean) => budgetApi.decide(p.id, approve, note.trim() || null),
    onSuccess: (_, approve) => {
      toast.success(approve ? 'Budget approved' : 'Sent back');
      setDeciding(null);
      refresh();
    },
  });
  const close = useMutation({ mutationFn: () => budgetApi.close(p.id), onSuccess: refresh, onError: (e) => toast.error(e) });
  const remove = useMutation({
    mutationFn: () => budgetApi.remove(p.id),
    onSuccess: () => {
      setDeleting(false);
      refresh();
      onDeleted();
    },
    onError: (e) => toast.error(e),
  });
  const set = (i: number, patch: Partial<Row>) => {
    setRows((x) => x.map((r, j) => (j === i ? { ...r, ...patch } : r)));
    setDirty(true);
  };
  const planned = rows.reduce((a, r) => a + (Number(r.headcount) || 0), 0);
  const tile = (label: string, value: string | number, tone?: string) => (
    <Card className="p-4">
      <p className={cn('text-xl font-semibold tabular-nums', tone ?? 'text-zinc-900 dark:text-white')}>{value}</p>
      <p className="text-xs text-zinc-500 dark:text-zinc-400">{label}</p>
    </Card>
  );
  return (
    <div className="space-y-5">
      <Card className="flex flex-wrap items-center gap-3 px-5 py-4">
        <div className="min-w-0 flex-1">
          <p className="flex flex-wrap items-center gap-2 text-lg font-semibold text-zinc-900 dark:text-white">
            {p.name} <BudgetStatusBadge status={p.status} />
          </p>
          <p className="text-sm text-zinc-500 dark:text-zinc-400">
            {formatDate(p.fromDate)} – {formatDate(p.toDate)}
            {p.decidedByName && ` · ${p.status === 'Rejected' ? 'sent back' : 'approved'} by ${p.decidedByName}, ${formatDateTimeUtc(p.decidedAtUtc)}`}
          </p>
          {p.decisionNote && <p className="text-sm text-zinc-600 dark:text-zinc-300">“{p.decisionNote}”</p>}
          <RecordStamp stamps={p.stamps} className="mt-1" />
        </div>
        <div className="flex flex-wrap gap-2">
          {p.canEdit && (
            <>
              <Button size="sm" variant="secondary" disabled={!dirty} loading={save.isPending} onClick={() => save.mutate()}>
                Save
              </Button>
              <Button
                size="sm"
                icon={<Send className="size-3.5" />}
                disabled={dirty || !p.lines.length}
                loading={submit.isPending}
                onClick={() => submit.mutate()}
              >
                Send for approval
              </Button>
              <IconButton label="Delete budget" onClick={() => setDeleting(true)}>
                <Trash2 className="size-4" />
              </IconButton>
            </>
          )}
          {p.canDecide && (
            <>
              <Button size="sm" variant="success" icon={<Check className="size-3.5" />} onClick={() => setDeciding(true)}>
                Approve
              </Button>
              <Button size="sm" variant="secondary" icon={<Undo2 className="size-3.5" />} onClick={() => setDeciding(false)}>
                Send back
              </Button>
            </>
          )}
          {p.canClose && (
            <Button size="sm" variant="ghost" loading={close.isPending} onClick={() => close.mutate()}>
              Close
            </Button>
          )}
        </div>
      </Card>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {tile('Approved headcount', p.headcount)}
        {tile('On the roll', p.onRoll)}
        {tile('Vacant', p.vacant, p.vacant ? 'text-amber-600' : undefined)}
        {tile('Being hired', p.hiring)}
        {tile('Budget cost', formatCurrency(p.budgetCost))}
        {tile('Roll cost (if unchanged)', formatCurrency(p.actualCost), p.actualCost > p.budgetCost ? 'text-red-600' : undefined)}
      </div>

      <Card>
        <CardHeader title="Positions" description={p.canEdit ? `${planned} planned. Monthly cost is gross per person.` : undefined} />
        <div className="overflow-x-auto">
          <Table>
            <THead>
              <Tr>
                <Th>Department</Th>
                <Th>Designation</Th>
                <Th className="w-24 text-end">Approved</Th>
                <Th className="w-36 text-end">Monthly cost</Th>
                {p.canEdit && <Th className="w-40">New from</Th>}
                <Th className="text-end">On roll</Th>
                <Th className="text-end">Hiring</Th>
                <Th className="text-end">Vacant</Th>
                <Th className="text-end">Budget</Th>
                <Th className="text-end">Variance</Th>
                <Th>Details</Th>
                {p.canEdit && <Th />}
              </Tr>
            </THead>
            <TBody>
              {rows.map((r, i) => {
                const line = p.lines.find((l) => (l.departmentId ?? '') === r.departmentId && (l.designationId ?? '') === r.designationId);
                return (
                  <Tr key={i}>
                    <Td>
                      {p.canEdit ? (
                        <Select aria-label="Department" value={r.departmentId} onChange={(e) => set(i, { departmentId: e.target.value })}>
                          <option value="">No department</option>
                          {departments.data?.map((d) => (
                            <option key={d.id} value={d.id}>
                              {d.name}
                            </option>
                          ))}
                        </Select>
                      ) : (
                        line?.department
                      )}
                    </Td>
                    <Td>
                      {p.canEdit ? (
                        <Select aria-label="Designation" value={r.designationId} onChange={(e) => set(i, { designationId: e.target.value })}>
                          <option value="">All designations</option>
                          {designations.data?.map((d) => (
                            <option key={d.id} value={d.id}>
                              {d.title}
                            </option>
                          ))}
                        </Select>
                      ) : (
                        line?.designation
                      )}
                    </Td>
                    <Td className="text-end tabular-nums">
                      {p.canEdit ? (
                        <Input
                          aria-label="Approved headcount"
                          type="number"
                          min={0}
                          value={r.headcount}
                          onChange={(e) => set(i, { headcount: e.target.value })}
                        />
                      ) : (
                        line?.headcount
                      )}
                    </Td>
                    <Td className="text-end tabular-nums">
                      {p.canEdit ? (
                        <Input
                          aria-label="Monthly cost"
                          type="number"
                          min={0}
                          value={r.monthlyCost}
                          onChange={(e) => set(i, { monthlyCost: e.target.value })}
                        />
                      ) : (
                        formatCurrency(line?.monthlyCost)
                      )}
                    </Td>
                    {p.canEdit && (
                      <Td>
                        <Input
                          aria-label="New from"
                          type="date"
                          min={p.fromDate}
                          max={p.toDate}
                          value={r.filledFrom}
                          onChange={(e) => set(i, { filledFrom: e.target.value })}
                        />
                      </Td>
                    )}
                    <Td className="text-end tabular-nums">{line?.onRoll ?? '—'}</Td>
                    <Td className="text-end tabular-nums">{line?.hiring ?? '—'}</Td>
                    <Td className={cn('text-end tabular-nums', !!line?.vacant && 'text-amber-600')}>{line?.vacant ?? '—'}</Td>
                    <Td className="text-end tabular-nums">{line ? formatCurrency(line.budgetCost) : '—'}</Td>
                    <Td className={cn('text-end tabular-nums', line && line.variance < 0 && 'text-red-600')}>{line ? formatCurrency(line.variance) : '—'}</Td>
                    <Td>
                      <IconButton label={p.canEdit ? 'Position details' : 'View position details'} onClick={() => setDetail(i)}>
                        <FileText className="size-4" />
                      </IconButton>
                      {r.code && <span className="ms-1 font-mono text-xs text-zinc-500 dark:text-zinc-400">{r.code}</span>}
                    </Td>
                    {p.canEdit && (
                      <Td>
                        <IconButton
                          label="Remove line"
                          onClick={() => {
                            setRows((x) => x.filter((_, j) => j !== i));
                            setDirty(true);
                          }}
                        >
                          <Trash2 className="size-4" />
                        </IconButton>
                      </Td>
                    )}
                  </Tr>
                );
              })}
            </TBody>
          </Table>
        </div>
        {p.canEdit && (
          <div className="border-t border-zinc-100 px-5 py-3 dark:border-zinc-800">
            <Button
              size="sm"
              variant="ghost"
              icon={<Plus className="size-3.5" />}
              onClick={() => {
                setRows((x) => [
                  ...x,
                  {
                    departmentId: '',
                    designationId: '',
                    headcount: '1',
                    monthlyCost: '',
                    filledFrom: todayIso() > p.fromDate ? todayIso() : p.fromDate,
                    notes: '',
                    ...NO_DETAILS,
                  },
                ]);
                setDirty(true);
              }}
            >
              Add position
            </Button>
          </div>
        )}
      </Card>

      {detail !== null && rows[detail] && (
        <PositionDetails
          row={rows[detail]}
          codes={rows.map((r) => r.code.trim()).filter((c, j) => c && j !== detail)}
          readOnly={!p.canEdit}
          onSave={(patch) => {
            set(detail, patch);
            setDetail(null);
          }}
          onClose={() => setDetail(null)}
        />
      )}
      {p.unplanned.length > 0 && (
        <Card>
          <CardHeader title="On the roll but not in the budget" description="People in departments and designations the budget does not cover" />
          <Table>
            <TBody>
              {p.unplanned.map((u, i) => (
                <Tr key={i}>
                  <Td>{u.department}</Td>
                  <Td>{u.designation}</Td>
                  <Td className="text-end tabular-nums">{u.onRoll} people</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(u.monthlyCost)} average</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        </Card>
      )}

      {deciding !== null && (
        <Modal
          open
          onClose={() => setDeciding(null)}
          size="sm"
          title={deciding ? 'Approve this budget?' : 'Send the budget back'}
          footer={
            <Button
              variant={deciding ? 'success' : 'primary'}
              disabled={!deciding && !note.trim()}
              loading={decide.isPending}
              onClick={() => decide.mutate(deciding)}
            >
              {deciding ? 'Approve' : 'Send back'}
            </Button>
          }
        >
          <Field label={deciding ? 'Note (optional)' : 'What should change?'} required={!deciding}>
            <Textarea rows={3} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
          </Field>
          <FormError message={decide.isError ? errorMessage(decide.error) : null} />
        </Modal>
      )}
      <ConfirmDialog
        open={deleting}
        title="Delete this budget?"
        message="This cannot be undone."
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setDeleting(false)}
      />
    </div>
  );
}

/** Code, grade, cost center, who it reports to, salary band and job description of one position. Saved with the budget. */
function PositionDetails({
  row,
  codes,
  readOnly,
  onSave,
  onClose,
}: {
  row: Row;
  codes: string[];
  readOnly: boolean;
  onSave: (patch: Partial<Row>) => void;
  onClose: () => void;
}) {
  const lookups = useLookups();
  const [f, setF] = useState({
    code: row.code,
    gradeId: row.gradeId,
    costCenterId: row.costCenterId,
    reportsToCode: row.reportsToCode,
    salaryMin: row.salaryMin,
    salaryMax: row.salaryMax,
    jobDescription: row.jobDescription,
  });
  const set = (k: keyof typeof f, v: string) => setF((x) => ({ ...x, [k]: v }));
  const of = (kind: string) => (lookups.data ?? []).filter((l) => l.kind === kind && (l.isActive || l.id === f.gradeId || l.id === f.costCenterId));
  const bandWrong = !!f.salaryMin && !!f.salaryMax && Number(f.salaryMax) < Number(f.salaryMin);
  return (
    <Modal
      open
      onClose={onClose}
      title="Position details"
      footer={
        readOnly ? (
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        ) : (
          <>
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button disabled={bandWrong} onClick={() => onSave(f)}>
              Done
            </Button>
          </>
        )
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Position code">
          <Input value={f.code} maxLength={50} disabled={readOnly} placeholder="SAL-01" onChange={(e) => set('code', e.target.value)} />
        </Field>
        <Field label="Reports to" hint="Code of another position">
          <Select value={f.reportsToCode} disabled={readOnly} onChange={(e) => set('reportsToCode', e.target.value)}>
            <option value="">Nobody</option>
            {codes.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Grade">
          <Select value={f.gradeId} disabled={readOnly} onChange={(e) => set('gradeId', e.target.value)}>
            <option value="">None</option>
            {of('Grade').map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Cost center">
          <Select value={f.costCenterId} disabled={readOnly} onChange={(e) => set('costCenterId', e.target.value)}>
            <option value="">None</option>
            {of('CostCenter').map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Salary from (Rs / month)">
          <Input type="number" min={0} value={f.salaryMin} disabled={readOnly} onChange={(e) => set('salaryMin', e.target.value)} />
        </Field>
        <Field label="Salary up to" error={bandWrong ? 'Below the bottom of the band.' : undefined}>
          <Input type="number" min={0} value={f.salaryMax} disabled={readOnly} onChange={(e) => set('salaryMax', e.target.value)} />
        </Field>
        <Field label="Job description" className="col-span-2">
          <Textarea rows={5} maxLength={4000} value={f.jobDescription} disabled={readOnly} onChange={(e) => set('jobDescription', e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
