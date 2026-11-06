import { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { ChevronRight, PanelLeftClose, PanelLeftOpen, Star, X } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { cn } from '../../lib/cn';
import { useFavourites } from '../../lib/favourites';
import { roleLabel } from '../../lib/format';
import { tx } from '../../lib/i18n';
import { Avatar, IconButton } from '../ui';
import { Logo } from './Logo';
import { navFor, type NavItem, type NavSection } from './nav';

export { Logo } from './Logo';

const OPEN_KEY = 'hrms.sidebar.groups';

function readOpen(): string[] {
  try {
    return JSON.parse(localStorage.getItem(OPEN_KEY) ?? '[]') as string[];
  } catch {
    return [];
  }
}

const isActivePath = (path: string, item: NavItem) =>
  item.end ? path === item.to : path === item.to || path.startsWith(item.to + '/');

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  /** Mobile drawer mode. */
  mobile?: boolean;
  onNavigate?: () => void;
}

export function Sidebar({ collapsed, onToggleCollapsed, mobile, onNavigate }: SidebarProps) {
  const user = useUser();
  const { pathname } = useLocation();
  const sections = navFor(user);
  const isCollapsed = collapsed && !mobile;
  const { favs, toggle } = useFavourites();
  const [open, setOpen] = useState<string[]>(readOpen);

  const allItems = sections.flatMap((s) => s.items);
  const pinned = favs.map((to) => allItems.find((i) => i.to === to)).filter((i): i is NavItem => !!i);

  const toggleGroup = (title: string) =>
    setOpen((o) => {
      const next = o.includes(title) ? o.filter((t) => t !== title) : [...o, title];
      try {
        localStorage.setItem(OPEN_KEY, JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });

  const link = (item: NavItem, nested?: boolean) => (
    <li key={item.to} className="group/item relative">
      <NavLink
        to={item.to}
        end={item.end}
        onClick={onNavigate}
        title={isCollapsed ? tx(item.label) : undefined}
        className={({ isActive }) =>
          cn(
            'relative flex items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium transition-colors lg:py-1.5',
            'focus-visible:outline-2 focus-visible:outline-accent-500',
            isCollapsed && 'justify-center px-0 py-2',
            nested && !isCollapsed && 'ps-9 font-normal',
            isActive
              ? 'bg-gradient-to-r from-indigo-500/[0.12] to-fuchsia-500/[0.08] text-accent-700 shadow-[inset_0_0_0_1px_rgb(99_102_241/0.15)] before:absolute before:inset-y-1.5 before:-start-3 before:w-1 before:rounded-e-full before:bg-gradient-to-b before:from-indigo-500 before:to-fuchsia-500 rtl:bg-gradient-to-l dark:from-indigo-400/[0.16] dark:to-fuchsia-400/[0.08] dark:text-accent-200'
              : 'text-zinc-600 hover:bg-zinc-900/[0.04] hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.05] dark:hover:text-zinc-100',
          )
        }
      >
        {(!nested || isCollapsed) && <item.icon className="size-[18px] shrink-0" aria-hidden="true" />}
        {isCollapsed ? <span className="sr-only">{tx(item.label)}</span> : <span className="truncate pe-5">{tx(item.label)}</span>}
      </NavLink>
      {!isCollapsed && (
        <button
          type="button"
          aria-label={favs.includes(item.to) ? `Unpin ${item.label}` : `Pin ${item.label}`}
          onClick={() => toggle(item.to)}
          className={cn(
            'absolute top-1/2 end-1.5 -translate-y-1/2 rounded p-0.5 text-zinc-300 hover:text-amber-500 focus-visible:opacity-100 dark:text-zinc-600',
            favs.includes(item.to) ? 'opacity-100' : 'opacity-0 group-hover/item:opacity-100',
          )}
        >
          <Star className={cn('size-3.5', favs.includes(item.to) && 'fill-amber-400 text-amber-400')} />
        </button>
      )}
    </li>
  );

  const group = (section: NavSection) => {
    const title = section.title!;
    const Icon = section.icon;
    const hasActive = section.items.some((i) => isActivePath(pathname, i));
    const expanded = open.includes(title) || hasActive;
    if (isCollapsed)
      return (
        <div key={title}>
          <div className="mx-auto my-2 h-px w-6 bg-zinc-200 dark:bg-zinc-800" />
          <ul className="space-y-0.5">{section.items.map((i) => link(i))}</ul>
        </div>
      );
    return (
      <div key={title}>
        <button
          type="button"
          aria-expanded={expanded}
          onClick={() => toggleGroup(title)}
          className={cn(
            'flex w-full items-center gap-3 rounded-xl px-2.5 py-2 text-sm font-medium transition-colors lg:py-1.5',
            'focus-visible:outline-2 focus-visible:outline-accent-500',
            hasActive ? 'text-zinc-900 dark:text-white' : 'text-zinc-600 hover:bg-zinc-900/[0.04] hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-white/[0.05] dark:hover:text-zinc-100',
          )}
        >
          {Icon && <Icon className="size-[18px] shrink-0" aria-hidden="true" />}
          <span className="flex-1 truncate text-start">{tx(title)}</span>
          <ChevronRight className={cn('size-4 text-zinc-500 dark:text-zinc-400 transition-transform', expanded && 'rotate-90')} aria-hidden="true" />
        </button>
        {expanded && <ul className="mt-0.5 space-y-0.5">{section.items.map((i) => link(i, true))}</ul>}
      </div>
    );
  };

  return (
    <div className="flex h-full flex-col">
      <div className={cn('flex h-14 items-center px-4', isCollapsed ? 'justify-center' : 'justify-between')}>
        <Logo collapsed={isCollapsed} />
        {mobile && (
          <IconButton label="Close menu" onClick={onNavigate}>
            <X className="size-4" />
          </IconButton>
        )}
      </div>

      <nav aria-label="Main" className="flex-1 space-y-1 overflow-y-auto px-3 py-2">
        {sections
          .filter((s) => !s.title)
          .map((s, idx) => (
            <ul key={idx} className="space-y-0.5">
              {s.items.map((i) => link(i))}
            </ul>
          ))}

        {pinned.length > 0 && (
          <div className="pt-3">
            {isCollapsed ? (
              <div className="mx-auto mb-2 h-px w-6 bg-zinc-200 dark:bg-zinc-800" />
            ) : (
              <p className="mb-1 px-2.5 text-[11px] font-semibold tracking-wider text-zinc-500 uppercase dark:text-zinc-400">{tx('Pinned')}</p>
            )}
            <ul className="space-y-0.5">{pinned.map((i) => link(i))}</ul>
          </div>
        )}

        <div className="space-y-0.5 pt-3">
          {!isCollapsed && (
            <p className="mb-1 px-2.5 text-[11px] font-semibold tracking-wider text-zinc-500 uppercase dark:text-zinc-400">{tx('Workspace')}</p>
          )}
          {sections.filter((s) => s.title).map(group)}
        </div>
      </nav>

      <div
        className={cn('flex items-center gap-3 border-t border-zinc-200/70 px-4 py-3 dark:border-white/[0.06]', isCollapsed && 'justify-center px-0')}
        title={isCollapsed ? `${user.fullName} · ${roleLabel(user.role)}` : undefined}
      >
        <Avatar name={user.fullName} size="sm" />
        {!isCollapsed && (
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{user.fullName}</p>
            <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">
              {tx(roleLabel(user.role))}
              {user.isActingAsOwner && user.companyName ? ` · in ${user.companyName}` : ''}
            </p>
          </div>
        )}
        {!mobile && !isCollapsed && (
          <IconButton label="Collapse sidebar" onClick={onToggleCollapsed}>
            <PanelLeftClose className="size-[18px]" />
          </IconButton>
        )}
      </div>
      {!mobile && isCollapsed && (
        <div className="flex justify-center border-t border-zinc-200/70 p-3 dark:border-white/[0.06]">
          <IconButton label="Expand sidebar" onClick={onToggleCollapsed}>
            <PanelLeftOpen className="size-[18px]" />
          </IconButton>
        </div>
      )}
    </div>
  );
}
