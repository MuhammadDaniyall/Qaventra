import { useSyncExternalStore } from 'react';

/** Chrome/Edge/Android offer "install this app" through an event we keep until the person asks for it. */
interface InstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

let deferred: InstallPromptEvent | null = null;
const listeners = new Set<() => void>();
const notify = () => listeners.forEach((l) => l());

if (typeof window !== 'undefined') {
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferred = e as InstallPromptEvent;
    notify();
  });
  window.addEventListener('appinstalled', () => {
    deferred = null;
    notify();
  });
}

const subscribe = (fn: () => void) => {
  listeners.add(fn);
  return () => listeners.delete(fn);
};

/** True when the browser will install the app on request (not already installed). */
export function useCanInstall() {
  return useSyncExternalStore(subscribe, () => deferred !== null);
}

export async function installApp() {
  const e = deferred;
  if (!e) return;
  await e.prompt();
  await e.userChoice.catch(() => undefined);
  deferred = null;
  notify();
}
