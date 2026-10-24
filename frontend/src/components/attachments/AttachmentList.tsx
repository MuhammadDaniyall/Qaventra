import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, FileImage, FileSpreadsheet, FileText, Paperclip, Trash2, Upload } from 'lucide-react';
import {
  ATTACHMENT_ACCEPT,
  attachmentProblem,
  attachmentsApi,
  DOCUMENT_CATEGORIES,
  formatBytes,
  type Attachment,
  type AttachmentOwner,
  type DocumentCategory,
} from '../../api/attachments';
import { cn } from '../../lib/cn';
import { formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { Badge, ConfirmDialog, IconButton, Input, Select, Skeleton, useToast } from '../ui';

export const attachmentsKey = (owner: AttachmentOwner, ownerId: string) => ['attachments', owner, ownerId] as const;

function FileIcon({ a }: { a: Attachment }) {
  const Icon = a.contentType.startsWith('image/') ? FileImage : /sheet|excel/.test(a.contentType) ? FileSpreadsheet : FileText;
  return <Icon className="size-5 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />;
}

function expiryBadge(expiresOn: string | null) {
  if (!expiresOn) return null;
  const today = todayIso();
  const soon = new Date(Date.parse(today) + 30 * 86_400_000).toISOString().slice(0, 10);
  if (expiresOn < today) return <Badge tone="red">Expired {formatDate(expiresOn)}</Badge>;
  if (expiresOn <= soon) return <Badge tone="amber">Expires {formatDate(expiresOn)}</Badge>;
  return <Badge>Valid till {formatDate(expiresOn)}</Badge>;
}

/**
 * Files on a request or an employee record: list, open, upload (drop or pick) and remove, as the server allows.
 * For employee documents it also asks for the document type and an optional expiry date.
 */
export function AttachmentList({
  owner,
  ownerId,
  emptyText = 'No files attached.',
  className,
}: {
  owner: AttachmentOwner;
  ownerId: string;
  emptyText?: string;
  className?: string;
}) {
  const toast = useToast();
  const qc = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [category, setCategory] = useState<DocumentCategory>('Cnic');
  const [expiresOn, setExpiresOn] = useState('');
  const [deleting, setDeleting] = useState<Attachment | null>(null);
  const isDocs = owner === 'EmployeeDocument';
  const list = useQuery({ queryKey: attachmentsKey(owner, ownerId), queryFn: () => attachmentsApi.list(owner, ownerId) });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: attachmentsKey(owner, ownerId) });
    if (owner === 'Leave') qc.invalidateQueries({ queryKey: ['leave'] });
  };
  const upload = useMutation({
    mutationFn: async (files: File[]) => {
      for (const f of files) await attachmentsApi.upload(owner, ownerId, f, isDocs ? { category, expiresOn: expiresOn || undefined } : undefined);
    },
    onSuccess: (_, files) => {
      toast.success(files.length === 1 ? 'File uploaded' : `${files.length} files uploaded`);
      setExpiresOn('');
      refresh();
    },
    onError: (err) => {
      toast.error(err);
      refresh();
    },
  });
  const remove = useMutation({
    mutationFn: (id: string) => attachmentsApi.remove(id),
    onSuccess: () => {
      toast.success('File removed');
      setDeleting(null);
      refresh();
    },
    onError: (err) => toast.error(err),
  });

  const pick = (files: FileList | null) => {
    const chosen = Array.from(files ?? []);
    const problem = chosen.map(attachmentProblem).find(Boolean);
    if (problem) return toast.error(problem);
    if (chosen.length) upload.mutate(chosen);
  };

  if (list.isLoading) return <Skeleton className={cn('h-16 w-full', className)} />;
  if (list.isError || !list.data) return <p className={cn('text-sm text-red-600', className)}>Could not load files.</p>;

  return (
    <div className={cn('space-y-3', className)}>
      {list.data.items.length === 0 && !list.data.canAdd && <p className="text-sm text-zinc-500 dark:text-zinc-400">{emptyText}</p>}

      {list.data.items.length > 0 && (
        <ul className="divide-y divide-zinc-100 rounded-xl border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
          {list.data.items.map((a) => (
            <li key={a.id} className="flex items-center gap-3 px-3 py-2.5">
              <FileIcon a={a} />
              <div className="min-w-0 flex-1">
                <button
                  type="button"
                  onClick={() => attachmentsApi.open(a).catch((err) => toast.error(err))}
                  className="block max-w-full truncate text-start text-sm font-medium text-zinc-900 hover:text-accent-700 hover:underline dark:text-zinc-100"
                >
                  {a.fileName}
                </button>
                <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-zinc-500 dark:text-zinc-400">
                  {a.category && <span>{DOCUMENT_CATEGORIES.find((c) => c.value === a.category)?.label}</span>}
                  <span>{formatBytes(a.sizeBytes)}</span>
                  <span>
                    {a.uploadedByName ? `${a.uploadedByName}, ` : ''}
                    {formatDateTimeUtc(a.createdAtUtc)}
                  </span>
                  {expiryBadge(a.expiresOn)}
                </p>
              </div>
              {a.canDelete && (
                <IconButton label={`Remove ${a.fileName}`} onClick={() => setDeleting(a)}>
                  <Trash2 className="size-4" />
                </IconButton>
              )}
            </li>
          ))}
        </ul>
      )}

      {list.data.canAdd && (
        <div className="space-y-2">
          {isDocs && (
            <div className="flex flex-wrap gap-2">
              <Select aria-label="Document type" value={category} onChange={(e) => setCategory(e.target.value as DocumentCategory)} className="w-48">
                {DOCUMENT_CATEGORIES.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </Select>
              <label className="flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-300">
                Expires
                <Input type="date" value={expiresOn} onChange={(e) => setExpiresOn(e.target.value)} className="w-40" aria-label="Expiry date (optional)" />
              </label>
            </div>
          )}
          <button
            type="button"
            disabled={upload.isPending}
            onClick={() => input.current?.click()}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragging(false);
              pick(e.dataTransfer.files);
            }}
            className={cn(
              'flex w-full items-center justify-center gap-2 rounded-xl border border-dashed px-3 py-4 text-sm transition-colors disabled:opacity-60',
              dragging
                ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10'
                : 'border-zinc-300 text-zinc-500 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:text-zinc-400',
            )}
          >
            {upload.isPending ? <Paperclip className="size-4 animate-pulse" aria-hidden="true" /> : <Upload className="size-4" aria-hidden="true" />}
            {upload.isPending ? 'Uploading…' : (
              <>
                Attach files <span className="hidden sm:inline">or drop them here</span>
                <span className="text-xs text-zinc-500 dark:text-zinc-400">PDF, images, Word, Excel · 5 MB each</span>
              </>
            )}
          </button>
          <input
            ref={input}
            type="file"
            multiple
            accept={ATTACHMENT_ACCEPT}
            className="sr-only"
            tabIndex={-1}
            onChange={(e) => {
              pick(e.target.files);
              e.target.value = '';
            }}
          />
        </div>
      )}

      <ConfirmDialog
        open={!!deleting}
        title="Remove this file?"
        message={
          <span className="flex items-start gap-2">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-500" aria-hidden="true" />
            {deleting?.fileName} will be deleted permanently.
          </span>
        }
        confirmLabel="Remove"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}
