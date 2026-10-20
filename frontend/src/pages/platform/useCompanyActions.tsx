import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { platformApi } from '../../api/platform';
import { useAuth } from '../../auth/AuthContext';
import { ConfirmDialog, useToast } from '../../components/ui';
import type { Company, CompanyAdminCredentials } from '../../types';
import { CredentialsDialog, EditCompanyModal, ResetAdminPasswordModal } from './CompanyDialogs';
import { DeleteCompanyModal, RecordPaymentModal } from './PaymentAndDeleteDialogs';

export type CompanyAction = 'edit' | 'reset' | 'status' | 'payment' | 'delete';
type Action = CompanyAction;

/** Row/detail actions for a company: open, edit, reset admin password, suspend/activate. */
export function useCompanyActions() {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const { enterCompany } = useAuth();
  const [pending, setPending] = useState<{ action: Action; company: Company } | null>(null);
  const [credentials, setCredentials] = useState<CompanyAdminCredentials | null>(null);
  const [opening, setOpening] = useState<string | null>(null);

  const toggle = useMutation({
    mutationFn: (c: Company) => platformApi.setCompanyStatus(c.id, !c.isActive),
    onSuccess: (_, c) => {
      toast.success(c.isActive ? `${c.name} is now inactive` : `${c.name} activated`);
      qc.invalidateQueries({ queryKey: ['platform'] });
      setPending(null);
    },
    onError: (err) => toast.error(err),
  });

  const openCompany = async (c: Company) => {
    setOpening(c.id);
    try {
      await enterCompany(c.id);
      navigate('/dashboard');
    } catch (err) {
      toast.error(err);
    } finally {
      setOpening(null);
    }
  };

  const run = (action: Action, company: Company) => setPending({ action, company });
  const close = () => setPending(null);
  const target = pending?.company;

  const dialogs = (
    <>
      {pending?.action === 'edit' && target && <EditCompanyModal company={target} onClose={close} />}
      {pending?.action === 'payment' && target && <RecordPaymentModal company={target} onClose={close} />}
      {pending?.action === 'delete' && target && (
        <DeleteCompanyModal
          company={target}
          onClose={close}
          onDeleted={() => {
            close();
            navigate('/platform/companies', { replace: true });
          }}
        />
      )}
      {pending?.action === 'reset' && target && (
        <ResetAdminPasswordModal
          company={target}
          onClose={close}
          onReset={(c) => {
            close();
            if (c.generatedPassword) setCredentials(c);
          }}
        />
      )}
      <ConfirmDialog
        open={pending?.action === 'status'}
        title={target?.isActive ? 'Make company inactive?' : 'Activate company?'}
        message={
          target?.isActive ? (
            <>
              All users of <strong>{target.name}</strong> will be signed out immediately and cannot sign in until you activate it
              again. No data is deleted.
            </>
          ) : (
            <>
              People at <strong>{target?.name}</strong> will be able to sign in again.
            </>
          )
        }
        confirmLabel={target?.isActive ? 'Make inactive' : 'Activate'}
        tone={target?.isActive ? 'danger' : 'primary'}
        loading={toggle.isPending}
        onConfirm={() => target && toggle.mutate(target)}
        onClose={close}
      />
      {credentials && (
        <CredentialsDialog credentials={credentials} title="New admin password" onClose={() => setCredentials(null)} />
      )}
    </>
  );

  return { run, openCompany, opening, dialogs, showCredentials: setCredentials };
}
