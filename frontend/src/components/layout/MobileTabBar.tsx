import { NavLink } from 'react-router-dom';
import { Menu } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { isApprover } from '../../auth/roles';
import { cn } from '../../lib/cn';
import { MOBILE_TABS } from './nav';

const tabCls = 'flex flex-1 flex-col items-center justify-center gap-0.5 py-2 text-[11px] font-medium transition-colors';

/** Bottom tab bar on phones; "More" opens the full menu drawer. */
export function MobileTabBar({ onMore }: { onMore: () => void }) {
  const user = useUser();
  const approver = isApprover(user);
  const tabs = MOBILE_TABS.filter((t) => (!t.needsEmployee || user.employeeId) && (!t.approver || approver)).slice(0, 4);

  return (
    <nav
      aria-label="Quick"
      className="no-print fixed inset-x-0 bottom-0 z-40 flex border-t border-zinc-200 bg-white/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden dark:border-zinc-800 dark:bg-zinc-950/95"
    >
      {tabs.map((t) => (
        <NavLink
          key={t.to}
          to={t.to}
          end={t.end}
          className={({ isActive }) =>
            cn(tabCls, isActive ? 'text-accent-600 dark:text-accent-400' : 'text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white')
          }
        >
          <t.icon className="size-5" aria-hidden="true" />
          {t.label}
        </NavLink>
      ))}
      <button type="button" onClick={onMore} className={cn(tabCls, 'text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white')}>
        <Menu className="size-5" aria-hidden="true" />
        More
      </button>
    </nav>
  );
}
