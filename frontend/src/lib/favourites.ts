import { useCallback, useSyncExternalStore } from 'react';

const KEY = 'hrms.favourites';
const listeners = new Set<() => void>();

function read(): string[] {
  try {
    return JSON.parse(localStorage.getItem(KEY) ?? '[]') as string[];
  } catch {
    return [];
  }
}

let current = read();

function subscribe(fn: () => void) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

/** Pinned screens, shared by the sidebar and quick search. */
export function useFavourites() {
  const favs = useSyncExternalStore(subscribe, () => current);
  const toggle = useCallback((to: string) => {
    current = current.includes(to) ? current.filter((x) => x !== to) : [...current, to];
    try {
      localStorage.setItem(KEY, JSON.stringify(current));
    } catch {
      // ignore
    }
    listeners.forEach((l) => l());
  }, []);
  return { favs, toggle };
}
