import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Mail, Pencil } from 'lucide-react';
import { http } from '../../api/client';
import type { PagedResult, RecordStamps } from '../../types';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  FormError,
  ErrorState,
  Field,
  Input,
  Modal,
  Pagination,
  SkeletonRows,
  StatCard,
  Switch,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { RecordStamp } from '../../components/RecordStamp';
import { formatDateTimeUtc, humanize } from '../../lib/format';
import { tx } from '../../lib/i18n';
import { useUser } from '../../auth/AuthContext';
import { isCompanyAdmin } from '../../auth/roles';
import { errorMessage } from '../../api/client';

interface EmailTemplate {
  kind: string;
  label: string;
  subject: string | null;
  intro: string | null;
  enabled: boolean;
  isCustom: boolean;
  /** Only for wording the company saved; standard wording has none. */
  stamps: RecordStamps | null;
}
type LogStatus = 'Sent' | 'Failed' | 'OverQuota' | 'NotConfigured';
interface EmailLog {
  id: string;
  createdAtUtc: string;
  to: string;
  subject: string;
  kind: string | null;
  status: LogStatus;
  error: string | null;
}

const api = {
  templates: () => http.get<EmailTemplate[]>('/setup/email/templates'),
  save: (kind: string, b: { subject: string; intro: string | null; enabled: boolean }) => http.put(`/setup/email/templates/${kind}`, b),
  reset: (kind: string) => http.del(`/setup/email/templates/${kind}`),
  log: (page: number) => http.get<PagedResult<EmailLog>>('/setup/email/log', { page, pageSize: 25 }),
  usage: () => http.get<{ sentThisMonth: number; failedThisMonth: number; quotaPerMonth: number | null }>('/setup/email/usage'),
  smtp: () => http.get<CompanySmtp | null>('/setup/email/smtp'),
  saveSmtp: (b: Omit<CompanySmtp, 'hasPassword'> & { password: string | null }) => http.put('/setup/email/smtp', b),
  testSmtp: (to: string) => http.post<{ ok: boolean; error: string | null }>('/setup/email/smtp/test', { to }),
};

interface CompanySmtp {
  host: string;
  port: number;
  user: string | null;
  hasPassword: boolean;
  from: string;
  fromName: string | null;
  enableSsl: boolean;
  isEnabled: boolean;
}

const TONES: Record<LogStatus, 'green' | 'red' | 'amber' | 'gray'> = { Sent: 'green', Failed: 'red', OverQuota: 'amber', NotConfigured: 'gray' };

/** Email wording per notification, this month's usage against the plan, and the log of what went out. */
export function EmailTab() {
  const admin = isCompanyAdmin(useUser());
  const usage = useQuery({ queryKey: ['email', 'usage'], queryFn: api.usage });
  const u = usage.data;
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <StatCard label="Sent this month" value={u?.sentThisMonth} icon={Mail} loading={usage.isLoading} hint={u?.quotaPerMonth ? `${tx('of')} ${u.quotaPerMonth}` : tx('No monthly limit')} />
        <StatCard label="Failed this month" value={u?.failedThisMonth} icon={Mail} tone="red" loading={usage.isLoading} />
      </div>
      {admin && <SmtpCard />}
      <TemplatesCard />
      <LogCard />
    </div>
  );
}

function TemplatesCard() {
  const list = useQuery({ queryKey: ['email', 'templates'], queryFn: api.templates });
  const [editing, setEditing] = useState<EmailTemplate | null>(null);
  return (
    <Card>
      <CardHeader title="Email wording" description="Change the subject and add your own opening line for each kind of email. Use {name}, {company}, {title} and {body}." />
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={2} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data!.map((t) => (
            <li key={t.kind} className="flex items-center gap-3 px-5 py-3">
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{tx(t.label)}</span>
                <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">
                  {!t.enabled ? tx('Email off, in-app only') : t.isCustom ? t.subject : tx('Standard wording')}
                </span>
              </span>
              {t.isCustom && <Badge tone={t.enabled ? 'accent' : 'gray'}>{t.enabled ? tx('Custom') : tx('Off')}</Badge>}
              <Button size="sm" variant="ghost" icon={<Pencil className="size-3.5" />} onClick={() => setEditing(t)}>
                Edit
              </Button>
            </li>
          ))}
        </ul>
      )}
      {editing && <TemplateModal template={editing} onClose={() => setEditing(null)} />}
    </Card>
  );
}

function TemplateModal({ template, onClose }: { template: EmailTemplate; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [subject, setSubject] = useState(template.subject ?? '{title}');
  const [intro, setIntro] = useState(template.intro ?? '');
  const [enabled, setEnabled] = useState(template.enabled);
  const done = () => {
    qc.invalidateQueries({ queryKey: ['email', 'templates'] });
    onClose();
  };
  const save = useMutation({
    mutationFn: () => api.save(template.kind, { subject: subject.trim(), intro: intro.trim() || null, enabled }),
    onSuccess: () => (toast.success('Email wording saved'), done()),
    onError: (e) => toast.error(e),
  });
  const reset = useMutation({ mutationFn: () => api.reset(template.kind), onSuccess: done, onError: (e) => toast.error(e) });

  return (
    <Modal
      open
      onClose={onClose}
      title={tx(template.label)}
      footer={
        <>
          {template.isCustom && (
            <Button variant="ghost" className="me-auto" loading={reset.isPending} onClick={() => reset.mutate()}>
              Use standard wording
            </Button>
          )}
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!subject.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Switch checked={enabled} onChange={setEnabled} label="Send this email" description="Off: people still see the notification in the app." />
        <Field label="Subject" required hint="{title} is the notification's own subject">
          <Input value={subject} maxLength={200} onChange={(e) => setSubject(e.target.value)} dir="auto" />
        </Field>
        <Field label="Opening line" hint="Shown above the details, e.g. Dear {name},">
          <Textarea rows={3} value={intro} maxLength={2000} onChange={(e) => setIntro(e.target.value)} dir="auto" />
        </Field>
        {template.stamps && <RecordStamp stamps={template.stamps} />}
      </div>
    </Modal>
  );
}

function LogCard() {
  const [page, setPage] = useState(1);
  const log = useQuery({ queryKey: ['email', 'log', page], queryFn: () => api.log(page) });
  return (
    <Card>
      <CardHeader title="Email log" description="Every email sent for your company, newest first" />
      {log.isLoading ? (
        <SkeletonRows rows={5} cols={4} />
      ) : log.isError ? (
        <ErrorState error={log.error} onRetry={() => log.refetch()} />
      ) : !log.data?.items.length ? (
        <EmptyState icon={Mail} title="No emails yet" className="py-8" />
      ) : (
        <>
          <Table>
            <THead>
              <tr>
                <Th>When</Th>
                <Th>To</Th>
                <Th>Subject</Th>
                <Th>Status</Th>
              </tr>
            </THead>
            <TBody>
              {log.data.items.map((l) => (
                <Tr key={l.id}>
                  <Td>{formatDateTimeUtc(l.createdAtUtc)}</Td>
                  <Td>{l.to}</Td>
                  <Td className="max-w-xs truncate" title={l.error ?? l.subject}>
                    {l.subject}
                  </Td>
                  <Td>
                    <Badge tone={TONES[l.status]} dot>
                      {humanize(l.status)}
                    </Badge>
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
          <Pagination page={page} pageSize={25} total={log.data.total} onPage={setPage} />
        </>
      )}
    </Card>
  );
}

/** The company's own mail server (company admin only). The password is stored encrypted and never shown again. */
function SmtpCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const current = useQuery({ queryKey: ['email', 'smtp'], queryFn: api.smtp });
  if (current.isLoading) return null;
  return <SmtpForm key={current.dataUpdatedAt} current={current.data ?? null} onSaved={() => qc.invalidateQueries({ queryKey: ['email', 'smtp'] })} toast={toast} />;
}

function SmtpForm({ current, onSaved, toast }: { current: CompanySmtp | null; onSaved: () => void; toast: ReturnType<typeof useToast> }) {
  const [f, setF] = useState({
    host: current?.host ?? '',
    port: String(current?.port ?? 587),
    user: current?.user ?? '',
    password: '',
    from: current?.from ?? '',
    fromName: current?.fromName ?? '',
    enableSsl: current?.enableSsl ?? true,
    isEnabled: current?.isEnabled ?? false,
  });
  const [testTo, setTestTo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const set = (k: keyof typeof f, v: string | boolean) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () =>
      api.saveSmtp({
        host: f.host.trim(),
        port: Number(f.port),
        user: f.user.trim() || null,
        password: f.password ? f.password : null,
        from: f.from.trim(),
        fromName: f.fromName.trim() || null,
        enableSsl: f.enableSsl,
        isEnabled: f.isEnabled,
      }),
    onSuccess: () => {
      setError(null);
      toast.success('Mail server saved');
      onSaved();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const test = useMutation({
    mutationFn: () => api.testSmtp(testTo.trim()),
    onSuccess: (r) => (r.ok ? toast.success('Test email sent') : setError(`The test failed: ${r.error ?? 'unknown error'}`)),
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Card>
      <CardHeader title="Your own mail server" description="Send the company's emails from your own address. Leave it off to use Qaventra's." />
      <CardBody className="space-y-4">
        <FormError message={error} />
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Server (SMTP host)" required>
            <Input value={f.host} maxLength={200} placeholder="smtp.office365.com" onChange={(e) => set('host', e.target.value)} />
          </Field>
          <Field label="Port" hint="587 or 465 usually">
            <Input type="number" value={f.port} onChange={(e) => set('port', e.target.value)} />
          </Field>
          <Field label="User name">
            <Input value={f.user} maxLength={200} autoComplete="off" onChange={(e) => set('user', e.target.value)} />
          </Field>
          <Field label="Password" hint={current?.hasPassword ? 'Saved. Type a new one only to change it.' : undefined}>
            <Input type="password" value={f.password} maxLength={300} autoComplete="new-password" onChange={(e) => set('password', e.target.value)} />
          </Field>
          <Field label="From address" required>
            <Input type="email" value={f.from} maxLength={200} placeholder="hr@yourcompany.com" onChange={(e) => set('from', e.target.value)} />
          </Field>
          <Field label="From name">
            <Input value={f.fromName} maxLength={100} placeholder="Acme HR" onChange={(e) => set('fromName', e.target.value)} />
          </Field>
        </div>
        <Switch checked={f.enableSsl} onChange={(v) => set('enableSsl', v)} label="Encrypted connection (SSL/TLS)" />
        <Switch checked={f.isEnabled} onChange={(v) => set('isEnabled', v)} label="Send the company's emails through this server" />
        <div className="flex flex-wrap items-end justify-between gap-3">
          {current && (
            <div className="flex items-end gap-2">
              <Field label="Send a test to">
                <Input type="email" value={testTo} placeholder="you@yourcompany.com" onChange={(e) => setTestTo(e.target.value)} />
              </Field>
              <Button variant="secondary" disabled={!testTo.trim()} loading={test.isPending} onClick={() => test.mutate()}>
                Send test
              </Button>
            </div>
          )}
          <Button disabled={!f.host.trim() || !f.from.trim()} loading={save.isPending} onClick={() => save.mutate()}>
            Save
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}
