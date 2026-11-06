import { NavLink } from 'react-router-dom';
import { Menu } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { isApprover } from '../../auth/roles';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';
import { MOBILE_TABS } from './nav';

const tabCls =
  'group relative flex min-h-14 min-w-0 flex-1 flex-col items-center justify-center gap-1 text-[11px] font-medium transition-colors focus-visible:outline-none';
const pill =
  'flex h-8 w-14 items-center justify-center rounded-full transition-all duration-200 group-active:scale-90 group-focus-visible:ring-2 group-focus-visible:ring-accent-500';

/** Bottom tab bar on phones; "More" opens the full menu drawer. */
export function MobileTabBar({ onMore }: { onMore: () => void }) {
  const user = useUser();
  const approver = isApprover(user);
  const tabs = MOBILE_TABS.filter(
    (t) => (!t.needsEmployee || user.employeeId) && (!t.approver || approver) && !(t.plan && user.companyModules && !user.companyModules.includes(t.plan)),
  ).slice(0, 4);

  return (
    <nav
      aria-label="Quick"
      className="no-print glass fixed inset-x-0 bottom-0 z-40 flex border-t border-zinc-200/70 px-[env(safe-area-inset-left)] pb-[env(safe-area-inset-bottom)] shadow-[0_-8px_30px_-12px_rgb(79_70_229/0.18)] lg:hidden dark:border-white/[0.07]"
    >
      {tabs.map((t) => (
        <NavLink
          key={t.to}
          to={t.to}
          end={t.end}
          className={({ isActive }) =>
            cn(tabCls, isActive ? 'text-accent-700 dark:text-accent-300' : 'text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white')
          }
        >
          {({ isActive }) => (
            <>
              <span
                className={cn(
                  pill,
                  isActive &&
                    'bg-gradient-to-r from-indigo-500/15 to-fuchsia-500/15 text-accent-600 dark:from-indigo-400/20 dark:to-fuchsia-400/20 dark:text-accent-300',
                )}
              >
                <t.icon className={cn('size-5', isActive && 'stroke-[2.25]')} aria-hidden="true" />
              </span>
              <span className="max-w-full truncate px-1">{tx(t.label)}</span>
            </>
          )}
        </NavLink>
      ))}
      <button type="button" onClick={onMore} className={cn(tabCls, 'text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white')}>
        <span className={pill}>
          <Menu className="size-5" aria-hidden="true" />
        </span>
        <span>{tx('More')}</span>
      </button>
    </nav>
  );
}
