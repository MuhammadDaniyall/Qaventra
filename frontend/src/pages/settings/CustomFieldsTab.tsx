import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ListPlus, Plus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { customFieldsApi, type CustomField, type CustomFieldType, type CustomValue } from '../../api/engagement';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate } from '../../lib/format';
import { RowActions } from './shared';

const TYPE_LABELS: Record<CustomFieldType, string> = { Text: 'Text', Number: 'Number', Date: 'Date', Dropdown: 'Dropdown', YesNo: 'Yes / No' };

/** HR: extra fields on every employee profile. */
export function CustomFieldsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: qk.customFields, queryFn: customFieldsApi.list });
  const [editing, setEditing] = useState<CustomField | 'new' | null>(null);
  const [deleting, setDeleting] = useState<CustomField | null>(null);
  const remove = useMutation({
    mutationFn: (id: string) => customFieldsApi.remove(id),
    onSuccess: () => {
      toast.success('Field deleted');
      setDeleting(null);
      qc.invalidateQueries({ queryKey: qk.customFields });
      qc.invalidateQueries({ queryKey: qk.employees });
    },
    onError: (err) => toast.error(err),
  });
  return (
    <Card>
      <CardHeader
        title="Custom fields"
        description="Extra details you keep for every employee, shown under Additional information on their profile."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add field
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={ListPlus}
          title="No custom fields"
          description="For example T-shirt size, laptop asset number, vehicle number or next of kin."
          action={<Button variant="secondary" onClick={() => setEditing('new')}>Add field</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Field</Th>
              <Th>Type</Th>
              <Th>Rules</Th>
              <Th className="text-right">Filled</Th>
              <Th className="text-right">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((f) => (
              <Tr key={f.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{f.label}</Td>
                <Td>
                  {TYPE_LABELS[f.type]}
                  {f.type === 'Dropdown' && <p className="text-xs text-zinc-500">{f.options.join(', ')}</p>}
                </Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    {f.isRequired && <Badge tone="amber">Required</Badge>}
                    {f.visibleToEmployee ? <Badge tone="blue">Employee sees it</Badge> : <Badge>HR only</Badge>}
                    {!f.isActive && <Badge tone="red">Hidden</Badge>}
                  </div>
                </Td>
                <Td className="text-right tabular-nums">{f.filled}</Td>
                <Td>
                  <RowActions label={f.label} onEdit={() => setEditing(f)} onDelete={() => setDeleting(f)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <FieldModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!deleting}
        title={`Delete ${deleting?.label}?`}
        message={deleting?.filled ? `The values for ${deleting.filled} employee${deleting.filled === 1 ? '' : 's'} are deleted too. To keep them, edit the field and hide it instead.` : 'Nobody has a value for it yet.'}
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function FieldModal({ item, onClose }: { item: CustomField | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [label, setLabel] = useState(item?.label ?? '');
  const [type, setType] = useState<CustomFieldType>(item?.type ?? 'Text');
  const [options, setOptions] = useState((item?.options ?? []).join('\n'));
  const [required, setRequired] = useState(item?.isRequired ?? false);
  const [visible, setVisible] = useState(item?.visibleToEmployee ?? true);
  const [active, setActive] = useState(item?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = { label: label.trim(), type, options: options.split('\n').map((o) => o.trim()).filter(Boolean), isRequired: required, visibleToEmployee: visible, isActive: active };
      return item ? customFieldsApi.update(item.id, b) : customFieldsApi.create(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Field updated' : 'Field added');
      qc.invalidateQueries({ queryKey: qk.customFields });
      qc.invalidateQueries({ queryKey: qk.employees });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? 'Edit field' : 'Add field'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!label.trim()) return setError('Give the field a name.');
              save.mutate();
            }}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Name" required>
            <Input value={label} maxLength={100} onChange={(e) => setLabel(e.target.value)} placeholder="T-shirt size" />
          </Field>
          <Field label="Type" hint={item?.filled ? 'Fixed once values exist' : undefined}>
            <Select value={type} disabled={!!item?.filled} onChange={(e) => setType(e.target.value as CustomFieldType)}>
              {(Object.keys(TYPE_LABELS) as CustomFieldType[]).map((t) => (
                <option key={t} value={t}>
                  {TYPE_LABELS[t]}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        {type === 'Dropdown' && (
          <Field label="Choices" hint="One per line">
            <Textarea rows={4} value={options} onChange={(e) => setOptions(e.target.value)} placeholder={'Small\nMedium\nLarge'} />
          </Field>
        )}
        <Checkbox label="Required" description="HR must fill it when saving additional information." checked={required} onChange={(e) => setRequired(e.target.checked)} />
        <Checkbox label="Employee can see it" description="Shown read-only on their own profile." checked={visible} onChange={(e) => setVisible(e.target.checked)} />
        {item && <Checkbox label="In use" description="Untick to hide it everywhere without losing the values." checked={active} onChange={(e) => setActive(e.target.checked)} />}
      </div>
    </Modal>
  );
}

function showValue(v: CustomValue) {
  if (!v.value) return <span className="text-zinc-400">—</span>;
  return v.type === 'Date' ? formatDate(v.value) : v.value;
}

/** Custom field values on a profile. HR can edit; others read. Hidden when the company has no fields. */
export function CustomValuesCard({ employeeId, editable }: { employeeId?: string; editable: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = employeeId ? qk.customValues(employeeId) : qk.myCustomValues;
  const values = useQuery({ queryKey: key, queryFn: () => (employeeId ? customFieldsApi.values(employeeId) : customFieldsApi.mine()) });
  const [draft, setDraft] = useState<Record<string, string> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => customFieldsApi.save(employeeId!, Object.fromEntries(Object.entries(draft!).map(([k, v]) => [k, v.trim() || null]))),
    onSuccess: (saved) => {
      qc.setQueryData(key, saved);
      qc.invalidateQueries({ queryKey: qk.customFields });
      setDraft(null);
      toast.success('Additional information saved');
    },
    onError: (err) => setError(errorMessage(err)),
  });
  if (!values.data?.length) return null;
  const set = (id: string, v: string) => setDraft((d) => ({ ...d!, [id]: v }));
  return (
    <Card>
      <CardHeader
        title="Additional information"
        actions={
          editable && !draft ? (
            <Button variant="secondary" size="sm" onClick={() => setDraft(Object.fromEntries(values.data.map((v) => [v.fieldId, v.value])))}>
              Edit
            </Button>
          ) : undefined
        }
      />
      {draft ? (
        <CardBody className="space-y-4">
          <FormError message={error} />
          <div className="grid gap-4 sm:grid-cols-2">
            {values.data.map((f) => (
              <Field key={f.fieldId} label={f.label} required={f.isRequired}>
                {f.type === 'Dropdown' || f.type === 'YesNo' ? (
                  <Select value={draft[f.fieldId] ?? ''} onChange={(e) => set(f.fieldId, e.target.value)}>
                    <option value="">Not set</option>
                    {(f.type === 'YesNo' ? ['Yes', 'No'] : f.options).map((o) => (
                      <option key={o} value={o}>
                        {o}
                      </option>
                    ))}
                  </Select>
                ) : (
                  <Input
                    type={f.type === 'Number' ? 'number' : f.type === 'Date' ? 'date' : 'text'}
                    value={draft[f.fieldId] ?? ''}
                    maxLength={500}
                    onChange={(e) => set(f.fieldId, e.target.value)}
                  />
                )}
              </Field>
            ))}
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setDraft(null)}>
              Cancel
            </Button>
            <Button loading={save.isPending} onClick={() => { setError(null); save.mutate(); }}>
              Save
            </Button>
          </div>
        </CardBody>
      ) : (
        <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {values.data.map((v) => (
            <div key={v.fieldId} className="grid grid-cols-1 gap-1 px-5 py-3 sm:grid-cols-[180px_1fr] sm:gap-4">
              <dt className="text-sm text-zinc-500 dark:text-zinc-400">{v.label}</dt>
              <dd className="text-sm text-zinc-900 dark:text-zinc-100">{showValue(v)}</dd>
            </div>
          ))}
        </dl>
      )}
    </Card>
  );
}
