import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FilePlus2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { lettersApi } from '../../api/letters';
import { Button, Card, CardHeader, Field, FormError, Input, Modal, Select, StatusBadge, Table, TBody, Td, Tr, useToast } from '../../components/ui';
import { formatDate } from '../../lib/format';

const KEY = ['letters', 'requests'] as const;

/** Ask HR for a letter (salary certificate, experience letter…); it is approved first. */
export function RequestLetterButton() {
  const options = useQuery({ queryKey: [...KEY, 'self-service'], queryFn: lettersApi.selfService });
  const [open, setOpen] = useState(false);
  if (!options.data?.length) return null;
  return (
    <>
      <Button icon={<FilePlus2 className="size-4" />} onClick={() => setOpen(true)}>
        Request a letter
      </Button>
      {open && <RequestModal options={options.data} onClose={() => setOpen(false)} />}
    </>
  );
}

function RequestModal({ options, onClose }: { options: { id: string; name: string }[]; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [templateId, setTemplateId] = useState(options[0].id);
  const [reason, setReason] = useState('');
  const save = useMutation({
    mutationFn: () => lettersApi.request(templateId, reason.trim() || null),
    onSuccess: () => {
      toast.success('Requested', 'You will find it here once approved.');
      qc.invalidateQueries({ queryKey: ['letters'] });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Request a letter"
      footer={
        <Button loading={save.isPending} onClick={() => save.mutate()}>
          Send request
        </Button>
      }
    >
      <div className="space-y-4">
        <Field label="Letter">
          <Select value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
            {options.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="What is it for?">
          <Input value={reason} maxLength={500} placeholder="Bank loan, visa…" onChange={(e) => setReason(e.target.value)} />
        </Field>
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

/** Letters the person asked for, and any waiting for approval. */
export function MyLetterRequests() {
  const list = useQuery({ queryKey: [...KEY, 'mine'], queryFn: lettersApi.myRequests });
  const open = (list.data ?? []).filter((l) => l.status !== 'Issued');
  if (!open.length) return null;
  return (
    <Card className="mb-6">
      <CardHeader title="My requests" />
      <Table>
        <TBody>
          {open.map((l) => (
            <Tr key={l.id}>
              <Td>{l.subject}</Td>
              <Td className="text-sm text-zinc-500 dark:text-zinc-400">{l.requestReason}</Td>
              <Td className="text-sm">{formatDate(l.issuedOn)}</Td>
              <Td>
                <StatusBadge status={l.status === 'Pending' ? 'Pending' : 'Rejected'} />
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}
