import { useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import {
  employeeFileApi,
  LOOKUP_KINDS,
  useLookups,
  type Dependant,
  type EmployeeFile,
  type EmployeeRecords,
  type LookupKind,
  type Qualification,
  type WorkExperience,
} from '../../api/employeeFile';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Select,
  Skeleton,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDate } from '../../lib/format';
import { cn } from '../../lib/cn';

type Key = keyof EmployeeFile;
type FieldDef = { key: Key; label: string; type?: 'date' | 'text'; max?: number; placeholder?: string };

const SECTIONS: { title: string; fields: FieldDef[] }[] = [
  {
    title: 'Personal',
    fields: [
      { key: 'fatherName', label: 'Father / husband name', max: 100 },
      { key: 'placeOfBirth', label: 'Place of birth', max: 100 },
      { key: 'nationality', label: 'Nationality', max: 100, placeholder: 'Pakistani' },
      { key: 'religion', label: 'Religion', max: 100 },
    ],
  },
  {
    title: 'Identity documents',
    fields: [
      { key: 'cnicIssuedOn', label: 'CNIC issued', type: 'date' },
      { key: 'cnicExpiresOn', label: 'CNIC expires', type: 'date' },
      { key: 'passportNo', label: 'Passport no.', max: 40 },
      { key: 'passportIssuedOn', label: 'Passport issued', type: 'date' },
      { key: 'passportExpiresOn', label: 'Passport expires', type: 'date' },
      { key: 'visaNo', label: 'Visa / iqama no.', max: 40 },
      { key: 'visaType', label: 'Visa type', max: 100 },
      { key: 'visaExpiresOn', label: 'Visa expires', type: 'date' },
      { key: 'drivingLicenseNo', label: 'Driving licence no.', max: 40 },
      { key: 'drivingLicenseExpiresOn', label: 'Licence expires', type: 'date' },
    ],
  },
  {
    title: 'Tax, social security and insurance',
    fields: [
      { key: 'ntn', label: 'NTN', max: 40 },
      { key: 'eobiNo', label: 'EOBI no.', max: 40 },
      { key: 'socialSecurityNo', label: 'Social security no.', max: 40, placeholder: 'PESSI / SESSI' },
      { key: 'insuranceProvider', label: 'Health insurance', max: 100 },
      { key: 'insurancePolicyNo', label: 'Policy no.', max: 40 },
      { key: 'insuranceExpiresOn', label: 'Cover ends', type: 'date' },
      { key: 'bankAccountTitle', label: 'Bank account title', max: 100 },
      { key: 'bankBranchCode', label: 'Bank branch code', max: 40 },
    ],
  },
  {
    title: 'Employment',
    fields: [
      { key: 'confirmationDate', label: 'Confirmed on', type: 'date' },
      { key: 'contractEndDate', label: 'Contract ends', type: 'date' },
      { key: 'punchCode', label: 'Punch code (attendance machine)', max: 40 },
    ],
  },
];

const GROUPS: { key: Key; kind: LookupKind }[] = [
  { key: 'divisionId', kind: 'Division' },
  { key: 'subDepartmentId', kind: 'SubDepartment' },
  { key: 'gradeId', kind: 'Grade' },
  { key: 'costCenterId', kind: 'CostCenter' },
  { key: 'regionId', kind: 'Region' },
  { key: 'employeeGroupId', kind: 'EmployeeGroup' },
];

/** Identity documents, tax and insurance numbers, and company grouping. HR edits; others read. */
export function EmployeeFileCard({ employeeId, departmentId, editable }: { employeeId?: string; departmentId?: string | null; editable: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = qk.employeeFile(employeeId ?? 'me');
  const file = useQuery({ queryKey: key, queryFn: () => employeeFileApi.file(employeeId) });
  const lookups = useLookups();
  const [draft, setDraft] = useState<EmployeeFile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: (b: EmployeeFile) => employeeFileApi.saveFile(employeeId!, b),
    onSuccess: (saved) => {
      qc.setQueryData(key, saved);
      qc.invalidateQueries({ queryKey: qk.lookups });
      setDraft(null);
      toast.success('Saved');
    },
    onError: (e) => setError(errorMessage(e)),
  });

  if (file.isLoading) return <Skeleton className="h-64" />;
  if (file.isError || !file.data) return <ErrorState error={file.error} onRetry={() => file.refetch()} />;
  const v = draft ?? file.data;
  const set = (k: Key, value: string | boolean | null) => setDraft({ ...v, [k]: value === '' ? null : value });
  const name = (id: string | null) => (id ? (lookups.data?.find((l) => l.id === id)?.name ?? '') : '');
  const options = (kind: LookupKind, current: string | null) =>
    (lookups.data ?? []).filter(
      (l) => l.kind === kind && (l.isActive || l.id === current) && (kind !== 'SubDepartment' || !departmentId || l.parentId === departmentId),
    );

  const show = (f: FieldDef) => {
    const val = v[f.key] as string | null;
    if (!draft) return <span>{val ? f.type === 'date' ? formatDate(val) : val : <span className="text-zinc-400">—</span>}</span>;
    return <Input type={f.type ?? 'text'} value={val ?? ''} maxLength={f.max} placeholder={f.placeholder} onChange={(e) => set(f.key, e.target.value)} />;
  };

  return (
    <Card>
      <CardHeader
        title="Employee file"
        description="Documents, numbers and company grouping. Expiry dates here also appear in the expiring documents report and alerts."
        actions={
          editable &&
          (draft ? (
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" onClick={() => (setDraft(null), setError(null))}>
                Cancel
              </Button>
              <Button size="sm" loading={save.isPending} onClick={() => (setError(null), save.mutate(draft))}>
                Save
              </Button>
            </div>
          ) : (
            <Button variant="secondary" size="sm" onClick={() => setDraft(file.data)}>
              Edit
            </Button>
          ))
        }
      />
      <div className="space-y-6 p-5">
        <FormError message={error} />
        <Section title="Company grouping">
          {GROUPS.map((g) => {
            const meta = LOOKUP_KINDS.find((k) => k.kind === g.kind)!;
            const val = v[g.key] as string | null;
            return (
              <Item key={g.key} label={meta.label}>
                {draft ? (
                  <Select value={val ?? ''} onChange={(e) => set(g.key, e.target.value)}>
                    <option value="">None</option>
                    {options(g.kind, val).map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.name}
                      </option>
                    ))}
                  </Select>
                ) : (
                  name(val) || <span className="text-zinc-400">—</span>
                )}
              </Item>
            );
          })}
        </Section>
        {SECTIONS.map((s) => (
          <Section key={s.title} title={s.title}>
            {s.fields.map((f) => (
              <Item key={f.key} label={f.label}>
                {show(f)}
              </Item>
            ))}
          </Section>
        ))}
        <Section title="Marking attendance">
          <Item label="Allowed from">
            {draft ? (
              <div className="flex flex-wrap gap-4">
                <Checkbox label="Web" checked={v.allowWebCheckIn} onChange={(e) => set('allowWebCheckIn', e.target.checked)} />
                <Checkbox label="Phone" checked={v.allowMobileCheckIn} onChange={(e) => set('allowMobileCheckIn', e.target.checked)} />
                <Checkbox label="Anywhere (no location check)" checked={v.checkInAnywhere} onChange={(e) => set('checkInAnywhere', e.target.checked)} />
              </div>
            ) : (
              [v.allowWebCheckIn && 'Web', v.allowMobileCheckIn && 'Phone', v.checkInAnywhere && 'anywhere'].filter(Boolean).join(', ') || 'Machine only'
            )}
          </Item>
          <Item label="Office network only (IP addresses)">
            {draft ? (
              <Input placeholder="203.0.113.10, 10.0.0." value={v.allowedIps ?? ''} maxLength={500} onChange={(e) => set('allowedIps', e.target.value)} />
            ) : (
              v.allowedIps || <span className="text-zinc-400">Any network</span>
            )}
          </Item>
          <Item label="Personal location (e.g. home)">
            {draft ? (
              <div className="grid grid-cols-3 gap-2">
                <Input
                  type="number"
                  step="any"
                  placeholder="Latitude"
                  aria-label="Latitude"
                  value={v.ownLatitude ?? ''}
                  onChange={(e) => setDraft({ ...v, ownLatitude: e.target.value ? Number(e.target.value) : null })}
                />
                <Input
                  type="number"
                  step="any"
                  placeholder="Longitude"
                  aria-label="Longitude"
                  value={v.ownLongitude ?? ''}
                  onChange={(e) => setDraft({ ...v, ownLongitude: e.target.value ? Number(e.target.value) : null })}
                />
                <Input
                  type="number"
                  min={20}
                  placeholder="Radius m"
                  aria-label="Radius in metres"
                  value={v.ownRadiusMeters ?? ''}
                  onChange={(e) => setDraft({ ...v, ownRadiusMeters: e.target.value ? Number(e.target.value) : null })}
                />
              </div>
            ) : v.ownLatitude != null ? (
              `${v.ownLatitude.toFixed(5)}, ${v.ownLongitude?.toFixed(5)} (${v.ownRadiusMeters ?? 200} m)`
            ) : (
              <span className="text-zinc-400">—</span>
            )}
          </Item>
        </Section>
        {(editable || v.exitTypeId || v.isBlacklisted) && (
          <Section title="Leaving">
            <Item label="Exit type">
              {draft ? (
                <Select value={v.exitTypeId ?? ''} onChange={(e) => set('exitTypeId', e.target.value)}>
                  <option value="">None</option>
                  {options('ExitType', v.exitTypeId).map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </Select>
              ) : (
                name(v.exitTypeId) || <span className="text-zinc-400">—</span>
              )}
            </Item>
            <Item label="Exit reason">
              {draft ? (
                <Input value={v.exitReason ?? ''} maxLength={500} onChange={(e) => set('exitReason', e.target.value)} />
              ) : (
                v.exitReason || <span className="text-zinc-400">—</span>
              )}
            </Item>
            <Item label="Do not re-hire">
              {draft ? (
                <div className="space-y-2">
                  <Checkbox label="On the do-not-hire list" checked={v.isBlacklisted} onChange={(e) => set('isBlacklisted', e.target.checked)} />
                  {v.isBlacklisted && (
                    <Textarea
                      rows={2}
                      placeholder="Why"
                      value={v.blacklistReason ?? ''}
                      maxLength={500}
                      onChange={(e) => set('blacklistReason', e.target.value)}
                    />
                  )}
                </div>
              ) : v.isBlacklisted ? (
                <span>
                  <Badge tone="red">Do not re-hire</Badge> {v.blacklistReason}
                </span>
              ) : (
                <span className="text-zinc-400">No</span>
              )}
            </Item>
          </Section>
        )}
      </div>
    </Card>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h3 className="mb-3 text-xs font-semibold tracking-wide text-zinc-500 uppercase">{title}</h3>
      <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">{children}</dl>
    </section>
  );
}

function Item({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="mb-1 text-xs text-zinc-500 dark:text-zinc-400">{label}</dt>
      <dd className="text-sm text-zinc-900 dark:text-zinc-100">{children}</dd>
    </div>
  );
}

// ---------- Family, education and past jobs ----------

const blankDependant = (): Dependant => ({
  name: '',
  relation: '',
  cnic: null,
  dateOfBirth: null,
  gender: null,
  isNominee: false,
  nomineeShare: null,
  coveredByInsurance: false,
});
const blankQualification = (): Qualification => ({ degree: '', institute: '', field: null, grade: null, fromYear: null, toYear: null, isHighest: false });
const blankJob = (): WorkExperience => ({ organization: '', designation: '', fromDate: null, toDate: null, lastSalary: null, reasonForLeaving: null });

/** Dependants and nominees, qualifications and previous jobs. `employeeId` omitted = the signed-in employee (who may edit their own). */
export function EmployeeRecordsCard({ employeeId, editable }: { employeeId?: string; editable: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = qk.employeeRecords(employeeId ?? 'me');
  const records = useQuery({ queryKey: key, queryFn: () => employeeFileApi.records(employeeId) });
  const [draft, setDraft] = useState<EmployeeRecords | null>(null);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: (b: EmployeeRecords) => employeeFileApi.saveRecords(employeeId, b),
    onSuccess: (saved) => {
      qc.setQueryData(key, saved);
      setDraft(null);
      toast.success('Saved');
    },
    onError: (e) => setError(errorMessage(e)),
  });
  if (records.isLoading) return <Skeleton className="h-48" />;
  if (records.isError || !records.data) return <ErrorState error={records.error} onRetry={() => records.refetch()} />;
  const v = draft ?? records.data;
  const edit = !!draft;
  const num = (s: string) => (s === '' ? null : Number(s));
  const txt = (s: string) => (s === '' ? null : s);
  function patch<K extends keyof EmployeeRecords>(list: K, i: number, p: Partial<EmployeeRecords[K][number]>) {
    setDraft((d) => ({ ...d!, [list]: (d![list] as EmployeeRecords[K][number][]).map((x, j) => (j === i ? { ...x, ...p } : x)) }));
  }
  function add<K extends keyof EmployeeRecords>(list: K, item: EmployeeRecords[K][number]) {
    setDraft((d) => ({ ...d!, [list]: [...(d![list] as EmployeeRecords[K][number][]), item] }));
  }
  function remove<K extends keyof EmployeeRecords>(list: K, i: number) {
    setDraft((d) => ({ ...d!, [list]: (d![list] as EmployeeRecords[K][number][]).filter((_, j) => j !== i) }));
  }
  const grid = 'grid gap-3 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800';
  const empty = (s: string) => <p className="text-sm text-zinc-500">{s}</p>;

  return (
    <Card>
      <CardHeader
        title="Family, education and experience"
        description="Dependants and nominees for final dues, qualifications and previous jobs."
        actions={
          editable &&
          (draft ? (
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" onClick={() => (setDraft(null), setError(null))}>
                Cancel
              </Button>
              <Button size="sm" loading={save.isPending} onClick={() => (setError(null), save.mutate(draft))}>
                Save
              </Button>
            </div>
          ) : (
            <Button variant="secondary" size="sm" onClick={() => setDraft(records.data)}>
              Edit
            </Button>
          ))
        }
      />
      <div className="space-y-6 p-5">
        <FormError message={error} />
        <section>
          <h3 className="mb-3 text-xs font-semibold tracking-wide text-zinc-500 uppercase">Dependants and nominees</h3>
          <div className="space-y-2">
            {!v.dependants.length && !edit && empty('No family members on file.')}
            {v.dependants.map((d, i) =>
              edit ? (
                <div key={i} className={cn(grid, 'sm:grid-cols-[1.4fr_1fr_1.2fr_1fr_auto]')}>
                  <Field label="Name" required>
                    <Input value={d.name} maxLength={150} onChange={(e) => patch('dependants', i, { name: e.target.value })} />
                  </Field>
                  <Field label="Relation" required>
                    <Input value={d.relation} maxLength={50} list="dep-relations" onChange={(e) => patch('dependants', i, { relation: e.target.value })} />
                  </Field>
                  <Field label="CNIC / B-form">
                    <Input value={d.cnic ?? ''} placeholder="12345-1234567-1" onChange={(e) => patch('dependants', i, { cnic: txt(e.target.value) })} />
                  </Field>
                  <Field label="Date of birth">
                    <Input type="date" value={d.dateOfBirth ?? ''} onChange={(e) => patch('dependants', i, { dateOfBirth: txt(e.target.value) })} />
                  </Field>
                  <div className="flex items-end">
                    <IconButton label="Remove" onClick={() => remove('dependants', i)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                  <div className="flex flex-wrap items-center gap-4 sm:col-span-5">
                    <Checkbox
                      label="Covered by insurance"
                      checked={d.coveredByInsurance}
                      onChange={(e) => patch('dependants', i, { coveredByInsurance: e.target.checked })}
                    />
                    <Checkbox label="Nominee" checked={d.isNominee} onChange={(e) => patch('dependants', i, { isNominee: e.target.checked })} />
                    {d.isNominee && (
                      <Input
                        className="w-28"
                        type="number"
                        min={0}
                        max={100}
                        placeholder="Share %"
                        aria-label="Nominee share"
                        value={d.nomineeShare ?? ''}
                        onChange={(e) => patch('dependants', i, { nomineeShare: num(e.target.value) })}
                      />
                    )}
                  </div>
                </div>
              ) : (
                <div key={i} className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                  <span className="font-medium">{d.name}</span>
                  <span className="text-zinc-500">{d.relation}</span>
                  {d.dateOfBirth && <span className="text-zinc-500">born {formatDate(d.dateOfBirth)}</span>}
                  {d.cnic && <span className="font-mono text-xs text-zinc-500">{d.cnic}</span>}
                  {d.isNominee && <Badge tone="accent">Nominee{d.nomineeShare != null ? ` ${d.nomineeShare}%` : ''}</Badge>}
                  {d.coveredByInsurance && <Badge>Insured</Badge>}
                </div>
              ),
            )}
            <datalist id="dep-relations">
              {['Spouse', 'Son', 'Daughter', 'Father', 'Mother', 'Brother', 'Sister'].map((r) => (
                <option key={r} value={r} />
              ))}
            </datalist>
            {edit && (
              <Button variant="ghost" size="sm" icon={<Plus className="size-4" />} onClick={() => add('dependants', blankDependant())}>
                Add family member
              </Button>
            )}
          </div>
        </section>

        <section>
          <h3 className="mb-3 text-xs font-semibold tracking-wide text-zinc-500 uppercase">Qualifications</h3>
          <div className="space-y-2">
            {!v.qualifications.length && !edit && empty('No qualifications on file.')}
            {v.qualifications.map((q, i) =>
              edit ? (
                <div key={i} className={cn(grid, 'sm:grid-cols-[1.2fr_1.4fr_1fr_6rem_6rem_6rem_auto]')}>
                  <Field label="Degree" required>
                    <Input
                      value={q.degree}
                      maxLength={150}
                      placeholder="BS, MBA, ACCA"
                      onChange={(e) => patch('qualifications', i, { degree: e.target.value })}
                    />
                  </Field>
                  <Field label="Institute" required>
                    <Input value={q.institute} maxLength={200} onChange={(e) => patch('qualifications', i, { institute: e.target.value })} />
                  </Field>
                  <Field label="Field">
                    <Input value={q.field ?? ''} maxLength={150} onChange={(e) => patch('qualifications', i, { field: txt(e.target.value) })} />
                  </Field>
                  <Field label="Grade / CGPA">
                    <Input value={q.grade ?? ''} maxLength={30} onChange={(e) => patch('qualifications', i, { grade: txt(e.target.value) })} />
                  </Field>
                  <Field label="From">
                    <Input
                      type="number"
                      min={1950}
                      max={2100}
                      value={q.fromYear ?? ''}
                      onChange={(e) => patch('qualifications', i, { fromYear: num(e.target.value) })}
                    />
                  </Field>
                  <Field label="To">
                    <Input
                      type="number"
                      min={1950}
                      max={2100}
                      value={q.toYear ?? ''}
                      onChange={(e) => patch('qualifications', i, { toYear: num(e.target.value) })}
                    />
                  </Field>
                  <div className="flex items-end">
                    <IconButton label="Remove" onClick={() => remove('qualifications', i)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                  <div className="sm:col-span-7">
                    <Checkbox
                      label="Highest qualification"
                      checked={q.isHighest}
                      onChange={(e) => patch('qualifications', i, { isHighest: e.target.checked })}
                    />
                  </div>
                </div>
              ) : (
                <div key={i} className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                  <span className="font-medium">
                    {q.degree}
                    {q.field ? ` in ${q.field}` : ''}
                  </span>
                  <span className="text-zinc-500">{q.institute}</span>
                  {(q.fromYear || q.toYear) && (
                    <span className="text-zinc-500">
                      {q.fromYear ?? ''}–{q.toYear ?? ''}
                    </span>
                  )}
                  {q.grade && <span className="text-zinc-500">{q.grade}</span>}
                  {q.isHighest && <Badge tone="accent">Highest</Badge>}
                </div>
              ),
            )}
            {edit && (
              <Button variant="ghost" size="sm" icon={<Plus className="size-4" />} onClick={() => add('qualifications', blankQualification())}>
                Add qualification
              </Button>
            )}
          </div>
        </section>

        <section>
          <h3 className="mb-3 text-xs font-semibold tracking-wide text-zinc-500 uppercase">Previous jobs</h3>
          <div className="space-y-2">
            {!v.experience.length && !edit && empty('No previous jobs on file.')}
            {v.experience.map((j, i) =>
              edit ? (
                <div key={i} className={cn(grid, 'sm:grid-cols-[1.4fr_1.2fr_9rem_9rem_auto]')}>
                  <Field label="Organization" required>
                    <Input value={j.organization} maxLength={200} onChange={(e) => patch('experience', i, { organization: e.target.value })} />
                  </Field>
                  <Field label="Designation" required>
                    <Input value={j.designation} maxLength={150} onChange={(e) => patch('experience', i, { designation: e.target.value })} />
                  </Field>
                  <Field label="From">
                    <Input type="date" value={j.fromDate ?? ''} onChange={(e) => patch('experience', i, { fromDate: txt(e.target.value) })} />
                  </Field>
                  <Field label="To">
                    <Input type="date" value={j.toDate ?? ''} onChange={(e) => patch('experience', i, { toDate: txt(e.target.value) })} />
                  </Field>
                  <div className="flex items-end">
                    <IconButton label="Remove" onClick={() => remove('experience', i)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                  <Field label="Last salary">
                    <Input type="number" min={0} value={j.lastSalary ?? ''} onChange={(e) => patch('experience', i, { lastSalary: num(e.target.value) })} />
                  </Field>
                  <div className="sm:col-span-4">
                    <Field label="Reason for leaving">
                      <Input
                        value={j.reasonForLeaving ?? ''}
                        maxLength={500}
                        onChange={(e) => patch('experience', i, { reasonForLeaving: txt(e.target.value) })}
                      />
                    </Field>
                  </div>
                </div>
              ) : (
                <div key={i} className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                  <span className="font-medium">{j.designation}</span>
                  <span className="text-zinc-500">at {j.organization}</span>
                  {(j.fromDate || j.toDate) && (
                    <span className="text-zinc-500">
                      {j.fromDate ? formatDate(j.fromDate) : '?'} – {j.toDate ? formatDate(j.toDate) : '?'}
                    </span>
                  )}
                  {j.reasonForLeaving && <span className="text-xs text-zinc-500">· {j.reasonForLeaving}</span>}
                </div>
              ),
            )}
            {edit && (
              <Button variant="ghost" size="sm" icon={<Plus className="size-4" />} onClick={() => add('experience', blankJob())}>
                Add previous job
              </Button>
            )}
          </div>
        </section>
      </div>
    </Card>
  );
}

/** One dropdown over all grouping lists (division, grade, cost center…), for filtering people. Hidden when the company has none. */
export function GroupFilter({ value, onChange, className }: { value: string; onChange: (id: string) => void; className?: string }) {
  const lookups = useLookups();
  const used = LOOKUP_KINDS.filter((k) => k.kind !== 'ExitType' && (lookups.data ?? []).some((l) => l.kind === k.kind));
  if (!used.length) return null;
  return (
    <Select aria-label="Filter by group" className={className} value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">All groups</option>
      {used.map((k) => (
        <optgroup key={k.kind} label={k.plural}>
          {(lookups.data ?? [])
            .filter((l) => l.kind === k.kind)
            .map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
        </optgroup>
      ))}
    </Select>
  );
}
