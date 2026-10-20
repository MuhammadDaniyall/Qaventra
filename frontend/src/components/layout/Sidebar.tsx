import { NavLink } from 'react-router-dom';
import { PanelLeftClose, PanelLeftOpen, X } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { cn } from '../../lib/cn';
import { APP_MARK, APP_NAME } from '../../lib/brand';
import { roleLabel } from '../../lib/format';
import { Avatar, IconButton } from '../ui';
import { navFor } from './nav';

export function Logo({ collapsed }: { collapsed?: boolean }) {
  return (
    <div className="flex items-center gap-2.5">
      <div className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-indigo-500 to-violet-600 text-sm font-bold text-white shadow-sm">
        {APP_MARK}
      </div>
      {!collapsed && (
        <span className="text-[15px] font-semibold tracking-tight text-zinc-900 dark:text-white">{APP_NAME}</span>
      )}
    </div>
  );
}

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  /** Mobile drawer mode. */
  mobile?: boolean;
  onNavigate?: () => void;
}

export function Sidebar({ collapsed, onToggleCollapsed, mobile, onNavigate }: SidebarProps) {
  const user = useUser();
  const sections = navFor(user);
  const isCollapsed = collapsed && !mobile;

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

      <nav aria-label="Main" className="flex-1 space-y-5 overflow-y-auto px-3 py-3">
        {sections.map((section, idx) => (
          <div key={section.title ?? idx}>
            {section.title &&
              (isCollapsed ? (
                <div className="mx-auto mb-2 h-px w-6 bg-zinc-200 dark:bg-zinc-800" />
              ) : (
                <p className="mb-1.5 px-2.5 text-[11px] font-semibold tracking-wider text-zinc-400 uppercase dark:text-zinc-500">
                  {section.title}
                </p>
              ))}
            <ul className="space-y-0.5">
              {section.items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end}
                    onClick={onNavigate}
                    title={isCollapsed ? item.label : undefined}
                    className={({ isActive }) =>
                      cn(
                        'group flex items-center gap-3 rounded-lg px-2.5 py-2 text-sm font-medium transition-colors',
                        'focus-visible:outline-2 focus-visible:outline-accent-500',
                        isCollapsed && 'justify-center px-0',
                        isActive
                          ? 'bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300'
                          : 'text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-400 dark:hover:bg-zinc-800/70 dark:hover:text-zinc-100',
                      )
                    }
                  >
                    <item.icon className="size-[18px] shrink-0" aria-hidden="true" />
                    {isCollapsed ? <span className="sr-only">{item.label}</span> : <span className="truncate">{item.label}</span>}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div
        className={cn('flex items-center gap-3 border-t border-zinc-200 px-4 py-3 dark:border-zinc-800', isCollapsed && 'justify-center px-0')}
        title={isCollapsed ? `${user.fullName} · ${roleLabel(user.role)}` : undefined}
      >
        <Avatar name={user.fullName} size="sm" />
        {!isCollapsed && (
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{user.fullName}</p>
            <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">
              {roleLabel(user.role)}
              {user.isActingAsOwner && user.companyName ? ` · in ${user.companyName}` : ''}
            </p>
          </div>
        )}
      </div>

      {!mobile && (
        <div className={cn('border-t border-zinc-200 p-3 dark:border-zinc-800', isCollapsed && 'flex justify-center')}>
          <button
            type="button"
            onClick={onToggleCollapsed}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            className={cn(
              'flex items-center gap-3 rounded-lg px-2.5 py-2 text-sm text-zinc-500 transition-colors hover:bg-zinc-100 hover:text-zinc-900',
              'focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-white',
              !isCollapsed && 'w-full',
            )}
          >
            {collapsed ? <PanelLeftOpen className="size-[18px]" /> : <PanelLeftClose className="size-[18px]" />}
            {!isCollapsed && <span>Collapse</span>}
          </button>
        </div>
      )}
    </div>
  );
}
