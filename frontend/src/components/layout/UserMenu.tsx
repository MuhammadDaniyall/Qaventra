import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronDown, KeyRound, Languages, LogOut, UserCircle } from 'lucide-react';
import { useAuth, useUser } from '../../auth/AuthContext';
import { cn } from '../../lib/cn';
import { roleLabel } from '../../lib/format';
import { tx, useT } from '../../lib/i18n';
import { Avatar } from '../ui';
import { ChangePasswordDialog } from './ChangePasswordDialog';

export function UserMenu() {
  const user = useUser();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [pwOpen, setPwOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const { lang, setLang } = useT();

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const item =
    'flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-start text-sm text-zinc-700 hover:bg-zinc-100 focus-visible:bg-zinc-100 focus-visible:outline-none dark:text-zinc-200 dark:hover:bg-zinc-800 dark:focus-visible:bg-zinc-800';

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="menu"
        aria-expanded={open}
        className="flex items-center gap-2 rounded-lg p-1 pe-2 transition-colors hover:bg-zinc-100 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-zinc-800"
      >
        <Avatar name={user.fullName} />
        <span className="hidden text-start sm:block">
          <span className="block max-w-36 truncate text-sm leading-tight font-medium text-zinc-900 dark:text-zinc-100">
            {user.fullName}
          </span>
          <span className="block text-xs leading-tight text-zinc-500 dark:text-zinc-400">{tx(roleLabel(user.role))}</span>
        </span>
        <ChevronDown className={cn('hidden size-4 text-zinc-500 dark:text-zinc-400 transition-transform sm:block', open && 'rotate-180')} />
      </button>

      {open && (
        <div
          role="menu"
          className="absolute end-0 z-40 mt-2 w-60 animate-pop-in rounded-xl border border-zinc-200 bg-white p-1.5 shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
        >
          <div className="px-2.5 py-2">
            <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{user.fullName}</p>
            <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">{user.email}</p>
          </div>
          <div className="my-1 h-px bg-zinc-100 dark:bg-zinc-800" />
          {user.employeeId && (
            <button
              type="button"
              role="menuitem"
              className={item}
              onClick={() => {
                setOpen(false);
                navigate('/me/profile');
              }}
            >
              <UserCircle className="size-4 text-zinc-500 dark:text-zinc-400" /> {tx('My profile')}
            </button>
          )}
          <button
            type="button"
            role="menuitem"
            className={item}
            onClick={() => {
              setOpen(false);
              setPwOpen(true);
            }}
          >
            <KeyRound className="size-4 text-zinc-500 dark:text-zinc-400" /> {tx('Change password')}
          </button>
          <button
            type="button"
            role="menuitem"
            className={item}
            onClick={() => {
              setOpen(false);
              setLang(lang === 'ur' ? 'en' : 'ur');
            }}
          >
            <Languages className="size-4 text-zinc-500 dark:text-zinc-400" />
            {/* Each option is written in its own language so people can find it. */}
            {lang === 'ur' ? <span lang="en">English</span> : <span lang="ur">اردو</span>}
          </button>
          <div className="my-1 h-px bg-zinc-100 dark:bg-zinc-800" />
          <button
            type="button"
            role="menuitem"
            className={cn(item, 'text-red-600 dark:text-red-400')}
            onClick={async () => {
              setOpen(false);
              await logout();
              navigate('/login', { replace: true });
            }}
          >
            <LogOut className="size-4" /> {tx('Sign out')}
          </button>
        </div>
      )}
      <ChangePasswordDialog open={pwOpen} onClose={() => setPwOpen(false)} />
    </div>
  );
}
