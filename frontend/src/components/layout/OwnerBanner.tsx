import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, LogOut } from 'lucide-react';
import { useAuth } from '../../auth/AuthContext';
import { Spinner } from '../ui';

/** Persistent strip shown while the owner is inside a company. */
export function OwnerBanner({ companyName }: { companyName: string }) {
  const { exitCompany } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);

  const exit = async () => {
    setBusy(true);
    const me = await exitCompany();
    setBusy(false);
    navigate(me ? '/platform/companies' : '/login', { replace: true });
  };

  return (
    <div
      role="status"
      className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 bg-amber-400 px-4 py-2 text-sm font-medium text-zinc-950"
    >
      <span className="inline-flex items-center gap-2">
        <Eye className="size-4 shrink-0" aria-hidden="true" />
        You are viewing <strong className="font-semibold">{companyName}</strong> as Owner
      </span>
      <span aria-hidden="true">·</span>
      <button
        type="button"
        onClick={exit}
        disabled={busy}
        className="inline-flex items-center gap-1.5 rounded-md bg-zinc-950 px-2.5 py-1 text-xs font-semibold text-white hover:bg-zinc-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-zinc-950 disabled:opacity-70"
      >
        {busy ? <Spinner className="size-3.5" /> : <LogOut className="size-3.5" />}
        Exit to platform
      </button>
    </div>
  );
}
