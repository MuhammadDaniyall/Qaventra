import { useState, type ReactNode } from 'react';
import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { Pencil, Trash2 } from 'lucide-react';
import { ApiError, errorMessage } from '../../api/client';
import { Button, ConfirmDialog, IconButton, Modal, FormError, useToast } from '../../components/ui';
import { HistoryButton } from '../../components/engine';

/** Save mutation for a settings modal: toasts, invalidates, collects form errors. */
export function useSaveMutation<T>(opts: { save: (value: T) => Promise<unknown>; invalidate: QueryKey[]; successMessage: string; onSuccess: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const mutation = useMutation({
    mutationFn: opts.save,
    onSuccess: () => {
      toast.success(opts.successMessage);
      opts.invalidate.forEach((k) => qc.invalidateQueries({ queryKey: k }));
      opts.onSuccess();
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });
  return { mutation, error, setError, fieldErrors, setFieldErrors };
}

export function EditModal({
  open,
  title,
  onClose,
  onSubmit,
  saving,
  error,
  children,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  onSubmit: () => void;
  saving: boolean;
  error: string | null;
  children: ReactNode;
}) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="settings-edit-form" loading={saving}>
            Save
          </Button>
        </>
      }
    >
      <form
        id="settings-edit-form"
        noValidate
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit();
        }}
      >
        <FormError message={error} />
        {children}
      </form>
    </Modal>
  );
}

export function RowActions({
  onEdit,
  onDelete,
  label,
  history,
}: {
  onEdit?: () => void;
  onDelete?: () => void;
  label: string;
  /** Show a change-history button for this record. */
  history?: { type: string; id: string };
}) {
  return (
    <div className="flex justify-end gap-1">
      {history && <HistoryButton entityType={history.type} entityId={history.id} label={`History of ${label}`} />}
      {onEdit && (
        <IconButton label={`Edit ${label}`} onClick={onEdit}>
          <Pencil className="size-4" />
        </IconButton>
      )}
      {onDelete && (
        <IconButton label={`Delete ${label}`} onClick={onDelete} className="hover:text-red-600 dark:hover:text-red-400">
          <Trash2 className="size-4" />
        </IconButton>
      )}
    </div>
  );
}

/** Confirm + perform a delete. */
export function DeleteConfirm<T extends { id: string }>({
  item,
  name,
  remove,
  invalidate,
  onClose,
}: {
  item: T | null;
  name: string;
  remove: (id: string) => Promise<unknown>;
  invalidate: QueryKey[];
  onClose: () => void;
}) {
  const toast = useToast();
  const qc = useQueryClient();
  const del = useMutation({
    mutationFn: (id: string) => remove(id),
    onSuccess: () => {
      toast.success(`${name} deleted`);
      invalidate.forEach((k) => qc.invalidateQueries({ queryKey: k }));
      onClose();
    },
    onError: (err) => toast.error(err),
  });
  return (
    <ConfirmDialog
      open={!!item}
      title={`Delete ${name}?`}
      message="This cannot be undone."
      confirmLabel="Delete"
      tone="danger"
      loading={del.isPending}
      onConfirm={() => item && del.mutate(item.id)}
      onClose={onClose}
    />
  );
}
