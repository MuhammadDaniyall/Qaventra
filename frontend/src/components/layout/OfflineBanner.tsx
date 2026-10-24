import { useEffect, useState } from 'react';
import { WifiOff } from 'lucide-react';
import { tx } from '../../lib/i18n';

/** A bar under the header while the device has no connection; saving and loading data needs the network. */
export function OfflineBanner() {
  const [online, setOnline] = useState(() => navigator.onLine);
  useEffect(() => {
    const update = () => setOnline(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, []);
  if (online) return null;
  return (
    <div role="status" className="flex items-center justify-center gap-2 bg-amber-50 px-4 py-2 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
      <WifiOff className="size-4 shrink-0" aria-hidden="true" />
      {tx("You're offline. Changes can't be saved until the connection is back.")}
    </div>
  );
}
