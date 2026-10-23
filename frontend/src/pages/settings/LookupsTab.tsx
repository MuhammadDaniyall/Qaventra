import { useState } from 'react';
import { Plus, Tags } from 'lucide-react';
import { employeeFileApi, LOOKUP_KINDS, useLookups, type LookupKind, type OrgLookup } from '../../api/employeeFile';
import { useDepartments, useEmployeeLookup } from '../../api/hooks';
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
  Input,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

/** Divisions, grades, cost centers, regions, groups, sub-departments and exit types: the company's own lists. */
export function LookupsTab() {
  const [kind, setKind] = useState<LookupKind>('Division');
  const all = useLookups();
  const departments = useDepartments();
  const [editing, setEditing] = useState<OrgLookup | 'new' | null>(null);
  const [deleting, setDeleting] = useState<OrgLookup | null>(null);
  const meta = LOOKUP_KINDS.find((k) => k.kind === kind)!;
  const rows = (all.data ?? []).filter((x) => x.kind === kind);
  const deptName = (id: string | null) => departments.data?.find((d) => d.id === id)?.name ?? '';

  return (
    <div className="grid gap-5 lg:grid-cols-[14rem_1fr]">
      <nav className="flex gap-1 overflow-x-auto lg:flex-col" aria-label="Lists">
        {LOOKUP_KINDS.map((k) => (
          <button
            key={k.kind}
            type="button"
            onClick={() => setKind(k.kind)}
            className={cn(
              'flex shrink-0 items-center justify-between gap-2 rounded-lg px-3 py-2 text-left text-sm',
              kind === k.kind
                ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-900'
                : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800',
            )}
          >
            {k.plural}
            <span className="text-xs opacity-60">{(all.data ?? []).filter((x) => x.kind === k.kind).length || ''}</span>
          </button>
        ))}
      </nav>
      <Card>
        <CardHeader
          title={meta.plural}
          description={meta.hint}
          actions={
            <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
              Add {meta.label.toLowerCase()}
            </Button>
          }
        />
        {all.isLoading ? (
          <SkeletonRows rows={4} cols={3} />
        ) : all.isError ? (
          <ErrorState error={all.error} onRetry={() => all.refetch()} />
        ) : !rows.length ? (
          <EmptyState icon={Tags} title={`No ${meta.plural.toLowerCase()} yet`} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Name</Th>
                <Th>Code</Th>
                {kind === 'SubDepartment' && <Th>Department</Th>}
                <Th className="text-right">Employees</Th>
                <Th className="text-right">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </THead>
            <TBody>
              {rows.map((x) => (
                <Tr key={x.id}>
                  <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                    {x.name} {!x.isActive && <Badge className="ml-1">Off</Badge>}
                  </Td>
                  <Td className="font-mono text-xs">{x.code}</Td>
                  {kind === 'SubDepartment' && <Td>{deptName(x.parentId)}</Td>}
                  <Td className="text-right tabular-nums">{x.employees}</Td>
                  <Td>
                    <RowActions label={x.name} onEdit={() => setEditing(x)} onDelete={() => setDeleting(x)} />
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {editing && <LookupModal kind={kind} item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name={meta.label} remove={employeeFileApi.deleteLookup} invalidate={[qk.lookups]} onClose={() => setDeleting(null)} />
    </div>
  );
}

function LookupModal({ kind, item, onClose }: { kind: LookupKind; item: OrgLookup | null; onClose: () => void }) {
  const meta = LOOKUP_KINDS.find((k) => k.kind === kind)!;
  const departments = useDepartments();
  const people = useEmployeeLookup(kind === 'Division' || kind === 'SubDepartment');
  const [f, setF] = useState({
    name: item?.name ?? '',
    code: item?.code ?? '',
    parentId: item?.parentId ?? '',
    headEmployeeId: item?.headEmployeeId ?? '',
    isActive: item?.isActive ?? true,
  });
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => {
      const b = {
        kind,
        name: f.name.trim(),
        code: f.code.trim() || null,
        parentId: f.parentId || null,
        headEmployeeId: f.headEmployeeId || null,
        isActive: f.isActive,
      };
      return item ? employeeFileApi.updateLookup(item.id, b) : employeeFileApi.addLookup(b);
    },
    invalidate: [qk.lookups],
    successMessage: item ? 'Saved' : `${meta.label} added`,
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? `Edit ${meta.label.toLowerCase()}` : `Add ${meta.label.toLowerCase()}`}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        if (!f.name.trim()) return setFieldErrors({ name: 'Name is required.' });
        if (kind === 'SubDepartment' && !f.parentId) return setFieldErrors({ parentId: 'Choose the department.' });
        mutation.mutate(undefined);
      }}
    >
      {kind === 'SubDepartment' && (
        <Field label="Department" required error={fieldErrors.parentId}>
          <Select value={f.parentId} onChange={(e) => setF({ ...f, parentId: e.target.value })}>
            <option value="">Choose…</option>
            {(departments.data ?? []).map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <div className="grid grid-cols-[1fr_8rem] gap-3">
        <Field label="Name" required error={fieldErrors.name}>
          <Input value={f.name} maxLength={150} onChange={(e) => setF({ ...f, name: e.target.value })} />
        </Field>
        <Field label="Code">
          <Input value={f.code} maxLength={30} onChange={(e) => setF({ ...f, code: e.target.value })} />
        </Field>
      </div>
      {(kind === 'Division' || kind === 'SubDepartment') && (
        <Field label="Head">
          <Select value={f.headEmployeeId} onChange={(e) => setF({ ...f, headEmployeeId: e.target.value })}>
            <option value="">No one</option>
            {(people.data ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <Checkbox label="In use (turn off to hide it from new choices)" checked={f.isActive} onChange={(e) => setF({ ...f, isActive: e.target.checked })} />
    </EditModal>
  );
}
