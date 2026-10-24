import { useState } from 'react';
import { Layers, Plus } from 'lucide-react';
import { orgApi } from '../../api/organization';
import { useDepartments, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, EmptyState, ErrorState, Field, Input, Select, SkeletonRows, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import type { Department } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function DepartmentsTab() {
  const list = useDepartments();
  const [editing, setEditing] = useState<Department | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Department | null>(null);

  return (
    <Card>
      <CardHeader
        title="Departments"
        description="Group employees for reporting and filtering."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add department
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={4} cols={3} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={Layers}
          title="No departments yet"
          action={<Button variant="secondary" onClick={() => setEditing('new')}>Add department</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Name</Th>
              <Th>Code</Th>
              <Th>Head</Th>
              <Th className="text-end">Employees</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((d) => (
              <Tr key={d.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{d.name}</Td>
                <Td className="font-mono text-xs">{d.code ?? '—'}</Td>
                <Td>{d.head ?? <span className="text-zinc-500 dark:text-zinc-400">—</span>}</Td>
                <Td className="text-end tabular-nums">{d.employees}</Td>
                <Td>
                  <RowActions history={{ type: 'Department', id: d.id }} label={d.name} onEdit={() => setEditing(d)} onDelete={() => setDeleting(d)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <DepartmentModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name="Department"
        remove={orgApi.deleteDepartment}
        invalidate={[qk.departments]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function DepartmentModal({ item, onClose }: { item: Department | null; onClose: () => void }) {
  const [name, setName] = useState(item?.name ?? '');
  const [code, setCode] = useState(item?.code ?? '');
  const [head, setHead] = useState(item?.headEmployeeId ?? '');
  const people = useEmployeeLookup();
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: { name: string; code: string | null; headEmployeeId: string | null }) => (item ? orgApi.updateDepartment(item.id, b) : orgApi.createDepartment(b)),
    invalidate: [qk.departments, qk.dashboard],
    successMessage: item ? 'Department updated' : 'Department added',
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? 'Edit department' : 'Add department'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        if (!name.trim()) return setFieldErrors({ name: 'Name is required.' });
        mutation.mutate({ name: name.trim(), code: code.trim() || null, headEmployeeId: head || null });
      }}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} placeholder="Engineering" />
      </Field>
      <Field label="Code" hint="Optional short code." error={fieldErrors.code}>
        <Input value={code} maxLength={30} onChange={(e) => setCode(e.target.value)} placeholder="ENG" />
      </Field>
      <Field label="Head" hint="Can approve requests when an approval chain has a Department head step.">
        <Select value={head} onChange={(e) => setHead(e.target.value)}>
          <option value="">No head</option>
          {people.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </Select>
      </Field>
    </EditModal>
  );
}
