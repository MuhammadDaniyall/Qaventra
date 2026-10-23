import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Star, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { employeesApi } from '../../api/employees';
import { useUser } from '../../auth/AuthContext';
import { isApprover } from '../../auth/roles';
import { cn } from '../../lib/cn';
import { useDebounced } from '../../lib/hooks';
import { navFor } from './nav';

const FAV_KEY = 'hrms.favourites';

/** Screens reached from inside other pages, so they can be found by name too. */
const EXTRA: { to: string; label: string; hr?: boolean }[] = [
  { to: '/settings?tab=departments', label: 'Departments', hr: true },
  { to: '/settings?tab=designations', label: 'Designations', hr: true },
  { to: '/settings?tab=lists', label: 'Grades, divisions, cost centers', hr: true },
  { to: '/settings?tab=shifts', label: 'Shifts and penalty rules', hr: true },
  { to: '/settings?tab=offices', label: 'Office locations', hr: true },
  { to: '/settings?tab=holidays', label: 'Holidays', hr: true },
  { to: '/settings?tab=leave-types', label: 'Leave types', hr: true },
  { to: '/settings?tab=approvals', label: 'Approval chains', hr: true },
  { to: '/settings?tab=custom-fields', label: 'Custom fields', hr: true },
  { to: '/leave/opening-balances', label: 'Opening leave balances', hr: true },
  { to: '/payroll/changes', label: 'Salary revisions, holds and encashment', hr: true },
  { to: '/payroll/settings', label: 'Payroll settings', hr: true },
  { to: '/payroll/funds', label: 'Provident fund, gratuity and tax adjustments', hr: true },
  { to: '/employees/new', label: 'Add employee', hr: true },
  { to: '/me/profile', label: 'My profile' },
  { to: '/suggestions', label: 'Suggestions' },
];

function readFavourites(): string[] {
  try {
    return JSON.parse(localStorage.getItem(FAV_KEY) ?? '[]') as string[];
  } catch {
    return [];
  }
}

/** Ctrl+K: jump to any screen or person; star screens to keep them at the top. */
export function QuickSearch() {
  const user = useUser();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState('');
  const [active, setActive] = useState(0);
  const [favs, setFavs] = useState<string[]>(readFavourites);
  const input = useRef<HTMLInputElement>(null);
  const approver = isApprover(user);
  const term = useDebounced(q.trim(), 250);
  const people = useQuery({
    queryKey: ['quick-search', term],
    queryFn: () => employeesApi.list({ search: term, pageSize: 6 }),
    enabled: open && approver && term.length >= 2,
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOpen((o) => !o);
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);
  useEffect(() => {
    if (open) {
      setQ('');
      setActive(0);
      setTimeout(() => input.current?.focus(), 0);
    }
  }, [open]);

  const screens = useMemo(() => {
    const items = navFor(user).flatMap((s) => s.items.map((i) => ({ to: i.to, label: i.label, section: s.title ?? '' })));
    const hr = items.some((i) => i.to === '/settings');
    for (const x of EXTRA) if (!x.hr || hr) if (!items.some((i) => i.to === x.to)) items.push({ to: x.to, label: x.label, section: 'More' });
    return items;
  }, [user]);

  const results = useMemo(() => {
    const t = q.trim().toLowerCase();
    const match = screens.filter((s) => !t || s.label.toLowerCase().includes(t) || s.section.toLowerCase().includes(t));
    const pinned = match.filter((s) => favs.includes(s.to));
    const rest = match.filter((s) => !favs.includes(s.to)).slice(0, t ? 12 : 8);
    const list: { key: string; label: string; hint: string; to: string; kind: 'screen' | 'person'; pinned?: boolean }[] = [
      ...pinned.map((s) => ({ key: s.to, label: s.label, hint: 'Pinned', to: s.to, kind: 'screen' as const, pinned: true })),
      ...rest.map((s) => ({ key: s.to, label: s.label, hint: s.section, to: s.to, kind: 'screen' as const })),
      ...(people.data?.items ?? []).map((p) => ({
        key: p.id,
        label: p.fullName,
        hint: [p.employeeCode, p.designation, p.department].filter(Boolean).join(' · '),
        to: `/employees/${p.id}`,
        kind: 'person' as const,
      })),
    ];
    return list;
  }, [q, screens, favs, people.data]);

  const go = (to: string) => {
    setOpen(false);
    navigate(to);
  };
  const toggleFav = (to: string) =>
    setFavs((f) => {
      const next = f.includes(to) ? f.filter((x) => x !== to) : [...f, to];
      try {
        localStorage.setItem(FAV_KEY, JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="hidden h-9 items-center gap-2 rounded-lg border border-zinc-200 px-3 text-sm text-zinc-500 hover:bg-zinc-50 sm:flex dark:border-zinc-700 dark:hover:bg-zinc-800"
      >
        <Search className="size-4" />
        Search
        <kbd className="rounded border border-zinc-200 px-1 text-[10px] dark:border-zinc-700">Ctrl K</kbd>
      </button>
      <button type="button" aria-label="Search" onClick={() => setOpen(true)} className="rounded-lg p-2 text-zinc-500 sm:hidden">
        <Search className="size-[18px]" />
      </button>
      {open && (
        <div className="fixed inset-0 z-[60] flex items-start justify-center bg-zinc-950/40 p-4 pt-[12vh]" onClick={() => setOpen(false)}>
          <div
            role="dialog"
            aria-label="Search"
            className="w-full max-w-lg overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center gap-2 border-b border-zinc-100 px-4 dark:border-zinc-800">
              <Search className="size-4 text-zinc-400" />
              <input
                ref={input}
                value={q}
                onChange={(e) => (setQ(e.target.value), setActive(0))}
                onKeyDown={(e) => {
                  if (e.key === 'Escape') setOpen(false);
                  else if (e.key === 'ArrowDown') (e.preventDefault(), setActive((a) => Math.min(a + 1, results.length - 1)));
                  else if (e.key === 'ArrowUp') (e.preventDefault(), setActive((a) => Math.max(a - 1, 0)));
                  else if (e.key === 'Enter' && results[active]) go(results[active].to);
                }}
                placeholder={approver ? 'Screens or people…' : 'Find a screen…'}
                className="h-12 flex-1 bg-transparent text-sm outline-none"
              />
            </div>
            <ul className="max-h-[50vh] overflow-y-auto py-2">
              {results.map((r, i) => (
                <li key={r.key}>
                  <div
                    role="option"
                    aria-selected={i === active}
                    onMouseEnter={() => setActive(i)}
                    onClick={() => go(r.to)}
                    className={cn('flex cursor-pointer items-center gap-3 px-4 py-2 text-sm', i === active && 'bg-zinc-100 dark:bg-zinc-800')}
                  >
                    {r.kind === 'person' ? <UserRound className="size-4 text-zinc-400" /> : <span className="size-4" />}
                    <span className="flex-1">{r.label}</span>
                    <span className="text-xs text-zinc-400">{r.hint}</span>
                    {r.kind === 'screen' && (
                      <button
                        type="button"
                        aria-label={r.pinned ? 'Unpin' : 'Pin'}
                        onClick={(e) => (e.stopPropagation(), toggleFav(r.to))}
                        className="text-zinc-300 hover:text-amber-500"
                      >
                        <Star className={cn('size-4', r.pinned && 'fill-amber-400 text-amber-400')} />
                      </button>
                    )}
                  </div>
                </li>
              ))}
              {!results.length && <li className="px-4 py-6 text-center text-sm text-zinc-500">Nothing found.</li>}
            </ul>
          </div>
        </div>
      )}
    </>
  );
}
