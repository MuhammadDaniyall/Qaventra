import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { OwnerBanner } from './OwnerBanner';
import { SubscriptionBanner } from './SubscriptionBanner';
import { isPlatformOwner } from '../../auth/roles';
import { Building2, Menu, Moon, ShieldCheck, Sun } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { cn } from '../../lib/cn';
import { useTheme } from '../../lib/theme';
import { IconButton } from '../ui';
import { Sidebar } from './Sidebar';
import { UserMenu } from './UserMenu';

const COLLAPSE_KEY = 'hrms.sidebar.collapsed';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === '1';
  } catch {
    return false;
  }
}

export function AppLayout() {
  const user = useUser();
  const { theme, toggle } = useTheme();
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  useEffect(() => setMobileOpen(false), [location.pathname]);

  useEffect(() => {
    if (!mobileOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setMobileOpen(false);
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [mobileOpen]);

  const toggleCollapsed = () =>
    setCollapsed((c) => {
      try {
        localStorage.setItem(COLLAPSE_KEY, c ? '0' : '1');
      } catch {
        // ignore
      }
      return !c;
    });

  return (
    <div className="flex min-h-screen">
      {/* Desktop sidebar */}
      <aside
        className={cn(
          'no-print sticky top-0 hidden h-screen shrink-0 border-r border-zinc-200 bg-white transition-[width] duration-200 lg:block',
          'dark:border-zinc-800 dark:bg-zinc-900/60',
          collapsed ? 'w-[68px]' : 'w-64',
        )}
      >
        <Sidebar collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
      </aside>

      {/* Mobile drawer */}
      {mobileOpen && (
        <div className="no-print fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Menu">
          <div className="absolute inset-0 animate-fade-in bg-zinc-950/40" onClick={() => setMobileOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-72 max-w-[85vw] animate-slide-in-left border-r border-zinc-200 bg-white shadow-2xl dark:border-zinc-800 dark:bg-zinc-900">
            <Sidebar collapsed={false} onToggleCollapsed={toggleCollapsed} mobile onNavigate={() => setMobileOpen(false)} />
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="no-print sticky top-0 z-30">
        {user.isActingAsOwner && <OwnerBanner companyName={user.companyName ?? 'this company'} />}
        <header className="flex h-14 items-center gap-3 border-b border-zinc-200 bg-white/80 px-4 backdrop-blur sm:px-6 dark:border-zinc-800 dark:bg-zinc-950/80">
          <IconButton label="Open menu" className="lg:hidden" onClick={() => setMobileOpen(true)}>
            <Menu className="size-5" />
          </IconButton>
          <div className="flex min-w-0 items-center gap-2 text-sm">
            {isPlatformOwner(user) ? (
              <>
                <ShieldCheck className="size-4 shrink-0 text-accent-500" />
                <span className="truncate font-medium text-zinc-800 dark:text-zinc-200">Platform administration</span>
              </>
            ) : (
              <>
                <Building2 className="size-4 shrink-0 text-zinc-400" />
                <span className="truncate font-medium text-zinc-800 dark:text-zinc-200">{user.companyName}</span>
              </>
            )}
          </div>
          <div className="ml-auto flex items-center gap-1.5">
            <IconButton label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'} onClick={toggle}>
              {theme === 'dark' ? <Sun className="size-[18px]" /> : <Moon className="size-[18px]" />}
            </IconButton>
            <UserMenu />
          </div>
        </header>
        <SubscriptionBanner user={user} />
        </div>
        <main className="print-reset mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
