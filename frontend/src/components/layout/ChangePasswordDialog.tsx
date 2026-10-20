import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { authApi } from '../../api/auth';
import { ApiError, errorMessage } from '../../api/client';
import { Button, Field, FormError, Input, Modal, useToast } from '../ui';

export function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const toast = useToast();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const reset = () => {
    setCurrent('');
    setNext('');
    setConfirm('');
    setError(null);
    setFieldErrors({});
  };

  const mutation = useMutation({
    mutationFn: () => authApi.changePassword({ currentPassword: current, newPassword: next }),
    onSuccess: () => {
      toast.success('Password changed');
      reset();
      onClose();
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    const fe: Record<string, string> = {};
    if (next.length < 8) fe.newPassword = 'Use at least 8 characters.';
    if (next !== confirm) fe.confirm = 'Passwords do not match.';
    setFieldErrors(fe);
    if (Object.keys(fe).length) return;
    mutation.mutate();
  };

  const close = () => {
    reset();
    onClose();
  };

  return (
    <Modal
      open={open}
      onClose={close}
      title="Change password"
      description="Use at least 8 characters. You'll stay signed in on this device."
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={close}>
            Cancel
          </Button>
          <Button type="submit" form="change-password-form" loading={mutation.isPending}>
            Update password
          </Button>
        </>
      }
    >
      <form id="change-password-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <Field label="Current password" required error={fieldErrors.currentPassword}>
          <Input type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} required />
        </Field>
        <Field label="New password" required error={fieldErrors.newPassword}>
          <Input type="password" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} required />
        </Field>
        <Field label="Confirm new password" required error={fieldErrors.confirm}>
          <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
        </Field>
      </form>
    </Modal>
  );
}
