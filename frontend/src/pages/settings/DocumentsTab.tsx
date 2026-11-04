import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HardDrive } from 'lucide-react';
import { http } from '../../api/client';
import { Card, CardHeader, ErrorState, Select, SkeletonRows, StatCard, Switch, Table, TBody, Td, Th, THead, Tr, useToast } from '../../components/ui';

interface ModuleSetting {
  ownerType: string;
  label: string;
  enabled: boolean;
  maxSizeMb: number;
  files: number;
  bytes: number;
}
interface Storage {
  modules: ModuleSetting[];
  receiptFiles: number;
  receiptBytes: number;
  totalBytes: number;
}

const api = {
  get: () => http.get<Storage>('/attachments/settings'),
  save: (ownerType: string, b: { enabled: boolean; maxSizeMb: number }) => http.put(`/attachments/settings/${ownerType}`, b),
};

const formatBytes = (n: number) =>
  n >= 1024 * 1024 * 1024 ? `${(n / 1024 / 1024 / 1024).toFixed(1)} GB` : n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} MB` : `${Math.ceil(n / 1024)} KB`;

/** Where files may be attached, how large each may be, and the space files take per module. */
export function DocumentsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const data = useQuery({ queryKey: ['documents', 'settings'], queryFn: api.get });
  const save = useMutation({
    mutationFn: (m: ModuleSetting) => api.save(m.ownerType, { enabled: m.enabled, maxSizeMb: m.maxSizeMb }),
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: ['documents', 'settings'] });
    },
    onError: (err) => toast.error(err),
  });
  const d = data.data;
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <StatCard label="Storage used" value={d ? formatBytes(d.totalBytes) : undefined} icon={HardDrive} loading={data.isLoading} />
        <StatCard
          label="Files"
          value={d ? d.modules.reduce((t, m) => t + m.files, 0) + d.receiptFiles : undefined}
          icon={HardDrive}
          loading={data.isLoading}
          hint={d ? `${d.receiptFiles} expense receipts (${formatBytes(d.receiptBytes)})` : undefined}
        />
      </div>
      <Card>
        <CardHeader title="Attachments by module" description="Switch attachments off where you do not want files, or lower the size limit." />
        {data.isLoading ? (
          <SkeletonRows rows={6} cols={4} />
        ) : data.isError || !d ? (
          <ErrorState error={data.error} onRetry={() => data.refetch()} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Module</Th>
                <Th>Attachments</Th>
                <Th>Largest file</Th>
                <Th className="text-end">Files</Th>
                <Th className="text-end">Space</Th>
              </tr>
            </THead>
            <TBody>
              {d.modules.map((m) => (
                <Tr key={m.ownerType}>
                  <Td className="font-medium">{m.label}</Td>
                  <Td>
                    <Switch checked={m.enabled} onChange={(v) => save.mutate({ ...m, enabled: v })} label={m.enabled ? 'On' : 'Off'} />
                  </Td>
                  <Td>
                    <Select
                      className="w-28"
                      aria-label={`Largest file for ${m.label}`}
                      value={m.maxSizeMb}
                      disabled={!m.enabled}
                      onChange={(e) => save.mutate({ ...m, maxSizeMb: Number(e.target.value) })}
                    >
                      {[1, 2, 3, 4, 5].map((n) => (
                        <option key={n} value={n}>
                          {n} MB
                        </option>
                      ))}
                    </Select>
                  </Td>
                  <Td className="text-end tabular-nums">{m.files}</Td>
                  <Td className="text-end tabular-nums">{formatBytes(m.bytes)}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </div>
  );
}
