import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Banknote, Building2, KeyRound, LogIn, MoreHorizontal, Pencil, Plus, Power, Search, Trash2 } from 'lucide-react';
import { platformApi } from '../../api/platform';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  EmptyState,
  ErrorState,
  IconButton,
  Input,
  PageHeader,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { accessKind, planLabel, type AccessKind } from '../../lib/billing';
import { AccessBadge, ExpiresCell, seatsLabel } from './companyUi';
import type { Company, CompanyAdminCredentials } from '../../types';
import { CreateCompanyModal, CredentialsDialog } from './CompanyDialogs';
import { useCompanyActions, type CompanyAction } from './useCompanyActions';

type Filter = 'all' | AccessKind;
const FILTERS: Array<{ value: Filter; label: string }> = [
  { value: 'all', label: 'All' },
  { value: 'active', label: 'Active' },
  { value: 'expiring', label: 'Expiring soon' },
  { value: 'expired', label: 'Expired' },
  { value: 'inactive', label: 'Inactive' },
];

export function CompaniesPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<Filter>('all');
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState<CompanyAdminCredentials | null>(null);
  const companies = useQuery({ queryKey: qk.companies, queryFn: platformApi.companies });
  const actions = useCompanyActions();

  const counts = useMemo(() => {
    const out: Record<Filter, number> = { all: 0, active: 0, expiring: 0, expired: 0, inactive: 0 };
    for (const c of companies.data ?? []) {
      out.all++;
      out[accessKind(c)]++;
    }
    return out;
  }, [companies.data]);

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase();
    return (companies.data ?? [])
      .filter((c) => filter === 'all' || accessKind(c) === filter)
      .filter((c) => !s || [c.name, c.city, c.adminEmail, c.adminName].some((f) => (f ?? '').toLowerCase().includes(s)));
  }, [companies.data, search, filter]);

  return (
    <>
      <PageHeader
        title="Companies"
        description="Create companies, manage their plan and admin, and open any company to work inside it."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
            Create company
          </Button>
        }
      />
      <Card>
        <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 lg:flex-row lg:items-center lg:justify-between dark:border-zinc-800">
          <div className="flex flex-wrap gap-1.5" role="group" aria-label="Filter by access">
            {FILTERS.map((f) => (
              <button
                key={f.value}
                type="button"
                aria-pressed={filter === f.value}
                onClick={() => setFilter(f.value)}
                className={cn(
                  'inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-accent-500',
                  filter === f.value
                    ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                    : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800',
                )}
              >
                {f.label}
                <span className="tabular-nums opacity-70">{counts[f.value]}</span>
              </button>
            ))}
          </div>
          <div className="relative w-full lg:max-w-xs">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input
              aria-label="Search companies"
              placeholder="Search name, city or admin"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>
        {companies.isLoading ? (
          <SkeletonRows rows={6} cols={6} />
        ) : companies.isError ? (
          <ErrorState error={companies.error} onRetry={() => companies.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyState
            icon={Building2}
            title={search || filter !== 'all' ? 'No matching companies' : 'No companies yet'}
            description={search || filter !== 'all' ? undefined : 'Create your first customer company and its Company Super Admin.'}
            action={
              !search && filter === 'all' && (
                <Button icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
                  Create company
                </Button>
              )
            }
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Company</Th>
                <Th>City</Th>
                <Th className="text-right">Employees</Th>
                <Th>Plan</Th>
                <Th>Expires</Th>
                <Th>Company Super Admin</Th>
                <Th>Access</Th>
                <Th>Last activity</Th>
                <Th className="text-right">Actions</Th>
              </tr>
            </THead>
            <TBody>
              {rows.map((c) => (
                <Tr key={c.id} onClick={() => navigate(`/platform/companies/${c.id}`)}>
                  <Td className="font-medium text-zinc-900 dark:text-zinc-100">{c.name}</Td>
                  <Td>{c.city ?? '—'}</Td>
                  <Td className="text-right tabular-nums">{seatsLabel(c)}</Td>
                  <Td>{c.billingCycle ? planLabel(c) : <span className="text-zinc-400">No plan</span>}</Td>
                  <Td>
                    <ExpiresCell company={c} />
                  </Td>
                  <Td>
                    <p className="text-zinc-900 dark:text-zinc-100">{c.adminName ?? '—'}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">{c.adminEmail ?? ''}</p>
                  </Td>
                  <Td>
                    <AccessBadge company={c} />
                  </Td>
                  <Td>{formatDateTimeUtc(c.lastActivityUtc)}</Td>
                  <Td className="text-right" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
                    <div className="inline-flex items-center gap-1">
                      <Button
                        size="sm"
                        variant="secondary"
                        icon={<LogIn className="size-3.5" />}
                        loading={actions.opening === c.id}
                        disabled={!!actions.opening}
                        onClick={() => actions.openCompany(c)}
                      >
                        Open company
                      </Button>
                      <RowMenu company={c} onAction={actions.run} />
                    </div>
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {creating && (
        <CreateCompanyModal
          onClose={() => setCreating(false)}
          onCreated={(res) => {
            setCreating(false);
            if (res.generatedPassword) setCreated(res);
          }}
        />
      )}
      {created && <CredentialsDialog credentials={created} title="Company created" onClose={() => setCreated(null)} />}
      {actions.dialogs}
    </>
  );
}

function RowMenu({ company, onAction }: { company: Company; onAction: (a: CompanyAction, c: Company) => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const item =
    'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-sm text-zinc-700 disabled:cursor-not-allowed disabled:opacity-50 hover:bg-zinc-100 focus-visible:bg-zinc-100 focus-visible:outline-none dark:text-zinc-200 dark:hover:bg-zinc-800 dark:focus-visible:bg-zinc-800';
  const pick = (a: CompanyAction) => {
    setOpen(false);
    onAction(a, company);
  };

  return (
    <div className="relative" ref={ref}>
      <IconButton label={`More actions for ${company.name}`} aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <MoreHorizontal className="size-4" />
      </IconButton>
      {open && (
        <div
          role="menu"
          className="absolute right-0 z-30 mt-1 w-60 animate-pop-in rounded-xl border border-zinc-200 bg-white p-1.5 text-left shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
        >
          <button type="button" role="menuitem" className={item} onClick={() => pick('edit')}>
            <Pencil className="size-4 text-zinc-400" /> Edit company
          </button>
          <button
            type="button"
            role="menuitem"
            className={item}
            disabled={!company.billingCycle}
            title={company.billingCycle ? undefined : 'Set a billing cycle first (Edit company)'}
            onClick={() => pick('payment')}
          >
            <Banknote className="size-4 text-zinc-400" /> Record payment
            {!company.billingCycle && <span className="ml-auto text-[10px] text-zinc-400">no cycle</span>}
          </button>
          <button type="button" role="menuitem" className={item} onClick={() => pick('reset')}>
            <KeyRound className="size-4 text-zinc-400" /> Reset admin password
          </button>
          <div className="my-1 h-px bg-zinc-100 dark:bg-zinc-800" />
          <button
            type="button"
            role="menuitem"
            className={cn(item, company.isActive ? 'text-red-600 dark:text-red-400' : 'text-emerald-700 dark:text-emerald-400')}
            onClick={() => pick('status')}
          >
            <Power className="size-4" /> {company.isActive ? 'Make inactive' : 'Activate'}
          </button>
          <button type="button" role="menuitem" className={cn(item, 'text-red-600 dark:text-red-400')} onClick={() => pick('delete')}>
            <Trash2 className="size-4" /> Delete company
          </button>
        </div>
      )}
    </div>
  );
}
