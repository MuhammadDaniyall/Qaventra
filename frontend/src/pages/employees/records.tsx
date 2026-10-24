import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Copy, Download, Phone, Plus, Trash2, UserRoundCheck, UsersRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { errorMessage } from '../../api/client';
import { employeesApi } from '../../api/employees';
import { useDepartments, useDesignations, useEmployeeLookup, useShifts } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  Select,
  Skeleton,
  Spinner,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDate, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import type { EmergencyContact, Employee, IssueLoginsResult, JobChange, JobChangeKind } from '../../types';

// ---------- Emergency contacts ----------

const blankContact = (): EmergencyContact => ({ name: '', relation: '', phone: '', altPhone: null });

/** Who to call in an emergency. `employeeId` for HR viewing someone; omit for the signed-in employee's own list. */
export function EmergencyContactsCard({ employeeId, editable }: { employeeId?: string; editable: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = employeeId ? qk.emergencyContacts(employeeId) : qk.myEmergencyContacts;
  const contacts = useQuery({
    queryKey: key,
    queryFn: () => (employeeId ? employeesApi.emergencyContacts(employeeId) : employeesApi.myEmergencyContacts()),
  });
  const [draft, setDraft] = useState<EmergencyContact[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: (list: EmergencyContact[]) =>
      employeeId ? employeesApi.saveEmergencyContacts(employeeId, list) : employeesApi.saveMyEmergencyContacts(list),
    onSuccess: (saved) => {
      qc.setQueryData(key, saved);
      setDraft(null);
      toast.success('Emergency contacts saved');
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const set = (i: number, patch: Partial<EmergencyContact>) => setDraft((d) => d!.map((c, j) => (j === i ? { ...c, ...patch } : c)));
  const submit = () => {
    setError(null);
    const list = draft!.filter((c) => c.name.trim() || c.phone.trim() || c.relation.trim());
    if (list.some((c) => !c.name.trim() || !c.relation.trim() || !c.phone.trim())) return setError('Every contact needs a name, relation and phone number.');
    save.mutate(list.map((c) => ({ ...c, altPhone: c.altPhone?.trim() || null })));
  };

  return (
    <Card>
      <CardHeader
        title="Emergency contacts"
        description="Who to call if something happens at work. The first one is called first."
        actions={
          editable && !draft && contacts.data ? (
            <Button variant="secondary" size="sm" onClick={() => setDraft(contacts.data.length ? contacts.data : [blankContact()])}>
              {contacts.data.length ? 'Edit' : 'Add contacts'}
            </Button>
          ) : undefined
        }
      />
      {contacts.isLoading ? (
        <div className="space-y-2 p-5">
          <Skeleton className="h-10" />
          <Skeleton className="h-10" />
        </div>
      ) : contacts.isError ? (
        <ErrorState error={contacts.error} onRetry={() => contacts.refetch()} />
      ) : draft ? (
        <div className="space-y-4 p-5">
          <FormError message={error} />
          {draft.map((c, i) => (
            <div key={i} className="grid grid-cols-2 gap-3 rounded-lg border border-zinc-200 p-3 sm:grid-cols-[1.4fr_1fr_1.2fr_1.2fr_auto] dark:border-zinc-800">
              <Field label="Name" required>
                <Input value={c.name} maxLength={150} onChange={(e) => set(i, { name: e.target.value })} />
              </Field>
              <Field label="Relation" required>
                <Input value={c.relation} maxLength={50} placeholder="Brother" list="relations" onChange={(e) => set(i, { relation: e.target.value })} />
              </Field>
              <Field label="Phone" required>
                <Input type="tel" value={c.phone} maxLength={30} placeholder="0300-1234567" onChange={(e) => set(i, { phone: e.target.value })} />
              </Field>
              <Field label="Other phone">
                <Input type="tel" value={c.altPhone ?? ''} maxLength={30} onChange={(e) => set(i, { altPhone: e.target.value })} />
              </Field>
              <div className="flex items-end">
                <IconButton label="Remove contact" onClick={() => setDraft((d) => d!.filter((_, j) => j !== i))}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            </div>
          ))}
          <datalist id="relations">
            {['Father', 'Mother', 'Spouse', 'Brother', 'Sister', 'Son', 'Daughter', 'Friend'].map((r) => (
              <option key={r} value={r} />
            ))}
          </datalist>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Button variant="ghost" size="sm" icon={<Plus className="size-4" />} disabled={draft.length >= 5} onClick={() => setDraft((d) => [...d!, blankContact()])}>
              Add another
            </Button>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => setDraft(null)}>
                Cancel
              </Button>
              <Button loading={save.isPending} onClick={submit}>
                Save contacts
              </Button>
            </div>
          </div>
        </div>
      ) : !contacts.data?.length ? (
        <EmptyState icon={Phone} title="No emergency contacts" description={editable ? 'Add at least one person HR can call.' : 'None added yet.'} />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {contacts.data.map((c, i) => (
            <li key={i} className="flex flex-wrap items-center justify-between gap-2 px-5 py-3">
              <div>
                <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                  {c.name} {i === 0 && <Badge tone="accent">First to call</Badge>}
                </p>
                <p className="text-xs text-zinc-500">{c.relation}</p>
              </div>
              <div className="text-end text-sm text-zinc-700 tabular-nums dark:text-zinc-300">
                <a href={`tel:${c.phone}`} className="hover:underline">
                  {c.phone}
                </a>
                {c.altPhone && <p className="text-xs text-zinc-500">{c.altPhone}</p>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

// ---------- Job history ----------

export const JOB_CHANGE_LABELS: Record<JobChangeKind, string> = {
  Transfer: 'Transfer',
  Promotion: 'Promotion',
  Demotion: 'Demotion',
  Redesignation: 'New title',
  ManagerChange: 'New manager',
  Rehire: 'Re-hired',
  ProfileEdit: 'Profile updated',
};

const KIND_TONES: Record<JobChangeKind, 'green' | 'blue' | 'amber' | 'violet' | 'gray' | 'accent'> = {
  Promotion: 'green',
  Transfer: 'blue',
  Demotion: 'amber',
  Redesignation: 'violet',
  ManagerChange: 'accent',
  Rehire: 'green',
  ProfileEdit: 'gray',
};

function Move({ label, from, to }: { label: string; from: string | null; to: string | null }) {
  if ((from ?? '') === (to ?? '')) return null;
  return (
    <p className="flex flex-wrap items-center gap-1.5 text-sm">
      <span className="w-24 shrink-0 text-xs text-zinc-500 dark:text-zinc-400">{label}</span>
      <span className="text-zinc-500 line-through decoration-zinc-300 dark:decoration-zinc-600">{from ?? 'None'}</span>
      <ArrowRight className="size-3.5 text-zinc-500" aria-hidden="true" />
      <span className="font-medium text-zinc-900 dark:text-zinc-100">{to ?? 'None'}</span>
    </p>
  );
}

/** Newest first, ending with the day they joined. */
export function JobHistory({ employeeId, joiningDate, actions }: { employeeId?: string; joiningDate: string; actions?: React.ReactNode }) {
  const history = useQuery({
    queryKey: employeeId ? qk.jobHistory(employeeId) : qk.myJobHistory,
    queryFn: () => (employeeId ? employeesApi.jobHistory(employeeId) : employeesApi.myJobHistory()),
  });
  return (
    <Card>
      <CardHeader title="Job history" description="Transfers, promotions, new managers and re-hires." actions={actions} />
      {history.isLoading ? (
        <div className="p-5">
          <Spinner />
        </div>
      ) : history.isError ? (
        <ErrorState error={history.error} onRetry={() => history.refetch()} />
      ) : (
        <ol className="relative space-y-5 px-5 py-5">
          {history.data!.map((h: JobChange) => (
            <li key={h.id} className="relative border-s-2 border-zinc-200 ps-5 dark:border-zinc-800">
              <span className="absolute top-1 -start-[7px] size-3 rounded-full border-2 border-white bg-accent-500 dark:border-zinc-900" aria-hidden="true" />
              <div className="flex flex-wrap items-center gap-2">
                <Badge tone={KIND_TONES[h.kind]}>{JOB_CHANGE_LABELS[h.kind]}</Badge>
                <span className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{formatDate(h.effectiveDate)}</span>
                {h.recordedByName && <span className="text-xs text-zinc-500">by {h.recordedByName}</span>}
              </div>
              <div className="mt-2 space-y-1">
                <Move label="Designation" from={h.fromDesignation} to={h.toDesignation} />
                <Move label="Department" from={h.fromDepartment} to={h.toDepartment} />
                <Move label="Manager" from={h.fromManager} to={h.toManager} />
              </div>
              {h.note && <p className="mt-1.5 text-sm text-zinc-600 dark:text-zinc-400">{h.note}</p>}
            </li>
          ))}
          <li className="relative border-s-2 border-transparent ps-5">
            <span className="absolute top-1 -start-[5px] size-3 rounded-full bg-zinc-300 dark:bg-zinc-600" aria-hidden="true" />
            <p className="text-sm text-zinc-600 dark:text-zinc-400">
              <span className="font-medium text-zinc-900 dark:text-zinc-100">Joined</span> on {formatDate(joiningDate)}
              {!history.data!.length && ' · no changes since'}
            </p>
          </li>
        </ol>
      )}
    </Card>
  );
}

function JobFields({
  value,
  onChange,
  selfId,
}: {
  value: { departmentId: string | null; designationId: string | null; managerId: string | null };
  onChange: (patch: Partial<{ departmentId: string | null; designationId: string | null; managerId: string | null }>) => void;
  selfId: string;
}) {
  const departments = useDepartments();
  const designations = useDesignations();
  const managers = useEmployeeLookup();
  return (
    <>
      <Field label="Designation">
        <Select value={value.designationId ?? ''} onChange={(e) => onChange({ designationId: e.target.value || null })}>
          <option value="">None</option>
          {designations.data?.map((d) => (
            <option key={d.id} value={d.id}>
              {d.title}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="Department">
        <Select value={value.departmentId ?? ''} onChange={(e) => onChange({ departmentId: e.target.value || null })}>
          <option value="">None</option>
          {departments.data?.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="Reports to" className="sm:col-span-2">
        <Select value={value.managerId ?? ''} onChange={(e) => onChange({ managerId: e.target.value || null })}>
          <option value="">No manager</option>
          {managers.data
            ?.filter((m) => m.id !== selfId)
            .map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
        </Select>
      </Field>
    </>
  );
}

const CHANGE_KINDS: JobChangeKind[] = ['Promotion', 'Transfer', 'Redesignation', 'ManagerChange', 'Demotion'];

/** HR: promote, transfer or re-assign; the old job is kept in the history. */
export function JobChangeModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [kind, setKind] = useState<JobChangeKind>('Promotion');
  const [date, setDate] = useState(todayIso());
  const [job, setJob] = useState({ departmentId: employee.departmentId, designationId: employee.designationId, managerId: employee.managerId });
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => employeesApi.recordJobChange(employee.id, { kind, effectiveDate: date, ...job, note: note.trim() || null }),
    onSuccess: (updated) => {
      toast.success(`${JOB_CHANGE_LABELS[kind]} recorded`, `${updated.firstName}'s profile and history are updated.`);
      qc.setQueryData(qk.employee(employee.id), updated);
      qc.invalidateQueries({ queryKey: qk.employees });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Record a change for ${employee.firstName}`}
      description="Choose the new designation, department or manager. The current job is kept in the history."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Save change
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Type of change">
          <Select value={kind} onChange={(e) => setKind(e.target.value as JobChangeKind)}>
            {CHANGE_KINDS.map((k) => (
              <option key={k} value={k}>
                {JOB_CHANGE_LABELS[k]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Effective from" hint="Today or earlier">
          <Input type="date" value={date} max={todayIso()} min={employee.joiningDate} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <JobFields value={job} selfId={employee.id} onChange={(p) => setJob((j) => ({ ...j, ...p }))} />
        <Field label="Note" className="sm:col-span-2">
          <Textarea rows={2} maxLength={1000} value={note} placeholder="e.g. Annual review 2026" onChange={(e) => setNote(e.target.value)} />
        </Field>
        {kind === 'Promotion' && <p className="text-xs text-zinc-500 dark:text-zinc-400 sm:col-span-2">Raising the salary too? Add the new salary on the Salary tab with the same date.</p>}
      </div>
    </Modal>
  );
}

/** HR: bring back someone who left. Keeps their record, documents and history. */
export function RehireModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const shifts = useShifts();
  const [joining, setJoining] = useState(todayIso());
  const [status, setStatus] = useState<'Probation' | 'Active'>('Probation');
  const [job, setJob] = useState({ departmentId: employee.departmentId, designationId: employee.designationId, managerId: employee.managerId });
  const [shiftId, setShiftId] = useState(employee.shiftId ?? '');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => employeesApi.rehire(employee.id, { joiningDate: joining, status, ...job, shiftId: shiftId || null, note: note.trim() || null }),
    onSuccess: (updated) => {
      toast.success(`${updated.firstName} re-hired`, updated.userId ? 'Their login is still off. Turn it on from the Login tab.' : 'Give them a login from the Login tab.');
      qc.setQueryData(qk.employee(employee.id), updated);
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Re-hire ${employee.firstName} ${employee.lastName}`}
      description={`Left on ${employee.leavingDate ? formatDate(employee.leavingDate) : 'an unknown date'}. Their record, documents and history stay; the new joining date starts leave and service afresh.`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} icon={<UserRoundCheck className="size-4" />} onClick={() => save.mutate()}>
            Re-hire
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="New joining date" required>
          <Input type="date" value={joining} min={employee.leavingDate ?? undefined} onChange={(e) => setJoining(e.target.value)} />
        </Field>
        <Field label="Status">
          <Select value={status} onChange={(e) => setStatus(e.target.value as 'Probation' | 'Active')}>
            <option value="Probation">Probation</option>
            <option value="Active">Active</option>
          </Select>
        </Field>
        <JobFields value={job} selfId={employee.id} onChange={(p) => setJob((j) => ({ ...j, ...p }))} />
        <Field label="Shift">
          <Select value={shiftId} onChange={(e) => setShiftId(e.target.value)}>
            <option value="">Keep current</option>
            {shifts.data?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Note" className="sm:col-span-2">
          <Textarea rows={2} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
        </Field>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 sm:col-span-2">Check the salary on the Salary tab after re-hiring.</p>
      </div>
    </Modal>
  );
}

/** HR: search people who have left, then open one to re-hire. */
export function FindFormerModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const q = useDebounced(search.trim(), 300);
  const list = useQuery({
    queryKey: [...qk.employees, 'former', q],
    queryFn: () => employeesApi.list({ former: true, search: q || undefined, pageSize: 20 }),
  });
  return (
    <Modal open onClose={onClose} title="Re-hire a former employee" description="Search by name, code or CNIC.">
      <div className="space-y-3">
        <Input autoFocus value={search} placeholder="Name, code or CNIC" onChange={(e) => setSearch(e.target.value)} />
        {list.isLoading ? (
          <Spinner />
        ) : !list.data?.items.length ? (
          <p className="py-6 text-center text-sm text-zinc-500">No former employees match.</p>
        ) : (
          <ul className="max-h-80 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {list.data.items.map((e) => (
              <li key={e.id}>
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-3 px-3 py-2.5 text-start hover:bg-zinc-50 dark:hover:bg-zinc-800/60"
                  onClick={() => navigate(`/employees/${e.id}?rehire=1`)}
                >
                  <span>
                    <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{e.fullName}</span>
                    <span className="text-xs text-zinc-500">
                      {e.employeeCode} · {e.designation ?? 'No designation'} · {e.status}
                    </span>
                  </span>
                  <ArrowRight className="size-4 text-zinc-500" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}

// ---------- Logins ----------

function downloadCsv(name: string, rows: string[][]) {
  const csv = rows.map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(',')).join('\r\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

/** Shows logins just handed out. Passwords appear only here, once, when they could not be emailed. */
export function IssuedLoginsView({ result }: { result: IssueLoginsResult }) {
  const toast = useToast();
  const withPasswords = result.created.filter((c) => c.password);
  return (
    <div className="space-y-4">
      <p className="text-sm text-zinc-700 dark:text-zinc-300">
        {result.created.length} login{result.created.length === 1 ? '' : 's'} created.{' '}
        {result.emailed
          ? 'Each person was emailed their sign-in details.'
          : 'Email is not set up, so hand these temporary passwords out yourself. They are shown only now.'}{' '}
        Everyone must choose their own password when they first sign in.
      </p>
      {withPasswords.length > 0 && (
        <>
          <div className="max-h-64 overflow-y-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
            <table className="w-full text-sm">
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {withPasswords.map((c) => (
                  <tr key={c.employeeId}>
                    <td className="px-3 py-2">
                      <p className="font-medium text-zinc-900 dark:text-zinc-100">{c.name}</p>
                      <p className="text-xs text-zinc-500">{c.email}</p>
                    </td>
                    <td className="px-3 py-2 text-end font-mono text-zinc-900 dark:text-zinc-100">{c.password}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="secondary"
              size="sm"
              icon={<Download className="size-4" />}
              onClick={() => downloadCsv('logins.csv', [['Name', 'Email', 'Temporary password'], ...withPasswords.map((c) => [c.name, c.email, c.password!])])}
            >
              Download CSV
            </Button>
            <Button
              variant="secondary"
              size="sm"
              icon={<Copy className="size-4" />}
              onClick={() =>
                navigator.clipboard
                  .writeText(withPasswords.map((c) => `${c.name}\t${c.email}\t${c.password}`).join('\n'))
                  .then(() => toast.success('Copied'))
                  .catch(() => toast.error('Could not copy'))
              }
            >
              Copy
            </Button>
          </div>
        </>
      )}
      {result.skipped.length > 0 && (
        <div>
          <p className="mb-1 text-xs font-medium tracking-wide text-zinc-500 uppercase">Skipped</p>
          <ul className="space-y-1 text-sm">
            {result.skipped.map((s) => (
              <li key={s.employeeId} className="text-zinc-600 dark:text-zinc-400">
                <span className="font-medium text-zinc-800 dark:text-zinc-200">{s.name}</span> · {s.reason}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/** HR: give logins to everyone who has none yet, in one go. */
export function IssueLoginsModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const departments = useDepartments();
  const [departmentId, setDepartmentId] = useState('');
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [result, setResult] = useState<IssueLoginsResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const list = useQuery({
    queryKey: [...qk.employees, 'without-login', departmentId],
    queryFn: () => employeesApi.list({ withoutLogin: true, departmentId: departmentId || undefined, pageSize: 200 }),
  });
  const people = (list.data?.items ?? []).filter((e) => e.status === 'Active' || e.status === 'Probation');
  const ready = people.filter((e) => e.email);
  const issue = useMutation({
    mutationFn: () => employeesApi.issueLogins([...picked]),
    onSuccess: (r) => {
      setResult(r);
      qc.invalidateQueries({ queryKey: qk.employees });
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const toggle = (id: string) =>
    setPicked((p) => {
      const n = new Set(p);
      if (n.has(id)) n.delete(id);
      else n.add(id);
      return n;
    });

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={result ? (result.emailed ? 'Logins sent' : 'Logins created') : 'Send logins'}
      description={result ? undefined : 'Each person gets a login with their work email and a temporary password, and must choose their own at first sign-in.'}
      footer={
        result ? (
          <Button onClick={onClose}>Done</Button>
        ) : (
          <>
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button loading={issue.isPending} disabled={!picked.size} onClick={() => issue.mutate()}>
              Send {picked.size || ''} login{picked.size === 1 ? '' : 's'}
            </Button>
          </>
        )
      }
    >
      {result ? (
        <IssuedLoginsView result={result} />
      ) : (
        <div className="space-y-3">
          <FormError message={error} />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <Select className="w-56" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} aria-label="Department">
              <option value="">All departments</option>
              {departments.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
            {ready.length > 0 && (
              <Checkbox
                label={`Select all ${ready.length} with an email`}
                checked={ready.every((e) => picked.has(e.id))}
                onChange={(e) => setPicked(e.target.checked ? new Set(ready.map((x) => x.id)) : new Set())}
              />
            )}
          </div>
          {list.isLoading ? (
            <Spinner />
          ) : !people.length ? (
            <EmptyState icon={UsersRound} title="Everyone has a login" description="Nobody here is waiting for one." />
          ) : (
            <ul className="max-h-80 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
              {people.map((e) => (
                <li key={e.id} className="px-3 py-2">
                  <Checkbox
                    label={`${e.fullName} · ${e.employeeCode}`}
                    description={e.email ?? 'No work email: add one on the profile first'}
                    disabled={!e.email}
                    checked={picked.has(e.id)}
                    onChange={() => toggle(e.id)}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Modal>
  );
}

