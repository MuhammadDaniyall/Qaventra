import type { ReactNode } from 'react';
import { Button } from './Button';
import { Modal } from './Modal';
import { tx } from '../../lib/i18n';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  tone?: 'primary' | 'danger';
  loading?: boolean;
  onConfirm: () => void;
  onClose: () => void;
  children?: ReactNode;
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  tone = 'primary',
  loading,
  onConfirm,
  onClose,
  children,
}: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      onClose={loading ? () => undefined : onClose}
      title={title}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={loading}>
            {tx('Cancel')}
          </Button>
          <Button variant={tone === 'danger' ? 'danger' : 'primary'} onClick={onConfirm} loading={loading}>
            {tx(confirmLabel)}
          </Button>
        </>
      }
    >
      <div className="space-y-4 text-sm text-zinc-600 dark:text-zinc-300">
        <div>{tx(message)}</div>
        {children}
      </div>
    </Modal>
  );
}
