import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { BellRing, Download, Eye, FileBarChart, History, Play, Plus, Send } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { downloadFile } from '../../api/excel';
import { useDepartments, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  ALERT_KINDS,
  REPORT_KINDS,
  scheduledApi,
  WEEKDAYS,
  type AlertKind,
  type ReportFrequency,
  type ReportKind,
  type ReportTable,
  type SaveReport,
  type ScheduledAlert,
  type ScheduledReport,
} from '../../api/scheduled';
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
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate, formatDateTimeUtc } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';

type Tab = 'alerts' | 'reports' | 'history';

const hourLabel = (h: number) => `${h % 12 === 0 ? 12 : h % 12}:00 ${h < 12 ? 'am' : 'pm'}`;

/** HR: reminders the system sends by itself, and reports emailed on a schedule. */
export function ScheduledPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'alerts';
  return (
    <>
      <PageHeader title="Alerts and reports" description="Reminders and reports that go out on their own, by notification and email." />
      <Tabs<Tab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams({ tab: t })}
        items={[
          { value: 'alerts', label: 'Alerts' },
          { value: 'reports', label: 'Scheduled reports' },
          { value: 'history', label: 'History' },
        ]}
      />
      {tab === 'alerts' && <AlertsTab />}
      {tab === 'reports' && <ReportsTab />}
      {tab === 'history' && <HistoryTab />}
    </>
  );
}

// ---------- Alerts ----------

function AlertsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const alerts = useQuery({ queryKey: qk.scheduledAlerts, queryFn: scheduledApi.alerts });
  const [editing, setEditing] = useState<ScheduledAlert | 'new' | null>(null);
  const [deleting, setDeleting] = useState<ScheduledAlert | null>(null);
  const [preview, setPreview] = useState<{ name: string; titles: string[]; count: number } | null>(null);
  const run = useMutation({
    mutationFn: scheduledApi.runAlert,
    onSuccess: (r) => {
      toast.success(r.people ? `Sent about ${r.people} ${r.people === 1 ? 'person' : 'people'}` : 'Nobody to alert about today');
      qc.invalidateQueries({ queryKey: qk.scheduled });
    },
    onError: (e) => toast.error(e),
  });
  const look = useMutation({
    mutationFn: async (a: ScheduledAlert) => ({ name: a.name, ...(await scheduledApi.previewAlert(a.id)) }),
    onSuccess: setPreview,
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader
        title="Alerts"
        description="Checked every 15 minutes; each alert goes out once a day after its hour."
        actions={
          <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            New alert
          </Button>
        }
      />
      {alerts.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : alerts.isError ? (
        <ErrorState error={alerts.error} onRetry={() => alerts.refetch()} />
      ) : !alerts.data?.length ? (
        <EmptyState
          icon={BellRing}
          title="No alerts yet"
          description="For example: birthday wishes for everyone, probation reminders to managers, expiring CNICs to HR."
        />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Alert</Th>
              <Th>When</Th>
              <Th>To</Th>
              <Th>Last sent</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {alerts.data.map((a) => (
              <Tr key={a.id}>
                <Td>
                  <p className="flex items-center gap-2 font-medium text-zinc-900 dark:text-white">
                    {a.name} {!a.isActive && <Badge>Off</Badge>}
                  </p>
                  <p className="text-xs text-zinc-500">
                    {ALERT_KINDS[a.kind].label} · “{a.titleTemplate}”
                  </p>
                </Td>
                <Td className="text-sm">
                  {a.kind === 'NotCheckedIn' ? 'Same day' : a.daysBefore ? `${a.daysBefore} days before` : 'On the day'}
                  <p className="text-xs text-zinc-500">at {hourLabel(a.sendHour)}</p>
                </Td>
                <Td className="text-sm">
                  {[
                    a.toEmployee && 'the person',
                    a.toManager && 'manager',
                    a.toHr && 'HR',
                    a.toEveryone && 'everyone',
                    a.extraRecipientIds.length && `${a.extraRecipientIds.length} more`,
                  ]
                    .filter(Boolean)
                    .join(', ')}
                </Td>
                <Td className="text-sm">{a.lastRunOn ? formatDate(a.lastRunOn) : '—'}</Td>
                <Td>
                  <div className="flex justify-end gap-1">
                    <IconButton label="Who is it about today" onClick={() => look.mutate(a)}>
                      <Eye className="size-4" />
                    </IconButton>
                    <IconButton label="Send now" onClick={() => run.mutate(a.id)}>
                      <Play className="size-4" />
                    </IconButton>
                    <RowActions label={a.name} onEdit={() => setEditing(a)} onDelete={() => setDeleting(a)} />
                  </div>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <AlertModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name={deleting?.name ?? ''}
        remove={scheduledApi.deleteAlert}
        invalidate={[qk.scheduledAlerts]}
        onClose={() => setDeleting(null)}
      />
      {preview && (
        <Modal open onClose={() => setPreview(null)} title={preview.name} description={`${preview.count} today`}>
          {preview.titles.length ? (
            <ul className="list-disc space-y-1 pl-5 text-sm">
              {preview.titles.map((t, i) => (
                <li key={i}>{t}</li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-zinc-500">Nobody to alert about today.</p>
          )}
        </Modal>
      )}
    </Card>
  );
}

function PeoplePicker({ value, onChange, label, hint }: { value: string[]; onChange: (ids: string[]) => void; label: string; hint?: string }) {
  const people = useEmployeeLookup();
  const [search, setSearch] = useState('');
  const shown = people.data?.filter((p) => value.includes(p.id) || (search && p.name.toLowerCase().includes(search.toLowerCase()))) ?? [];
  return (
    <Field label={label} hint={hint ?? (value.length ? `${value.length} chosen` : undefined)}>
      <div>
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search people to add" />
        {shown.length > 0 && (
          <ul className="mt-2 max-h-40 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {shown.map((p) => (
              <li key={p.id} className="px-3 py-1.5">
                <Checkbox
                  label={p.name}
                  checked={value.includes(p.id)}
                  onChange={(e) => onChange(e.target.checked ? [...value, p.id] : value.filter((x) => x !== p.id))}
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </Field>
  );
}

function DepartmentPicker({ value, onChange }: { value: string[]; onChange: (ids: string[]) => void }) {
  const departments = useDepartments();
  return (
    <Field label="Departments" hint={value.length ? `${value.length} chosen` : 'None chosen: everyone'}>
      <div className="flex flex-wrap gap-x-4 gap-y-1.5">
        {departments.data?.map((d) => (
          <Checkbox
            key={d.id}
            label={d.name}
            checked={value.includes(d.id)}
            onChange={(e) => onChange(e.target.checked ? [...value, d.id] : value.filter((x) => x !== d.id))}
          />
        ))}
      </div>
    </Field>
  );
}

function AlertModal({ item, onClose }: { item: ScheduledAlert | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [f, setF] = useState({
    name: item?.name ?? ALERT_KINDS.Birthday.label,
    kind: (item?.kind ?? 'Birthday') as AlertKind,
    daysBefore: String(item?.daysBefore ?? 0),
    probationMonths: String(item?.probationMonths ?? 3),
    sendHour: String(item?.sendHour ?? 9),
    toEmployee: item?.toEmployee ?? true,
    toManager: item?.toManager ?? false,
    toHr: item?.toHr ?? false,
    toEveryone: item?.toEveryone ?? false,
    extra: item?.extraRecipientIds ?? [],
    departments: item?.departmentIds ?? [],
    title: item?.titleTemplate ?? ALERT_KINDS.Birthday.title,
    body: item?.bodyTemplate ?? ALERT_KINDS.Birthday.body,
    email: item?.sendEmail ?? true,
    active: item?.isActive ?? true,
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const changeKind = (k: AlertKind) =>
    setF((x) => {
      const fresh = !item;
      const defaults = ALERT_KINDS[k];
      return {
        ...x,
        kind: k,
        name: fresh ? defaults.label : x.name,
        title: fresh ? defaults.title : x.title,
        body: fresh ? defaults.body : x.body,
        daysBefore: fresh ? (k === 'ProbationEnd' || k === 'DocumentExpiry' ? '7' : k === 'NewJoiner' || k === 'LeaveStarting' ? '1' : '0') : x.daysBefore,
        toEmployee: fresh ? k === 'Birthday' || k === 'WorkAnniversary' || k === 'DocumentExpiry' : x.toEmployee,
        toManager: fresh ? k !== 'Birthday' : x.toManager,
        toHr: fresh ? k === 'ProbationEnd' || k === 'DocumentExpiry' || k === 'NotCheckedIn' : x.toHr,
        toEveryone: fresh ? k === 'Birthday' || k === 'WorkAnniversary' : x.toEveryone,
        sendHour: fresh ? (k === 'NotCheckedIn' ? '11' : '9') : x.sendHour,
      };
    });
  const save = useMutation({
    mutationFn: () => {
      const b = {
        name: f.name.trim(),
        kind: f.kind,
        daysBefore: Number(f.daysBefore) || 0,
        probationMonths: Number(f.probationMonths) || 3,
        sendHour: Number(f.sendHour),
        toEmployee: f.toEmployee,
        toManager: f.toManager,
        toHr: f.toHr,
        toEveryone: f.toEveryone,
        extraRecipientIds: f.extra,
        departmentIds: f.departments,
        titleTemplate: f.title.trim(),
        bodyTemplate: f.body.trim() || null,
        sendEmail: f.email,
        isActive: f.active,
      };
      return item ? scheduledApi.updateAlert(item.id, b) : scheduledApi.addAlert(b);
    },
    onSuccess: () => {
      toast.success('Alert saved');
      qc.invalidateQueries({ queryKey: qk.scheduled });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={item ? 'Edit alert' : 'New alert'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.name.trim() || !f.title.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="About" hint={ALERT_KINDS[f.kind].hint}>
          <Select value={f.kind} onChange={(e) => changeKind(e.target.value as AlertKind)}>
            {(Object.keys(ALERT_KINDS) as AlertKind[]).map((k) => (
              <option key={k} value={k}>
                {ALERT_KINDS[k].label}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Name" required>
          <Input value={f.name} maxLength={150} onChange={(e) => set('name', e.target.value)} />
        </Field>
        {f.kind !== 'NotCheckedIn' && (
          <Field label="Days before" hint="0 = on the day">
            <Input type="number" min={0} max={90} value={f.daysBefore} onChange={(e) => set('daysBefore', e.target.value)} />
          </Field>
        )}
        <Field label="Send at">
          <Select value={f.sendHour} onChange={(e) => set('sendHour', e.target.value)}>
            {Array.from({ length: 24 }, (_, h) => (
              <option key={h} value={h}>
                {hourLabel(h)}
              </option>
            ))}
          </Select>
        </Field>
        {f.kind === 'ProbationEnd' && (
          <Field label="Probation length (months)">
            <Input type="number" min={1} max={24} value={f.probationMonths} onChange={(e) => set('probationMonths', e.target.value)} />
          </Field>
        )}
        <Field label="Title" required className="sm:col-span-2" hint="{name}, {firstname}, {date}, {years}, {document}, {details}, {department}, {days}">
          <Input value={f.title} maxLength={200} onChange={(e) => set('title', e.target.value)} />
        </Field>
        <Field label="Message" className="sm:col-span-2">
          <Textarea rows={2} value={f.body} maxLength={2000} onChange={(e) => set('body', e.target.value)} />
        </Field>
        <div className="space-y-2 sm:col-span-2">
          <p className="text-sm font-medium text-zinc-800 dark:text-zinc-200">Send to</p>
          <div className="flex flex-wrap gap-x-5 gap-y-2">
            <Checkbox label="The person" checked={f.toEmployee} onChange={(e) => set('toEmployee', e.target.checked)} />
            <Checkbox label="Their manager" checked={f.toManager} onChange={(e) => set('toManager', e.target.checked)} />
            <Checkbox label="HR" checked={f.toHr} onChange={(e) => set('toHr', e.target.checked)} />
            <Checkbox label="Everyone" checked={f.toEveryone} onChange={(e) => set('toEveryone', e.target.checked)} />
          </div>
        </div>
        <div className="sm:col-span-2">
          <PeoplePicker label="Also send to" value={f.extra} onChange={(v) => set('extra', v)} />
        </div>
        <div className="sm:col-span-2">
          <DepartmentPicker value={f.departments} onChange={(v) => set('departments', v)} />
        </div>
        <div className="flex flex-wrap gap-x-5 gap-y-2 sm:col-span-2">
          <Checkbox label="Also email (for people with email turned on)" checked={f.email} onChange={(e) => set('email', e.target.checked)} />
          <Checkbox label="On" checked={f.active} onChange={(e) => set('active', e.target.checked)} />
        </div>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Reports ----------

function ReportsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const reports = useQuery({ queryKey: qk.scheduledReports, queryFn: scheduledApi.reports });
  const [editing, setEditing] = useState<ScheduledReport | 'new' | null>(null);
  const [deleting, setDeleting] = useState<ScheduledReport | null>(null);
  const [preview, setPreview] = useState<ReportTable | null>(null);
  const send = useMutation({
    mutationFn: ({ id, test }: { id: string; test: boolean }) => scheduledApi.send(id, test),
    onSuccess: (r, v) => {
      toast.success(v.test ? 'Test sent to your email' : `Sent to ${r.recipients}`);
      qc.invalidateQueries({ queryKey: qk.scheduled });
    },
    onError: (e) => toast.error(e),
  });
  const look = useMutation({ mutationFn: scheduledApi.previewReport, onSuccess: setPreview, onError: (e) => toast.error(e) });
  const when = (r: ScheduledReport) =>
    `${r.frequency === 'Daily' ? 'Every day' : r.frequency === 'Weekly' ? `Every ${WEEKDAYS[r.day]}` : `Monthly on the ${r.day}${['st', 'nd', 'rd'][((((r.day + 90) % 100) - 10) % 10) - 1] ?? 'th'}`} at ${hourLabel(r.sendHour)}`;
  return (
    <Card>
      <CardHeader
        title="Scheduled reports"
        description="Emailed as a table with the full Excel file attached."
        actions={
          <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            New report
          </Button>
        }
      />
      {reports.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : reports.isError ? (
        <ErrorState error={reports.error} onRetry={() => reports.refetch()} />
      ) : !reports.data?.length ? (
        <EmptyState
          icon={FileBarChart}
          title="No scheduled reports"
          description="For example: daily attendance to department heads, a monthly headcount to the CEO."
        />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Report</Th>
              <Th>When</Th>
              <Th>To</Th>
              <Th>Next</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {reports.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  <p className="flex items-center gap-2 font-medium text-zinc-900 dark:text-white">
                    {r.name} {!r.isActive && <Badge>Off</Badge>}
                  </p>
                  <p className="text-xs text-zinc-500">{REPORT_KINDS[r.kind].label}</p>
                </Td>
                <Td className="text-sm">{when(r)}</Td>
                <Td className="max-w-56 text-sm">
                  <span className="line-clamp-2">{[...r.recipients, ...(r.extraEmails ? r.extraEmails.split(', ') : [])].join(', ')}</span>
                </Td>
                <Td className="text-sm">
                  {r.isActive && r.nextRunAtUtc ? formatDateTimeUtc(r.nextRunAtUtc) : '—'}
                  {r.lastSentAtUtc && <p className="text-xs text-zinc-500">Last {formatDateTimeUtc(r.lastSentAtUtc)}</p>}
                </Td>
                <Td>
                  <div className="flex justify-end gap-1">
                    <IconButton label="Preview" onClick={() => look.mutate(r.id)}>
                      <Eye className="size-4" />
                    </IconButton>
                    <IconButton
                      label="Download Excel"
                      onClick={() => downloadFile(scheduledApi.excelPath(r.id), `${r.name}.xlsx`).catch((e) => toast.error(e))}
                    >
                      <Download className="size-4" />
                    </IconButton>
                    <IconButton label="Send a test to me" onClick={() => send.mutate({ id: r.id, test: true })}>
                      <Send className="size-4" />
                    </IconButton>
                    <IconButton label="Send to everyone now" onClick={() => send.mutate({ id: r.id, test: false })}>
                      <Play className="size-4" />
                    </IconButton>
                    <RowActions label={r.name} onEdit={() => setEditing(r)} onDelete={() => setDeleting(r)} />
                  </div>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <ReportModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name={deleting?.name ?? ''}
        remove={scheduledApi.deleteReport}
        invalidate={[qk.scheduledReports]}
        onClose={() => setDeleting(null)}
      />
      {preview && <PreviewModal table={preview} onClose={() => setPreview(null)} />}
    </Card>
  );
}

function PreviewModal({ table, onClose }: { table: ReportTable; onClose: () => void }) {
  return (
    <Modal open onClose={onClose} size="xl" title={table.title} description={table.subtitle ?? undefined}>
      <ReportPreview table={table} />
    </Modal>
  );
}

function ReportPreview({ table }: { table: ReportTable }) {
  if (!table.rows.length) return <p className="text-sm text-zinc-500">Nothing to report right now.</p>;
  return (
    <div className="max-h-[60vh] overflow-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
      <Table>
        <THead>
          <Tr>
            {table.headers.map((h, i) => (
              <Th key={i}>{h}</Th>
            ))}
          </Tr>
        </THead>
        <TBody>
          {table.rows.slice(0, 100).map((r, i) => (
            <Tr key={i}>
              {r.map((c, j) => (
                <Td key={j} className="text-sm">
                  {c}
                </Td>
              ))}
            </Tr>
          ))}
        </TBody>
      </Table>
      {table.rows.length > 100 && <p className="px-3 py-2 text-xs text-zinc-500">Showing 100 of {table.rows.length} rows.</p>}
    </div>
  );
}

function ReportModal({ item, onClose }: { item: ScheduledReport | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [f, setF] = useState({
    name: item?.name ?? REPORT_KINDS.Headcount.label,
    kind: (item?.kind ?? 'Headcount') as ReportKind,
    frequency: (item?.frequency ?? 'Weekly') as ReportFrequency,
    day: String(item?.day ?? 1),
    sendHour: String(item?.sendHour ?? 9),
    recipients: item?.recipientIds ?? [],
    extra: item?.extraEmails ?? '',
    departments: item?.departmentIds ?? [],
    active: item?.isActive ?? true,
  });
  const [preview, setPreview] = useState<ReportTable | null>(null);
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const body = (): SaveReport => ({
    name: f.name.trim() || REPORT_KINDS[f.kind].label,
    kind: f.kind,
    frequency: f.frequency,
    day: Number(f.day) || (f.frequency === 'Monthly' ? 1 : 0),
    sendHour: Number(f.sendHour),
    recipientIds: f.recipients,
    extraEmails: f.extra.trim() || null,
    departmentIds: f.departments,
    isActive: f.active,
  });
  const save = useMutation({
    mutationFn: () => (item ? scheduledApi.updateReport(item.id, body()) : scheduledApi.addReport(body())),
    onSuccess: () => {
      toast.success('Report scheduled');
      qc.invalidateQueries({ queryKey: qk.scheduled });
      onClose();
    },
  });
  const look = useMutation({
    mutationFn: () => scheduledApi.previewDraft(body()),
    onSuccess: setPreview,
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={item ? 'Edit report' : 'Schedule a report'}
      footer={
        <>
          <Button variant="ghost" icon={<Eye className="size-4" />} loading={look.isPending} onClick={() => look.mutate()}>
            Preview
          </Button>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.name.trim() || (!f.recipients.length && !f.extra.trim())} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Report" hint={REPORT_KINDS[f.kind].description}>
          <Select
            value={f.kind}
            onChange={(e) => {
              const k = e.target.value as ReportKind;
              setF((x) => ({ ...x, kind: k, name: item ? x.name : REPORT_KINDS[k].label }));
              setPreview(null);
            }}
          >
            {(Object.keys(REPORT_KINDS) as ReportKind[]).map((k) => (
              <option key={k} value={k}>
                {REPORT_KINDS[k].label}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Name" required>
          <Input value={f.name} maxLength={150} onChange={(e) => set('name', e.target.value)} />
        </Field>
        <Field label="How often">
          <Select
            value={f.frequency}
            onChange={(e) => {
              const fr = e.target.value as ReportFrequency;
              setF((x) => ({ ...x, frequency: fr, day: fr === 'Weekly' ? '1' : fr === 'Monthly' ? '1' : '0' }));
            }}
          >
            <option value="Daily">Every day</option>
            <option value="Weekly">Every week</option>
            <option value="Monthly">Every month</option>
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-2">
          {f.frequency === 'Weekly' && (
            <Field label="Day">
              <Select value={f.day} onChange={(e) => set('day', e.target.value)}>
                {WEEKDAYS.map((d, i) => (
                  <option key={d} value={i}>
                    {d}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {f.frequency === 'Monthly' && (
            <Field label="Day of month">
              <Input type="number" min={1} max={28} value={f.day} onChange={(e) => set('day', e.target.value)} />
            </Field>
          )}
          <Field label="At">
            <Select value={f.sendHour} onChange={(e) => set('sendHour', e.target.value)}>
              {Array.from({ length: 24 }, (_, h) => (
                <option key={h} value={h}>
                  {hourLabel(h)}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="sm:col-span-2">
          <PeoplePicker label="Send to" value={f.recipients} onChange={(v) => set('recipients', v)} />
        </div>
        <Field label="Other email addresses" hint="Separated by commas, up to 20" className="sm:col-span-2">
          <Input value={f.extra} maxLength={2000} onChange={(e) => set('extra', e.target.value)} placeholder="ceo@company.com" />
        </Field>
        <div className="sm:col-span-2">
          <DepartmentPicker value={f.departments} onChange={(v) => set('departments', v)} />
        </div>
        <Checkbox label="On" checked={f.active} onChange={(e) => set('active', e.target.checked)} />
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : look.isError ? errorMessage(look.error) : null} />
      {preview && (
        <div className="mt-4">
          <p className="mb-2 text-sm font-medium">
            {preview.title} <span className="font-normal text-zinc-500">· {preview.subtitle}</span>
          </p>
          <ReportPreview table={preview} />
        </div>
      )}
    </Modal>
  );
}

// ---------- History ----------

function HistoryTab() {
  const runs = useQuery({ queryKey: qk.scheduledRuns, queryFn: scheduledApi.runs });
  return (
    <Card>
      {runs.isLoading ? (
        <SkeletonRows rows={5} cols={4} />
      ) : !runs.data?.length ? (
        <EmptyState icon={History} title="Nothing sent yet" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>When</Th>
              <Th>What</Th>
              <Th>About</Th>
              <Th className="text-right">Recipients</Th>
            </Tr>
          </THead>
          <TBody>
            {runs.data.map((r) => (
              <Tr key={r.id}>
                <Td className="text-sm whitespace-nowrap">{formatDateTimeUtc(r.createdAtUtc)}</Td>
                <Td>
                  <p className="font-medium">{r.name}</p>
                  <p className="text-xs text-zinc-500">
                    {r.alertId ? 'Alert' : 'Report'}
                    {r.manual ? ' · sent by hand' : ''}
                  </p>
                </Td>
                <Td className="text-sm">
                  {r.reportId ? `${r.items} rows` : `${r.items} people`}
                  {r.note && <p className="text-xs text-zinc-500">{r.note}</p>}
                </Td>
                <Td className="text-right tabular-nums">{r.recipients}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}
