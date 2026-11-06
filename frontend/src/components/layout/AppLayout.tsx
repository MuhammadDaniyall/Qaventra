import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { OwnerBanner } from './OwnerBanner';
import { SubscriptionBanner } from './SubscriptionBanner';
import { OfflineBanner } from './OfflineBanner';
import { ChangePasswordDialog } from './ChangePasswordDialog';
import { isPlatformOwner } from '../../auth/roles';
import { Building2, Menu, Moon, ShieldCheck, Sun } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { cn } from '../../lib/cn';
import { useTheme } from '../../lib/theme';
import { IconButton } from '../ui';
import { Sidebar } from './Sidebar';
import { NotificationBell } from './NotificationBell';
import { UserMenu } from './UserMenu';
import { QuickSearch } from './QuickSearch';
import { AskAi } from './AskAi';
import { MobileTabBar } from './MobileTabBar';

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
    <div className="relative isolate flex min-h-screen">
      {/* Soft brand glows behind everything, as on the sign-in page. */}
      <div className="no-print pointer-events-none fixed inset-0 -z-10 overflow-hidden" aria-hidden="true">
        <div className="absolute -top-48 start-1/4 size-[560px] rounded-full bg-indigo-400/[0.13] blur-[120px] motion-safe:animate-glow dark:bg-indigo-500/[0.12]" />
        <div className="absolute top-1/3 -end-40 size-[460px] rounded-full bg-fuchsia-400/[0.10] blur-[120px] motion-safe:animate-glow dark:bg-fuchsia-500/[0.08]" style={{ animationDelay: '-7s' }} />
        <div className="absolute -bottom-40 -start-32 size-[420px] rounded-full bg-sky-300/[0.10] blur-[120px] dark:bg-violet-500/[0.07]" />
      </div>

      {/* Desktop sidebar */}
      <aside
        className={cn(
          'no-print glass sticky top-0 hidden h-screen shrink-0 border-e border-zinc-200/70 transition-[width] duration-200 lg:block',
          'dark:border-white/[0.06]',
          collapsed ? 'w-[68px]' : 'w-64',
        )}
      >
        <Sidebar collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
      </aside>

      {/* Mobile drawer */}
      {mobileOpen && (
        <div className="no-print fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Menu">
          <div className="absolute inset-0 animate-fade-in bg-zinc-950/50 backdrop-blur-sm" onClick={() => setMobileOpen(false)} />
          <aside className="absolute inset-y-0 start-0 w-72 max-w-[85vw] animate-slide-in-left rtl:animate-slide-in-right rounded-e-3xl border-e border-zinc-200/80 bg-white pt-[env(safe-area-inset-top)] pb-[env(safe-area-inset-bottom)] shadow-2xl dark:border-white/10 dark:bg-zinc-900">
            <Sidebar collapsed={false} onToggleCollapsed={toggleCollapsed} mobile onNavigate={() => setMobileOpen(false)} />
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="no-print sticky top-0 z-30">
        {user.isActingAsOwner && <OwnerBanner companyName={user.companyName ?? 'this company'} />}
        <header className="glass flex min-h-14 items-center gap-2 border-b border-zinc-200/70 ps-[max(1rem,env(safe-area-inset-left))] pe-[max(1rem,env(safe-area-inset-right))] pt-[env(safe-area-inset-top)] sm:gap-3 sm:px-6 dark:border-white/[0.06]">
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
                <span className="flex size-7 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-indigo-500 to-fuchsia-500 text-white shadow-sm shadow-indigo-500/30">
                  <Building2 className="size-3.5" />
                </span>
                <span className="truncate font-medium text-zinc-800 dark:text-zinc-200">{user.companyName}</span>
              </>
            )}
          </div>
          <div className="ms-auto flex items-center gap-1.5">
            {!isPlatformOwner(user) && <QuickSearch />}
            {!isPlatformOwner(user) && <AskAi />}
            <IconButton label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'} onClick={toggle}>
              {theme === 'dark' ? <Sun className="size-[18px]" /> : <Moon className="size-[18px]" />}
            </IconButton>
            {!isPlatformOwner(user) && <NotificationBell />}
            <UserMenu />
          </div>
        </header>
        <SubscriptionBanner user={user} />
        <OfflineBanner />
        </div>
        <main
          className={cn(
            'print-reset mx-auto w-full max-w-7xl min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8',
            !isPlatformOwner(user) && 'pb-[calc(6rem+env(safe-area-inset-bottom))] lg:pb-8',
          )}
        >
          <Outlet />
        </main>
        {!isPlatformOwner(user) && <MobileTabBar onMore={() => setMobileOpen(true)} />}
        {user.mustChangePassword && <ChangePasswordDialog open forced onClose={() => undefined} />}
      </div>
    </div>
  );
}
