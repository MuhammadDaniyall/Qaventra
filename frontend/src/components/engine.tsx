import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { History } from 'lucide-react';
import { historyApi } from '../api/engine';
import { useEmployeeLookup } from '../api/hooks';
import { useUser } from '../auth/AuthContext';
import { isApprover } from '../auth/roles';
import { formatDateTimeUtc, humanize } from '../lib/format';
import { EmptyState, Field, IconButton, Modal, Select, Skeleton } from './ui';

/** "For whom" picker on request forms: approvers may file for someone else (HR anyone, managers their team). */
export function OnBehalfField({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const user = useUser();
  const approver = isApprover(user);
  const people = useEmployeeLookup(approver);
  if (!approver) return null;
  return (
    <Field label="For" hint="File it for someone in your team; it goes through their approval chain.">
      <Select value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">Myself</option>
        {(people.data ?? [])
          .filter((p) => p.id !== user.employeeId)
          .map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
      </Select>
    </Field>
  );
}

/** Button that shows who changed a record and how, from the audit log. */
export function HistoryButton({ entityType, entityId, label }: { entityType: string; entityId: string; label?: string }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <IconButton label={label ?? 'Change history'} onClick={() => setOpen(true)}>
        <History className="size-4" />
      </IconButton>
      {open && <HistoryModal entityType={entityType} entityId={entityId} onClose={() => setOpen(false)} />}
    </>
  );
}

function HistoryModal({ entityType, entityId, onClose }: { entityType: string; entityId: string; onClose: () => void }) {
  const history = useQuery({ queryKey: ['history', entityType, entityId], queryFn: () => historyApi.get(entityType, entityId) });
  const parse = (json: string | null): [string, { old?: unknown; new?: unknown }][] => {
    if (!json) return [];
    try {
      return Object.entries(JSON.parse(json) as Record<string, { old?: unknown; new?: unknown }>);
    } catch {
      return [];
    }
  };
  const show = (v: unknown) => (v === null || v === undefined || v === '' ? '—' : typeof v === 'object' ? JSON.stringify(v) : String(v));
  return (
    <Modal open onClose={onClose} size="lg" title="Change history" description="Who changed what, newest first.">
      {history.isLoading ? (
        <Skeleton className="h-40" />
      ) : !history.data?.length ? (
        <EmptyState icon={History} title="No changes recorded" />
      ) : (
        <ol className="space-y-4">
          {history.data.map((h, i) => (
            <li key={i} className="border-l-2 border-zinc-200 pl-3 dark:border-zinc-700">
              <p className="text-xs text-zinc-500">
                {formatDateTimeUtc(h.timestampUtc)} · {h.userEmail ?? 'System'} · {humanize(h.action)}
              </p>
              <ul className="mt-1 space-y-0.5 text-sm">
                {parse(h.changes)
                  .filter(([k]) => !['UpdatedAtUtc', 'CreatedAtUtc', 'TenantId', 'Id'].includes(k))
                  .map(([k, v]) => (
                    <li key={k}>
                      <span className="font-medium">{humanize(k)}</span>: <span className="text-zinc-500 line-through">{show(v.old)}</span> → {show(v.new)}
                    </li>
                  ))}
              </ul>
            </li>
          ))}
        </ol>
      )}
    </Modal>
  );
}
