import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { qk } from '../../api/keys';
import { leaveRulesApi } from '../../api/leaveRules';
import { Badge, Button, Checkbox, IconButton, Input, Modal, useToast } from '../../components/ui';
import type { LeaveType } from '../../types';

/** Reasons people give for a kind of leave, each with an optional day limit and document rule. */
export function LeaveReasonsModal({ type, onClose }: { type: LeaveType; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const all = useQuery({ queryKey: qk.leaveReasons, queryFn: leaveRulesApi.reasons });
  const reasons = (all.data ?? []).filter((r) => r.leaveTypeId === type.id);
  const [name, setName] = useState('');
  const [max, setMax] = useState('');
  const [doc, setDoc] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: qk.leaveReasons });
  const add = useMutation({
    mutationFn: () =>
      leaveRulesApi.addReason({ leaveTypeId: type.id, name: name.trim(), maxDays: max ? Number(max) : null, requiresDocument: doc, isActive: true }),
    onSuccess: () => {
      setName('');
      setMax('');
      setDoc(false);
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const toggle = useMutation({
    mutationFn: (r: (typeof reasons)[number]) => leaveRulesApi.updateReason(r.id, { ...r, isActive: !r.isActive }),
    onSuccess: refresh,
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({ mutationFn: leaveRulesApi.deleteReason, onSuccess: refresh, onError: (e) => toast.error(e) });
  return (
    <Modal open onClose={onClose} title={`Reasons for ${type.name}`} description="When a leave type has reasons, people must pick one.">
      <ul className="mb-4 divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
        {reasons.map((r) => (
          <li key={r.id} className="flex items-center gap-2 px-3 py-2 text-sm">
            <span className="flex-1">
              {r.name}
              {r.maxDays != null && <span className="ms-2 text-xs text-zinc-500 dark:text-zinc-400">up to {r.maxDays} days</span>}
              {r.requiresDocument && <Badge className="ms-2">Document</Badge>}
              {!r.isActive && <Badge className="ms-2">Off</Badge>}
            </span>
            <Button size="sm" variant="ghost" onClick={() => toggle.mutate(r)}>
              {r.isActive ? 'Turn off' : 'Turn on'}
            </Button>
            <IconButton label="Delete reason" onClick={() => remove.mutate(r.id)}>
              <Trash2 className="size-4" />
            </IconButton>
          </li>
        ))}
        {!reasons.length && <li className="px-3 py-3 text-sm text-zinc-500 dark:text-zinc-400">No reasons: people just describe it.</li>}
      </ul>
      <div className="grid grid-cols-[1fr_7rem] gap-2">
        <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="e.g. Fever, Surgery" aria-label="Reason" />
        <Input type="number" min={0.5} step={0.5} value={max} onChange={(e) => setMax(e.target.value)} placeholder="Max days" aria-label="Max days" />
      </div>
      <div className="mt-2 flex items-center justify-between">
        <Checkbox label="Needs a document" checked={doc} onChange={(e) => setDoc(e.target.checked)} />
        <Button size="sm" disabled={!name.trim()} loading={add.isPending} onClick={() => add.mutate()}>
          Add reason
        </Button>
      </div>
    </Modal>
  );
}
