import { useState } from 'react';
import { Briefcase, Plus } from 'lucide-react';
import { orgApi } from '../../api/organization';
import { useDesignations } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, EmptyState, ErrorState, Field, Input, SkeletonRows, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import type { Designation } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function DesignationsTab() {
  const list = useDesignations();
  const [editing, setEditing] = useState<Designation | 'new' | null>(null);
  const [deleting, setDeleting] = useState<Designation | null>(null);

  return (
    <Card>
      <CardHeader
        title="Designations"
        description="Job titles used across the company."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add designation
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={4} cols={2} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={Briefcase}
          title="No designations yet"
          action={<Button variant="secondary" onClick={() => setEditing('new')}>Add designation</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Title</Th>
              <Th className="text-right">Employees</Th>
              <Th className="text-right">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((d) => (
              <Tr key={d.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{d.title}</Td>
                <Td className="text-right tabular-nums">{d.employees}</Td>
                <Td>
                  <RowActions history={{ type: 'Designation', id: d.id }} label={d.title} onEdit={() => setEditing(d)} onDelete={() => setDeleting(d)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <DesignationModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name="Designation"
        remove={orgApi.deleteDesignation}
        invalidate={[qk.designations]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function DesignationModal({ item, onClose }: { item: Designation | null; onClose: () => void }) {
  const [title, setTitle] = useState(item?.title ?? '');
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: { title: string }) => (item ? orgApi.updateDesignation(item.id, b) : orgApi.createDesignation(b)),
    invalidate: [qk.designations],
    successMessage: item ? 'Designation updated' : 'Designation added',
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? 'Edit designation' : 'Add designation'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        if (!title.trim()) return setFieldErrors({ title: 'Title is required.' });
        mutation.mutate({ title: title.trim() });
      }}
    >
      <Field label="Title" required error={fieldErrors.title}>
        <Input value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} placeholder="Software Engineer" />
      </Field>
    </EditModal>
  );
}
