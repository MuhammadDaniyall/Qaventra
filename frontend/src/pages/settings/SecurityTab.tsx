import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Copy, KeyRound, Plus, ShieldCheck, TriangleAlert } from 'lucide-react';
import { http } from '../../api/client';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  IconButton,
  Input,
  Modal,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDateTimeUtc, roleLabel } from '../../lib/format';
import { tx } from '../../lib/i18n';

interface LastLogin {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  lastLoginUtc: string | null;
  failedLast30Days: number;
}
interface ErrorRow {
  id: number;
  timestampUtc: string;
  userEmail: string | null;
  method: string | null;
  path: string | null;
  statusCode: number | null;
  correlationId: string | null;
}

const api = {
  get: () => http.get<{ allowedEmailDomains: string[] }>('/setup/security'),
  save: (allowedEmailDomains: string[]) => http.put<{ allowedEmailDomains: string[] }>('/setup/security', { allowedEmailDomains }),
  lastLogins: () => http.get<LastLogin[]>('/setup/security/last-logins'),
  errors: () => http.get<ErrorRow[]>('/setup/security/errors'),
};

/** Company admin: who may sign in, when each login was last used, and server errors to report. */
export function SecurityTab() {
  return (
    <div className="space-y-6">
      <DomainsCard />
      <ApiKeysCard />
      <LastLoginsCard />
      <ErrorsCard />
    </div>
  );
}

function DomainsCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ['security'], queryFn: api.get });
  const [text, setText] = useState('');
  useEffect(() => {
    if (q.data) setText(q.data.allowedEmailDomains.join(', '));
  }, [q.data]);
  const save = useMutation({
    mutationFn: () => api.save(text.split(/[\s,;]+/).filter(Boolean)),
    onSuccess: (d) => {
      qc.setQueryData(['security'], d);
      toast.success('Sign-in domains saved');
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader
        title="Sign-in domains"
        description="Only logins with these email domains can sign in. Company admins always can, so nobody is locked out. Leave empty to allow any email."
      />
      <CardBody className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <Field label="Allowed domains" hint="Separate with commas" className="flex-1">
          <Input value={text} onChange={(e) => setText(e.target.value)} placeholder="yourcompany.com.pk, yourcompany.com" dir="ltr" disabled={q.isLoading} />
        </Field>
        <Button loading={save.isPending} onClick={() => save.mutate()}>
          Save
        </Button>
      </CardBody>
    </Card>
  );
}

function LastLoginsCard() {
  const q = useQuery({ queryKey: ['security', 'last-logins'], queryFn: api.lastLogins });
  return (
    <Card>
      <CardHeader title="Last sign-in" description="Longest unused first. Turn off logins that are no longer needed." />
      {q.isLoading ? (
        <SkeletonRows rows={5} cols={4} />
      ) : q.isError ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Person</Th>
              <Th>Role</Th>
              <Th>Last sign-in</Th>
              <Th className="text-end">Failed (30 days)</Th>
            </tr>
          </THead>
          <TBody>
            {q.data!.map((u) => (
              <Tr key={u.userId}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{u.fullName}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">{u.email}</p>
                </Td>
                <Td>
                  {tx(roleLabel(u.role))} {!u.isActive && <Badge tone="gray">{tx('Inactive')}</Badge>}
                </Td>
                <Td>{u.lastLoginUtc ? formatDateTimeUtc(u.lastLoginUtc) : <span className="text-zinc-500 dark:text-zinc-400">{tx('Never')}</span>}</Td>
                <Td className="text-end tabular-nums">
                  {u.failedLast30Days > 0 ? <Badge tone={u.failedLast30Days >= 5 ? 'red' : 'amber'}>{u.failedLast30Days}</Badge> : '0'}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}

function ErrorsCard() {
  const q = useQuery({ queryKey: ['security', 'errors'], queryFn: api.errors });
  return (
    <Card>
      <CardHeader title="Errors" description="Requests that failed on the server in the last 30 days. Send the reference to support." />
      {q.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : q.isError ? (
        <ErrorState error={q.error} onRetry={() => q.refetch()} />
      ) : !q.data!.length ? (
        <EmptyState icon={ShieldCheck} title="No errors" description="Nothing failed on the server in the last 30 days." className="py-8" />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>When</Th>
              <Th>Who</Th>
              <Th>Request</Th>
              <Th>Reference</Th>
            </tr>
          </THead>
          <TBody>
            {q.data!.map((e) => (
              <Tr key={e.id}>
                <Td>{formatDateTimeUtc(e.timestampUtc)}</Td>
                <Td>{e.userEmail ?? '—'}</Td>
                <Td>
                  <span className="inline-flex items-center gap-1.5 font-mono text-xs" dir="ltr">
                    <TriangleAlert className="size-3.5 text-red-500" aria-hidden="true" />
                    {e.statusCode} {e.method} {e.path}
                  </span>
                </Td>
                <Td className="font-mono text-xs">{e.correlationId ?? '—'}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}

interface ApiToken {
  id: string;
  name: string;
  tokenPrefix: string;
  createdByName: string | null;
  createdAtUtc: string;
  expiresOn: string | null;
  revokedAtUtc: string | null;
  lastUsedAtUtc: string | null;
}

const keysApi = {
  list: () => http.get<ApiToken[]>('/setup/api-tokens'),
  create: (b: { name: string; expiresOn: string | null }) => http.post<{ token: ApiToken; key: string }>('/setup/api-tokens', b),
  revoke: (id: string) => http.post(`/setup/api-tokens/${id}/revoke`),
};

/** Read-only keys for other systems (accounting, BI). Each key is shown once. */
function ApiKeysCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['api-tokens'], queryFn: keysApi.list });
  const [adding, setAdding] = useState(false);
  const [name, setName] = useState('');
  const [expires, setExpires] = useState('');
  const [shown, setShown] = useState<string | null>(null);
  const [revoking, setRevoking] = useState<ApiToken | null>(null);
  const create = useMutation({
    mutationFn: () => keysApi.create({ name: name.trim(), expiresOn: expires || null }),
    onSuccess: (r) => {
      setAdding(false);
      setName('');
      setExpires('');
      setShown(r.key);
      qc.invalidateQueries({ queryKey: ['api-tokens'] });
    },
    onError: (e) => toast.error(e),
  });
  const revoke = useMutation({
    mutationFn: (t: ApiToken) => keysApi.revoke(t.id),
    onSuccess: () => {
      setRevoking(null);
      qc.invalidateQueries({ queryKey: ['api-tokens'] });
    },
    onError: (e) => toast.error(e),
  });
  const base = `${window.location.origin}/api/integration`;

  return (
    <Card>
      <CardHeader
        title="API keys"
        description="Read-only keys for other software to pull employees, attendance, leave and finalized payroll. Keys can never change data."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setAdding(true)}>
            New key
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={2} cols={3} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data!.length ? (
        <EmptyState icon={KeyRound} title="No API keys" description="Create one when another system needs Qaventra data." className="py-8" />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data!.map((t) => (
            <li key={t.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">
                  {t.name} <span className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{t.tokenPrefix}…</span>
                </span>
                <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                  {t.lastUsedAtUtc ? `${tx('Last used')} ${formatDateTimeUtc(t.lastUsedAtUtc)}` : tx('Never used')}
                  {t.expiresOn ? ` · ${tx('expires')} ${t.expiresOn}` : ''}
                </span>
              </span>
              {t.revokedAtUtc ? (
                <Badge tone="gray">{tx('Revoked')}</Badge>
              ) : (
                <Button size="sm" variant="ghost" onClick={() => setRevoking(t)}>
                  Revoke
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      <Modal
        open={adding}
        onClose={() => setAdding(false)}
        size="sm"
        title="New API key"
        footer={
          <>
            <Button variant="secondary" onClick={() => setAdding(false)}>
              Cancel
            </Button>
            <Button loading={create.isPending} disabled={!name.trim()} onClick={() => create.mutate()}>
              Create key
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <Field label="What it is for" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Accounting software" />
          </Field>
          <Field label="Expires on" hint="Optional">
            <Input type="date" value={expires} onChange={(e) => setExpires(e.target.value)} />
          </Field>
        </div>
      </Modal>
      <Modal
        open={!!shown}
        onClose={() => setShown(null)}
        size="lg"
        title="Copy the key now"
        description="It is shown only once. Send it in the X-Api-Key header."
        footer={<Button onClick={() => setShown(null)}>{tx('Done')}</Button>}
      >
        <div className="space-y-4">
          <div className="flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-700 dark:bg-zinc-800/50">
            <code className="min-w-0 flex-1 font-mono text-sm break-all" dir="ltr">
              {shown}
            </code>
            <IconButton label="Copy key" onClick={() => shown && navigator.clipboard.writeText(shown).then(() => toast.success('Copied'))}>
              <Copy className="size-4" />
            </IconButton>
          </div>
          <pre className="overflow-x-auto rounded-lg bg-zinc-950 p-3 text-xs leading-relaxed text-zinc-100" dir="ltr">
            {`GET ${base}/employees
GET ${base}/attendance?from=2026-10-01&to=2026-10-31
GET ${base}/leave?from=2026-10-01&to=2026-10-31
GET ${base}/payroll/2026/9

X-Api-Key: ${shown ?? ''}`}
          </pre>
        </div>
      </Modal>
      <ConfirmDialog
        open={!!revoking}
        title="Revoke this key?"
        message="Anything using it stops at once. This can't be undone; create a new key if needed."
        confirmLabel="Revoke"
        loading={revoke.isPending}
        onConfirm={() => revoking && revoke.mutate(revoking)}
        onClose={() => setRevoking(null)}
      />
    </Card>
  );
}
