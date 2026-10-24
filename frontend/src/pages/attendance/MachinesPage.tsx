import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Copy, Fingerprint, KeyRound, Pencil, Plus, Trash2 } from 'lucide-react';
import { biometricApi, type BiometricDevice, type SaveBiometricDevice } from '../../api/biometric';
import { qk } from '../../api/keys';
import { setupApi } from '../../api/organization';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Switch,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDateTimeUtc } from '../../lib/format';
import { tx } from '../../lib/i18n';

const KEY = ['biometric', 'devices'] as const;

/** HR's list of attendance machines, with the one-time token each needs to send punches. */
export function MachinesPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const devices = useQuery({ queryKey: KEY, queryFn: biometricApi.list });
  const [editing, setEditing] = useState<BiometricDevice | 'new' | null>(null);
  const [token, setToken] = useState<{ name: string; token: string } | null>(null);
  const [removing, setRemoving] = useState<BiometricDevice | null>(null);
  const [renewing, setRenewing] = useState<BiometricDevice | null>(null);

  const renew = useMutation({
    mutationFn: (d: BiometricDevice) => biometricApi.newToken(d.id),
    onSuccess: (r) => {
      setRenewing(null);
      setToken({ name: r.device.name, token: r.token });
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({
    mutationFn: (d: BiometricDevice) => biometricApi.remove(d.id),
    onSuccess: () => {
      setRemoving(null);
      toast.success('Machine removed');
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });

  return (
    <>
      <PageHeader
        title="Attendance machines"
        description="Fingerprint or face machines that send punches straight into attendance. Match people by their punch code."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            Add machine
          </Button>
        }
      />
      <Card>
        {devices.isLoading ? (
          <SkeletonRows rows={3} cols={4} />
        ) : devices.isError ? (
          <ErrorState error={devices.error} onRetry={() => devices.refetch()} />
        ) : !devices.data?.length ? (
          <EmptyState
            icon={Fingerprint}
            title="No machines yet"
            description="Add a machine to get its token, then set the punch code on each employee's file."
            action={
              <Button icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
                Add machine
              </Button>
            }
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Machine</Th>
                <Th>Office</Th>
                <Th>Token</Th>
                <Th>Last punch received</Th>
                <Th className="text-end">Punches</Th>
                <Th className="text-end">Actions</Th>
              </tr>
            </THead>
            <TBody>
              {devices.data.map((d) => (
                <Tr key={d.id}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{d.name}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">{d.serialNumber ?? '—'}</p>
                  </Td>
                  <Td>{d.office ?? '—'}</Td>
                  <Td>
                    <span className="font-mono text-xs">{d.tokenPrefix}…</span> {!d.isActive && <Badge tone="gray">{tx('Off')}</Badge>}
                  </Td>
                  <Td>{d.lastSeenAtUtc ? formatDateTimeUtc(d.lastSeenAtUtc) : tx('Never')}</Td>
                  <Td className="text-end tabular-nums">{d.punchesReceived}</Td>
                  <Td className="text-end">
                    <div className="inline-flex gap-1">
                      <IconButton label="Edit" onClick={() => setEditing(d)}>
                        <Pencil className="size-4" />
                      </IconButton>
                      <IconButton label="Replace token" onClick={() => setRenewing(d)}>
                        <KeyRound className="size-4" />
                      </IconButton>
                      <IconButton label="Remove" onClick={() => setRemoving(d)}>
                        <Trash2 className="size-4" />
                      </IconButton>
                    </div>
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {editing && (
        <MachineModal
          device={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onCreated={(name, t) => setToken({ name, token: t })}
        />
      )}
      {token && <TokenModal {...token} onClose={() => setToken(null)} />}
      <ConfirmDialog
        open={!!renewing}
        title="Replace the token?"
        message="The machine stops sending punches until its bridge is set up with the new token."
        confirmLabel="Replace token"
        loading={renew.isPending}
        onConfirm={() => renewing && renew.mutate(renewing)}
        onClose={() => setRenewing(null)}
      />
      <ConfirmDialog
        open={!!removing}
        title="Remove this machine?"
        message="Attendance it already sent stays. New punches from it are refused."
        confirmLabel="Remove"
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing)}
        onClose={() => setRemoving(null)}
      />
    </>
  );
}

function MachineModal({ device, onClose, onCreated }: { device: BiometricDevice | null; onClose: () => void; onCreated: (name: string, token: string) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const offices = useQuery({ queryKey: qk.offices, queryFn: setupApi.offices });
  const [v, setV] = useState<SaveBiometricDevice>({
    name: device?.name ?? '',
    serialNumber: device?.serialNumber ?? '',
    officeLocationId: device?.officeLocationId ?? null,
    isActive: device?.isActive ?? true,
  });
  const save = useMutation({
    mutationFn: () => {
      const body = { ...v, serialNumber: v.serialNumber?.trim() || null };
      return device ? biometricApi.update(device.id, body).then(() => null) : biometricApi.create(body);
    },
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: KEY });
      if (created) onCreated(created.device.name, created.token);
      else toast.success('Machine saved');
      onClose();
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={device ? 'Edit machine' : 'Add machine'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!v.name.trim()} onClick={() => save.mutate()}>
            {device ? 'Save' : 'Add machine'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Name" required>
          <Input value={v.name} maxLength={100} onChange={(e) => setV({ ...v, name: e.target.value })} placeholder="Main gate" />
        </Field>
        <Field label="Serial number" hint="Optional, from the label on the machine">
          <Input value={v.serialNumber ?? ''} maxLength={100} onChange={(e) => setV({ ...v, serialNumber: e.target.value })} />
        </Field>
        <Field label="Office">
          <Select value={v.officeLocationId ?? ''} onChange={(e) => setV({ ...v, officeLocationId: e.target.value || null })}>
            <option value="">{tx('Not specified')}</option>
            {(offices.data ?? []).map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </Select>
        </Field>
        {device && <Switch checked={v.isActive} onChange={(on) => setV({ ...v, isActive: on })} label="Accept punches from this machine" />}
      </div>
    </Modal>
  );
}

/** Shown once: the token and how the machine's bridge sends punches. */
function TokenModal({ name, token, onClose }: { name: string; token: string; onClose: () => void }) {
  const toast = useToast();
  const url = `${window.location.origin}/api/biometric/punches`;
  const example = `POST ${url}
X-Device-Token: ${token}
Content-Type: application/json

{ "punches": [ { "punchCode": "1001", "time": "2026-10-24T09:02:00" } ] }`;
  const copy = (text: string) =>
    navigator.clipboard.writeText(text).then(
      () => toast.success('Copied'),
      () => toast.error('Could not copy. Select the text and copy it.'),
    );

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={`${tx('Token for')} ${name}`}
      description="Copy it now: it is shown only once. Give it to whoever sets up the machine's bridge software."
      footer={<Button onClick={onClose}>{tx('Done')}</Button>}
    >
      <div className="space-y-4">
        <div className="flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-700 dark:bg-zinc-800/50">
          <code className="min-w-0 flex-1 font-mono text-sm break-all text-zinc-900 dark:text-zinc-100" dir="ltr">
            {token}
          </code>
          <IconButton label="Copy token" onClick={() => copy(token)}>
            <Copy className="size-4" />
          </IconButton>
        </div>
        <div>
          <p className="mb-1.5 text-sm font-medium text-zinc-700 dark:text-zinc-300">{tx('How the machine sends punches')}</p>
          <pre className="overflow-x-auto rounded-lg bg-zinc-950 p-3 text-xs leading-relaxed text-zinc-100" dir="ltr">
            {example}
          </pre>
          <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">
            {tx('Times are your company’s local time. The first punch of a day is the check-in and the last is the check-out. Each person needs a punch code on their file that matches the machine.')}
          </p>
        </div>
      </div>
    </Modal>
  );
}
