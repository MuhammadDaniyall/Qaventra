import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Boxes, Package, Plus, Undo2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { assetsApi, type AssetRequest, type Assignment } from '../../api/assets';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardHeader, ConfirmDialog, EmptyState, ErrorState, Field, FormError, Modal, PageHeader, SkeletonRows, Textarea, useToast } from '../../components/ui';
import { formatDate, formatDateTimeUtc } from '../../lib/format';
import { AssignmentCards, RequestAssetModal, requestLabel } from './shared';

/** Everyone: what company property I hold, and my requests. */
export function MyAssetsPage() {
  const navigate = useNavigate();
  const data = useQuery({ queryKey: qk.assetsMy, queryFn: assetsApi.my });
  const handler = useQuery({ queryKey: qk.assetsMe, queryFn: assetsApi.me });
  const [requesting, setRequesting] = useState(false);
  const [returning, setReturning] = useState<Assignment | null>(null);
  return (
    <>
      <PageHeader
        title="My assets"
        description="Company property in your name. Ask for something you need, or to hand something back."
        actions={
          <>
            {handler.data?.isHandler && (
              <Button variant="secondary" icon={<Boxes className="size-4" />} onClick={() => navigate('/assets')}>
                Manage assets
              </Button>
            )}
            <Button icon={<Plus className="size-4" />} onClick={() => setRequesting(true)}>
              Request an asset
            </Button>
          </>
        }
      />
      {data.isLoading ? (
        <Card>
          <SkeletonRows rows={3} cols={3} />
        </Card>
      ) : data.isError ? (
        <Card>
          <ErrorState error={data.error} onRetry={() => data.refetch()} />
        </Card>
      ) : (
        <div className="space-y-6">
          {data.data!.holding.length ? (
            <AssignmentCards
              items={data.data!.holding}
              action={(a) =>
                !a.returnRequested && (
                  <Button variant="ghost" size="sm" icon={<Undo2 className="size-3.5" />} onClick={() => setReturning(a)}>
                    Return
                  </Button>
                )
              }
            />
          ) : (
            <Card>
              <EmptyState icon={Package} title="Nothing in your name" description="Laptops, phones, SIMs and cards the company gives you appear here." />
            </Card>
          )}
          <MyRequests requests={data.data!.requests} />
          {data.data!.returned.length > 0 && (
            <div>
              <h2 className="mb-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Returned</h2>
              <AssignmentCards items={data.data!.returned} />
            </div>
          )}
        </div>
      )}
      {requesting && <RequestAssetModal onClose={() => setRequesting(false)} />}
      {returning && <ReturnModal assignment={returning} onClose={() => setReturning(null)} />}
    </>
  );
}

function MyRequests({ requests }: { requests: AssetRequest[] }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [cancelling, setCancelling] = useState<AssetRequest | null>(null);
  const cancel = useMutation({
    mutationFn: (id: string) => assetsApi.cancel(id),
    onSuccess: () => {
      toast.success('Request cancelled');
      setCancelling(null);
      qc.invalidateQueries({ queryKey: qk.assets });
    },
    onError: (err) => toast.error(err),
  });
  if (!requests.length) return null;
  return (
    <Card>
      <CardHeader title="My requests" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {requests.map((r) => (
          <li key={r.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                {r.kind === 'Return' ? `Return ${r.assetLabel}` : r.type}
                {r.returnBy ? ` · until ${formatDate(r.returnBy)}` : ''}
              </p>
              <p className="truncate text-xs text-zinc-500">
                {r.reason} · {formatDateTimeUtc(r.createdAtUtc)}
                {r.decisionNote ? ` · ${r.decisionNote}` : ''}
              </p>
            </div>
            <Badge tone={r.status === 'Fulfilled' ? 'green' : r.status === 'Rejected' ? 'red' : r.status === 'Cancelled' ? 'gray' : 'amber'}>{requestLabel(r)}</Badge>
            {(r.status === 'Pending' || r.status === 'Approved') && (
              <Button variant="ghost" size="sm" onClick={() => setCancelling(r)}>
                Cancel
              </Button>
            )}
          </li>
        ))}
      </ul>
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this request?"
        message="You can send a new one any time."
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling.id)}
        onClose={() => setCancelling(null)}
      />
    </Card>
  );
}

function ReturnModal({ assignment, onClose }: { assignment: Assignment; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => assetsApi.requestReturn(assignment.id, reason.trim() || null),
    onSuccess: () => {
      toast.success('Return requested', 'The team will collect it from you.');
      qc.invalidateQueries({ queryKey: qk.assets });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Return ${assignment.assetName}`}
      description={`${assignment.tag} · The person who handles ${assignment.type.toLowerCase()}s is told to take it back.`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Ask to return
          </Button>
        </>
      }
    >
      <FormError message={error} />
      <Field label="Reason" hint="Optional">
        <Textarea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Not needed after the project" />
      </Field>
    </Modal>
  );
}
