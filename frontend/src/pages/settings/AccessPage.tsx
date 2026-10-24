import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, ShieldCheck } from 'lucide-react';
import { accessApi, type AccessTemplate } from '../../api/engine';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  Field,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { roleLabel } from '../../lib/format';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

const KEY = ['access'] as const;

/** Company admin: access templates (sets of HR modules) and who has which. */
export function AccessPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const templates = useQuery({ queryKey: [...KEY, 'templates'], queryFn: accessApi.templates });
  const modules = useQuery({ queryKey: [...KEY, 'modules'], queryFn: accessApi.modules, staleTime: Infinity });
  const users = useQuery({ queryKey: [...KEY, 'users'], queryFn: accessApi.users });
  const [editing, setEditing] = useState<AccessTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AccessTemplate | null>(null);
  const set = useMutation({
    mutationFn: ({ userId, templateId }: { userId: string; templateId: string | null }) => accessApi.setTemplate(userId, templateId),
    onSuccess: () => {
      toast.success('Access updated', 'It applies from their next page load.');
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });
  const label = (k: string) => modules.data?.find((m) => m.key === k)?.label ?? k;

  return (
    <>
      <PageHeader
        title="Access"
        description="Templates limit an HR user to some modules (e.g. a payroll officer), or open a module's HR screens to a manager or employee. People without a template keep their role's usual access."
      />
      <Card>
        <CardHeader
          title="Templates"
          actions={
            <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
              New template
            </Button>
          }
        />
        {templates.isLoading ? (
          <SkeletonRows rows={3} cols={3} />
        ) : !templates.data?.length ? (
          <EmptyState icon={ShieldCheck} title="No templates" description="Everyone has their role's usual access." />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Template</Th>
                <Th>Modules</Th>
                <Th className="text-end">Users</Th>
                <Th className="text-end">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </THead>
            <TBody>
              {templates.data.map((t) => (
                <Tr key={t.id}>
                  <Td className="font-medium">
                    {t.name}
                    {t.description && <p className="text-xs font-normal text-zinc-500 dark:text-zinc-400">{t.description}</p>}
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      {t.modules.map((m) => (
                        <Badge key={m}>{label(m)}</Badge>
                      ))}
                    </div>
                  </Td>
                  <Td className="text-end tabular-nums">{t.users}</Td>
                  <Td>
                    <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      <Card className="mt-6">
        <CardHeader title="Users" description="The company admin always has full access and is not listed." />
        {users.isLoading ? (
          <SkeletonRows rows={5} cols={3} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>User</Th>
                <Th>Role</Th>
                <Th className="w-64">Template</Th>
              </tr>
            </THead>
            <TBody>
              {(users.data ?? []).map((u) => (
                <Tr key={u.userId}>
                  <Td>
                    {u.name} <span className="text-xs text-zinc-500 dark:text-zinc-400">{u.email}</span>
                    {!u.isActive && <Badge className="ms-2">Off</Badge>}
                  </Td>
                  <Td>{roleLabel(u.role)}</Td>
                  <Td>
                    <Select
                      aria-label={`Template for ${u.name}`}
                      value={u.accessTemplateId ?? ''}
                      onChange={(e) => set.mutate({ userId: u.userId, templateId: e.target.value || null })}
                    >
                      <option value="">Usual access for the role</option>
                      {(templates.data ?? []).map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.name}
                        </option>
                      ))}
                    </Select>
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {editing && <TemplateModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Template" remove={accessApi.deleteTemplate} invalidate={[KEY]} onClose={() => setDeleting(null)} />
    </>
  );
}

function TemplateModal({ item, onClose }: { item: AccessTemplate | null; onClose: () => void }) {
  const modules = useQuery({ queryKey: [...KEY, 'modules'], queryFn: accessApi.modules, staleTime: Infinity });
  const [name, setName] = useState(item?.name ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [picked, setPicked] = useState<string[]>(item?.modules ?? []);
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => {
      const b = { name: name.trim(), description: description.trim() || null, modules: picked };
      return item ? accessApi.updateTemplate(item.id, b) : accessApi.addTemplate(b);
    },
    invalidate: [KEY],
    successMessage: 'Template saved',
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? 'Edit template' : 'New template'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!name.trim()) fe.name = 'Name is required.';
        if (!picked.length) fe.modules = 'Tick at least one module.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate(undefined);
      }}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={name} maxLength={100} placeholder="Payroll officer" onChange={(e) => setName(e.target.value)} />
      </Field>
      <Field label="Description">
        <Input value={description} maxLength={300} onChange={(e) => setDescription(e.target.value)} />
      </Field>
      <Field label="Modules" error={fieldErrors.modules}>
        <div className="grid gap-2 sm:grid-cols-2">
          {(modules.data ?? []).map((m) => (
            <Checkbox
              key={m.key}
              label={m.label}
              checked={picked.includes(m.key)}
              onChange={() => setPicked((x) => (x.includes(m.key) ? x.filter((k) => k !== m.key) : [...x, m.key]))}
            />
          ))}
        </div>
      </Field>
    </EditModal>
  );
}
