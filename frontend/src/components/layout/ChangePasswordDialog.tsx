import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { authApi } from '../../api/auth';
import { ApiError, errorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Button, Field, FormError, Input, Modal, useToast } from '../ui';

/**
 * `forced`: shown after signing in with a temporary password from HR. It cannot be dismissed; signing out is the only
 * other way out.
 */
export function ChangePasswordDialog({ open, onClose, forced = false }: { open: boolean; onClose: () => void; forced?: boolean }) {
  const toast = useToast();
  const { passwordChanged, logout } = useAuth();
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
      passwordChanged();
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
    if (forced) return;
    reset();
    onClose();
  };

  return (
    <Modal
      open={open}
      onClose={close}
      title={forced ? 'Choose your own password' : 'Change password'}
      description={
        forced
          ? 'You signed in with a temporary password from HR. Pick a new one of at least 8 characters to continue.'
          : "Use at least 8 characters. You'll stay signed in on this device."
      }
      size="sm"
      hideClose={forced}
      footer={
        <>
          <Button variant="secondary" onClick={forced ? () => void logout() : close}>
            {forced ? 'Sign out' : 'Cancel'}
          </Button>
          <Button type="submit" form="change-password-form" loading={mutation.isPending}>
            Update password
          </Button>
        </>
      }
    >
      <form id="change-password-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <Field label={forced ? 'Temporary password' : 'Current password'} required error={fieldErrors.currentPassword}>
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
